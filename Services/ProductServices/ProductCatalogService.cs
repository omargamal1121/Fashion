using AutoMapper;
using E_Commers.DtoModels.CategoryDtos;
using E_Commers.DtoModels.CollectionDtos;
using E_Commers.DtoModels.DiscoutDtos;
using E_Commers.DtoModels.ImagesDtos;
using E_Commers.DtoModels.ProductDtos;
using E_Commers.DtoModels.Responses;
using E_Commers.Enums;
using E_Commers.ErrorHnadling;
using E_Commers.Interfaces;
using E_Commers.Models;
using E_Commers.Services.AdminOpreationServices;
using E_Commers.Services.EmailServices;
using E_Commers.UOW;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using E_Commers.Services.Cache;
using Hangfire;
using System.Linq.Expressions;

namespace E_Commers.Services.ProductServices
{
	public interface IProductCatalogService
	{
		
		Task<Result<ProductDetailDto>> GetProductByIdAsync(int id, bool? isActive, bool? deletedOnly);
		public void UpdateProductQuantity(E_Commers.Models.Product Product  );
		Task<Result<ProductDto>> CreateProductAsync(CreateProductDto dto, string userId);
		Task<Result<ProductDto>> UpdateProductAsync(int id, UpdateProductDto dto, string userId);
		Task<Result<bool>> DeleteProductAsync(int id, string userId);
		Task<Result<ProductDto>> RestoreProductAsync(int id, string userId);
		Task<Result<List<ProductDto>>> GetProductsBySubCategoryId(int SubCategoryid, bool? isActive, bool? deletedOnly);
		Task<Result<bool>> ActivateProductAsync(int productId, string userId);
		Task<Result<bool>> DeactivateProductAsync(int productId, string userId);
	}

	public class ProductCatalogService : IProductCatalogService
	{
		private readonly IUnitOfWork _unitOfWork;
		private readonly ISubCategoryServices _subCategoryServices;
		private readonly IBackgroundJobClient _backgroundJobClient;
		private readonly ILogger<ProductCatalogService> _logger;
		private readonly IAdminOpreationServices _adminOpreationServices;
		private readonly IErrorNotificationService _errorNotificationService;
		private readonly ICacheManager _cacheManager;
		private const string CACHE_TAG_PRODUCT_SEARCH = "product_search";

		public const string CACHE_TAG_CATEGORY_WITH_DATA = "categorywithdata";
		private static readonly string[] PRODUCT_CACHE_TAGS = new[] { CACHE_TAG_PRODUCT_SEARCH, CACHE_TAG_CATEGORY_WITH_DATA, PRODUCT_WITH_VARIANT_TAG };
		private const string PRODUCT_WITH_VARIANT_TAG = "productwithvariantdata";

		public ProductCatalogService( 
			IBackgroundJobClient backgroundJobClient,
			IUnitOfWork unitOfWork,
			ISubCategoryServices subCategoryServices,
			ILogger<ProductCatalogService> logger,
			IAdminOpreationServices adminOpreationServices,
			IErrorNotificationService errorNotificationService,
			ICacheManager cacheManager)
		{
			_backgroundJobClient = backgroundJobClient;
			_unitOfWork = unitOfWork;
			_subCategoryServices = subCategoryServices;
			_logger = logger;
			_adminOpreationServices = adminOpreationServices;
			_errorNotificationService = errorNotificationService;
			_cacheManager = cacheManager;
		}
		private string GetProductByIdCacheKey(int id, bool? isActive, bool? deletedOnly) => $"product_detail:{id}:isActive={isActive}:deletedOnly={deletedOnly}";
		private string GetProductsBySubCategoryCacheKey(int subCategoryId, bool? isActive, bool? deletedOnly) => $"products_subcategory:{subCategoryId}:isActive={isActive}:deletedOnly={deletedOnly}";


		private static Expression<Func< E_Commers.Models.Product, ProductDetailDto>> maptoProductDetailDtoexpression = p =>
		 new ProductDetailDto
		 {
			 Id = p.Id,
			 Name = p.Name,
			 Description = p.Description,
			 AvailableQuantity = p.Quantity,
			 Gender = p.Gender,
			 SubCategoryId = p.SubCategoryId,
			 Discount = (p.Discount != null && p.Discount.IsActive && (p.Discount.DeletedAt == null) && (p.Discount.EndDate == null || p.Discount.EndDate > DateTime.UtcNow)) ? new DiscountDto
			 {
				 Id = p.Discount.Id,
				 DiscountPercent = p.Discount.DiscountPercent,
				 IsActive = p.Discount.IsActive,
				 StartDate = p.Discount.StartDate,
				 EndDate = p.Discount.EndDate,
				 Name = p.Discount.Name,
				 Description = p.Discount.Description
			 } : null,
			 Images = p.Images.Where(i => i.DeletedAt == null).Select(i => new ImageDto
			 {
				 Id = i.Id,
				 Url = i.Url
			 }).ToList(),
			 Variants = p.ProductVariants.Where(v => v.DeletedAt == null && v.Quantity != 0).Select(v => new ProductVariantDto
			 {
				 Id = v.Id,
				 Color = v.Color,
				 Size = v.Size,
				 Waist = v.Waist,
				 Length = v.Length,
				 Quantity = v.Quantity,
				 ProductId = v.ProductId
			 }).ToList()
		 };




		public async Task<Result<ProductDetailDto>> GetProductByIdAsync(int id, bool? isActive, bool? deletedOnly)
		{
			var cacheKey = GetProductByIdCacheKey(id, isActive, deletedOnly);
			var cached = await _cacheManager.GetAsync<ProductDetailDto>(cacheKey);
			if (cached != null)
				return Result<ProductDetailDto>.Ok(cached, "Product retrieved from cache", 200);
			try
			{
				var query = _unitOfWork.Product.GetAll().AsNoTracking().Where(p => p.Id == id);

				if (deletedOnly.HasValue)
				{
					if (deletedOnly.Value)
						query = query.Where(p => p.DeletedAt != null);
					else
						query = query.Where(p => p.DeletedAt == null);
				}

				if (isActive.HasValue)
				{
					query = query.Where(p => p.IsActive==isActive);
				}

				var product = await query
					.Select(maptoProductDetailDtoexpression)
					.FirstOrDefaultAsync();

				if (product == null)
					return Result<ProductDetailDto>.Fail("Product not found", 404);

				BackgroundJob.Enqueue(() => _cacheManager.SetAsync(cacheKey, product, null, new[] { PRODUCT_WITH_VARIANT_TAG }));
				return Result<ProductDetailDto>.Ok(product, "Product retrieved successfully", 200);
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, $"Error in GetProductByIdAsync for id: {id}");
				 	_backgroundJobClient.Enqueue(()=> _errorNotificationService.SendErrorNotificationAsync(ex.Message, ex.StackTrace));
				return Result<ProductDetailDto>.Fail("Error retrieving product", 500);
			}
		}

		private  void RemoveProductCachesAsync()
		{
			
			BackgroundJob.Enqueue(() => _cacheManager.RemoveByTagsAsync(PRODUCT_CACHE_TAGS));
		}
		private ProductDto Maptoproductdto(E_Commers.Models. Product p)
		{
			var productdto = new ProductDto
			{
				Id = p.Id,
				Name = p.Name,
				IsActive = p.IsActive,
				AvailableQuantity = p.Quantity,
				Price = p.Price,
				Description = p.Description,
				SubCategoryId = p.SubCategoryId,
				CreatedAt = p.CreatedAt,
				FinalPrice = p.FinalPrice,
				fitType = p.fitType,
				Gender = p.Gender,
				ModifiedAt = p.ModifiedAt,
				DeletedAt = p.DeletedAt,
			};
			if (p.Discount != null && p.Discount.IsActive && (p.Discount.DeletedAt == null) && (p.Discount.EndDate == null || p.Discount.EndDate > DateTime.UtcNow)){
				productdto.EndAt = p.Discount.EndDate;
				productdto.DiscountPrecentage = p.Discount.DiscountPercent;
				productdto.DiscountName = p.Discount.Name;
			}
			if (p.Images != null)
				productdto.images = p.Images.Where(i => i.DeletedAt == null).Select(i => new ImageDto
				{
					Id = i.Id,
					IsMain = i.IsMain,
					Url = i.Url
				});


			return productdto;

			
			
		}

		public async Task<Result<ProductDto>> CreateProductAsync(CreateProductDto dto, string userId)
		{
			_logger.LogInformation($"Creating new product: {dto.Name}");
				using var transaction = await _unitOfWork.BeginTransactionAsync();
			try
			{
				// Validate category exists
				var categoryExists = await _unitOfWork.SubCategory.IsExsistAsync(dto.Subcategoryid);
				if (!categoryExists)
					return Result<ProductDto>.Fail("Category not found", 404);

				var isesist= await _unitOfWork.Product.IsExsistByNameAsync(dto.Name);
				if (isesist)
					return Result<ProductDto>.Fail($"thier's Product With Same Name:{dto.Name}", 404);


				var product = new Models.Product
				{
					Name = dto.Name,
					Description = dto.Description,
					SubCategoryId = dto.Subcategoryid,
					Gender = dto.Gender,
					IsActive=false,
					Price=dto.Price,
					fitType=dto.fitType,
					
				};

				
			
				var result = await _unitOfWork.Product.CreateAsync(product);
				if (result == null)
				{
					await transaction.RollbackAsync();
					return Result<ProductDto>.Fail("Failed to create product", 400);
				}

				// Log admin operation
				await _adminOpreationServices.AddAdminOpreationAsync(
					$"Create Product {product.Id}",
					E_Commers.Enums.Opreations.AddOpreation,
					userId,
					product.Id
				);
				await _unitOfWork.CommitAsync();
				await transaction.CommitAsync();
				 RemoveProductCachesAsync();
				var productdto = Maptoproductdto(product);
				return Result<ProductDto>.Ok(productdto, "Product created successfully", 201);
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, $"Unexpected error in CreateProductAsync for product {dto.Name}");
				await transaction.RollbackAsync();
				 	_backgroundJobClient.Enqueue(()=> _errorNotificationService.SendErrorNotificationAsync(ex.Message, ex.StackTrace));
				return Result<ProductDto>.Fail("Unexpected error occurred while creating product", 500);
			}
		}

		public async Task<Result<ProductDto>> UpdateProductAsync(int id, UpdateProductDto dto, string userId)
		{
			_logger.LogInformation($"Updating product: {id}");

			try
			{
				var updates = new List<string> () ;
				var product = await _unitOfWork.Product.GetProductByIdAsync(id,null,false);

				if (product == null||product.DeletedAt!=null)
					return Result<ProductDto>.Fail("Product not found", 404);

				if (!string.IsNullOrEmpty(dto.Name)){
					updates.Add($"change name from :{product.Name} to {dto.Name}");
					product.Name = dto.Name;
				}

				if (!string.IsNullOrEmpty(dto.Description)){
					updates.Add($"change description from :{product.Description} to {dto.Description}");

					product.Description = dto.Description;
}
				if (dto.SubCategoryid.HasValue)
				{
					var subCatCheck = await _subCategoryServices.IsExsistAsync(dto.SubCategoryid.Value);
					if (!subCatCheck.Success)
						return Result<ProductDto>.Fail("SubCategory not found", 404);

					updates.Add($"change SubCategory from :{product.SubCategoryId} to {dto.SubCategoryid.Value}");
					product.SubCategoryId = dto.SubCategoryid.Value;
				}

			
				if (dto.Price.HasValue){
					updates.Add($"change Price from :{product.Price} to {dto.Price.Value}");
					product.Price = dto.Price.Value;
				}

				if (dto.Gender.HasValue){
					updates.Add($"change Gender from :{product.Gender} to {dto.Gender.Value}");
					product.Gender = dto.Gender.Value;
				}

				if (dto.fitType.HasValue){
					updates.Add($"change fitType from :{product.fitType} to {dto.fitType.Value}");
					product.fitType = dto.fitType.Value;
				}

				if (updates.Count == 0)
					return Result<ProductDto>.Fail("No updates provided", 400);

				var result = _unitOfWork.Product.Update(product);
				if (!result)
					return Result<ProductDto>.Fail("Failed to update product", 400);

				await _adminOpreationServices.AddAdminOpreationAsync(
					updates.ToString(),
					E_Commers.Enums.Opreations.UpdateOpreation,
					userId,
					id
				);
				 RemoveProductCachesAsync();
				
				var productDetailDto =Maptoproductdto(product);
				return Result<ProductDto>.Ok(productDetailDto, "Product updated successfully", 200);
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, $"Error in UpdateProductAsync for id: {id}");
				_backgroundJobClient.Enqueue(()=> _errorNotificationService.SendErrorNotificationAsync(ex.Message, ex.StackTrace));
				return Result<ProductDto>.Fail("Error updating product", 500);
			}
		}
		public async Task<Result<bool>> DeleteProductAsync(int id, string userId)
		{
			_logger.LogInformation($"Deleting product: {id}");
			try
			{
				var product = await _unitOfWork.Product.GetByIdAsync(id);
				if (product == null)
					return Result<bool>.Fail("Product not found", 404);

				product.IsActive = false;
				var result = await _unitOfWork.Product.SoftDeleteAsync(id);
				if (!result)
					return Result<bool>.Fail("Failed to delete product", 400);

				// Log admin operation
				await _adminOpreationServices.AddAdminOpreationAsync(
					$"Delete Product {id}",
					E_Commers.Enums.Opreations.DeleteOpreation,
					userId,
					id
				);
				 await _unitOfWork.CommitAsync();
				RemoveProductCachesAsync();
				await _subCategoryServices.DeactivateSubCategoryIfAllProductsAreInactiveAsync(product.SubCategoryId, userId);
				return Result<bool>.Ok(true, "Product deleted", 200);
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, $"Error in DeleteProductAsync for id: {id}");
				 	_backgroundJobClient.Enqueue(()=> _errorNotificationService.SendErrorNotificationAsync(ex.Message, ex.StackTrace));
				return Result<bool>.Fail("Error deleting product", 500);
			}
		}

		public async Task<Result<ProductDto>> RestoreProductAsync(int id, string userId)
		{
			_logger.LogInformation($"Restoring product: {id}");
			try
			{
				var product = await _unitOfWork.Product.GetAll().FirstOrDefaultAsync(p=>p.Id==id);
				if (product == null)
					return Result<ProductDto>.Fail("Product not found", 404);

				product.DeletedAt = null;
				var result = _unitOfWork.Product.Update(product);
				if (!result)
					return Result<ProductDto>.Fail("Failed to restore product", 400);

				// Log admin operation
				await _adminOpreationServices.AddAdminOpreationAsync(
					$"Restore Product {id}",
					E_Commers.Enums.Opreations.UpdateOpreation,
					userId,
					id
				);
				 RemoveProductCachesAsync();
				var productdto = Maptoproductdto(product);
				return Result<ProductDto>.Ok(productdto, "Product restored successfully", 200);
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, $"Error in RestoreProductAsync for id: {id}");
				BackgroundJob.Enqueue(()=> _errorNotificationService.SendErrorNotificationAsync(ex.Message, ex.StackTrace));
				return Result<ProductDto>.Fail("Error restoring product", 500);
			}
		}

		public async Task<Result<List<ProductDto>>> GetProductsBySubCategoryId(int SubCategoryid, bool? isActive, bool? deletedOnly)
		{
			var cacheKey = GetProductsBySubCategoryCacheKey(SubCategoryid, isActive, deletedOnly);
			var cached = await _cacheManager.GetAsync<List<ProductDto>>(cacheKey);
			if (cached != null)
				return Result<List<ProductDto>>.Ok(cached, "Products by Category (from cache)", 200);
			try
			{
				var isfound = await _subCategoryServices.IsExsistAsync(SubCategoryid);
				if (!isfound.Success)
					return Result<List<ProductDto>>.Fail($"No Category with this id:{SubCategoryid}", 404);

				var productsQuery = _unitOfWork.Product.GetAll().AsNoTracking().Where(p => p.SubCategoryId == SubCategoryid);

				if (deletedOnly.HasValue)
				{
					if (deletedOnly.Value)
						productsQuery = productsQuery.Where(p => p.DeletedAt != null);
					else
						productsQuery = productsQuery.Where(p => p.DeletedAt == null);
				}

				if (isActive.HasValue)
				{
					productsQuery = productsQuery.Where(p => p.IsActive == isActive);
				}

				if (productsQuery == null)
					return Result<List<ProductDto>>.Fail("No Products Found", 404);

				var products = await productsQuery
					.Select(p => new ProductDto
					{
						Id = p.Id,
						Name = p.Name,
						IsActive = p.IsActive,
						AvailableQuantity = p.Quantity,
						Price = p.Price,
						Description = p.Description,
						SubCategoryId = p.SubCategoryId,
						CreatedAt = p.CreatedAt,
						DiscountPrecentage = (p.Discount != null && p.Discount.IsActive && (p.Discount.DeletedAt == null) && (p.Discount.EndDate == null || p.Discount.EndDate > DateTime.UtcNow)) ? p.Discount.DiscountPercent : null,
						FinalPrice = p.FinalPrice,
						DiscountName = (p.Discount != null && p.Discount.IsActive && (p.Discount.DeletedAt == null) && (p.Discount.EndDate == null || p.Discount.EndDate > DateTime.UtcNow)) ? p.Discount.Name : null,
						EndAt = (p.Discount != null && p.Discount.IsActive && (p.Discount.DeletedAt == null) && (p.Discount.EndDate == null || p.Discount.EndDate > DateTime.UtcNow)) ? p.Discount.EndDate : null,
						fitType = p.fitType,
						Gender = p.Gender,
						ModifiedAt = p.ModifiedAt,
						DeletedAt = p.DeletedAt,
						images = p.Images.Select(img => new ImageDto
						{
							Id = img.Id,
							IsMain = img.IsMain,
							Url = img.Url
						})
					})
					.ToListAsync();

				BackgroundJob.Enqueue(() => _cacheManager.SetAsync(cacheKey, products, null, new string[] { CACHE_TAG_PRODUCT_SEARCH }));
				return Result<List<ProductDto>>.Ok(products, "Products by Category", 200);
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, $"Error in GetProductsBySubCategoryId for sub categoryId: {SubCategoryid}");
				_backgroundJobClient.Enqueue(() => _errorNotificationService.SendErrorNotificationAsync(ex.Message, ex.StackTrace));
				return Result<List<ProductDto>>.Fail("Error retrieving products by category", 500);
			}
		}

		public async Task<Result<bool>> ActivateProductAsync(int productId, string userId)
		{
			var product = await _unitOfWork.Product.GetByIdAsync(productId);
			if (product == null)
				return Result<bool>.Fail("Product not found", 404);
			if (product.IsActive)
				return Result<bool>.Ok(true, "Product already active", 200);

			product.IsActive = true;
			var result = _unitOfWork.Product.Update(product);
			if (!result)
				return Result<bool>.Fail("Failed to activate product", 400);

			await _adminOpreationServices.AddAdminOpreationAsync(
				$"Activate Product {productId}",
				E_Commers.Enums.Opreations.UpdateOpreation,
				userId,
				productId
			);

			await _unitOfWork.CommitAsync();
			RemoveProductCachesAsync();
			return Result<bool>.Ok(true, "Product activated successfully", 200);
		}

		public async Task<Result<bool>> DeactivateProductAsync(int productId, string userId)
		{
			var product = await _unitOfWork.Product.GetByIdAsync(productId);
			if (product == null)
				return Result<bool>.Fail("Product not found", 404);
			if (!product.IsActive)
				return Result<bool>.Ok(true, "Product already inactive", 200);

			product.IsActive = false;
			var result = _unitOfWork.Product.Update(product);
			if (!result)
				return Result<bool>.Fail("Failed to deactivate product", 400);

			await _adminOpreationServices.AddAdminOpreationAsync(
				$"Deactivate Product {productId}",
				E_Commers.Enums.Opreations.UpdateOpreation,
				userId,
				productId
			);

			await _unitOfWork.CommitAsync();
			RemoveProductCachesAsync();

			// Check if subcategory has any other active products
			var activeProducts = await _unitOfWork.Product.GetAll()
				.Where(p => p.SubCategoryId == product.SubCategoryId && p.IsActive && p.DeletedAt == null)
				.AnyAsync();
			if (!activeProducts)
			{
				await _subCategoryServices.DeactivateSubCategoryAsync(product.SubCategoryId, userId);
			}

			return Result<bool>.Ok(true, "Product deactivated successfully", 200);
		}

		// Updates the product's quantity based on the sum of all non-deleted variant quantities
		public void UpdateProductQuantity(Models.Product product)
		{
			if (product.ProductVariants != null)
			{
				product.Quantity = product.ProductVariants
					.Where(v => v.DeletedAt == null&&v.IsActive)
					.Sum(v => v.Quantity);
			}
		}

	}
} 