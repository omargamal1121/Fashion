using AutoMapper;
using E_Commerce.DtoModels.CategoryDtos;
using E_Commerce.DtoModels.CollectionDtos;
using E_Commerce.DtoModels.DiscoutDtos;
using E_Commerce.DtoModels.ImagesDtos;
using E_Commerce.DtoModels.ProductDtos;
using E_Commerce.DtoModels.Responses;
using E_Commerce.Enums;
using E_Commerce.ErrorHnadling;
using E_Commerce.Interfaces;
using E_Commerce.Models;
using E_Commerce.Services.AdminOpreationServices;
using E_Commerce.Services.EmailServices;
using E_Commerce.UOW;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using E_Commerce.Services.Cache;
using Hangfire;
using System.Linq.Expressions;

namespace E_Commerce.Services.ProductServices
{
	public interface IProductCatalogService
	{
		
		Task<Result<ProductDetailDto>> GetProductByIdAsync(int id, bool? isActive, bool? deletedOnly);
		public void UpdateProductQuantity(int Productid  );
		Task<Result<ProductDto>> CreateProductAsync(CreateProductDto dto, string userId);
		Task<Result<ProductDto>> UpdateProductAsync(int id, UpdateProductDto dto, string userId);
		Task<Result<bool>> DeleteProductAsync(int id, string userId);
		Task<Result<bool>> RestoreProductAsync(int id, string userId);
		Task<Result<List<ProductDto>>> GetProductsBySubCategoryId(int SubCategoryid, bool? isActive, bool? deletedOnly);
		Task<Result<bool>> ActivateProductAsync(int productId, string userId);
		Task<Result<bool>> DeactivateProductAsync(int productId, string userId);
	}

	public class ProductCatalogService : IProductCatalogService
	{
		private readonly IUnitOfWork _unitOfWork;
		private readonly ICartServices _cartServices;
		private readonly ISubCategoryServices _subCategoryServices;
		private readonly IBackgroundJobClient _backgroundJobClient;
		private readonly ILogger<ProductCatalogService> _logger;
		private readonly IAdminOpreationServices _adminOpreationServices;
		private readonly ICollectionServices _collectionServices;
		private readonly IErrorNotificationService _errorNotificationService;
		private readonly ICacheManager _cacheManager;
		private const string CACHE_TAG_COLLECTION_With_Product = "CollectionWithProduct";
		
		private const string CACHE_TAG_PRODUCT_SEARCH = "product_search";
		private const string CACHE_TAG_CART = "cart";
		public const string CACHE_TAG_CATEGORY_WITH_DATA = "categorywithdata";
		private static readonly string[] PRODUCT_CACHE_TAGS = new[] { CACHE_TAG_PRODUCT_SEARCH, CACHE_TAG_CATEGORY_WITH_DATA, PRODUCT_WITH_VARIANT_TAG,CACHE_TAG_COLLECTION_With_Product,CACHE_TAG_CART };
		private const string PRODUCT_WITH_VARIANT_TAG = "productwithvariantdata";

		public ProductCatalogService(
			ICartServices cartServices,
			ICollectionServices collectionServices,
			IBackgroundJobClient backgroundJobClient,
			IUnitOfWork unitOfWork,
			ISubCategoryServices subCategoryServices,
			ILogger<ProductCatalogService> logger,
			IAdminOpreationServices adminOpreationServices,
			IErrorNotificationService errorNotificationService,
			ICacheManager cacheManager)
		{
			_cartServices = cartServices;
			_collectionServices= collectionServices;
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

		private void DeactiveCollectionMethod(int productid)
		{
			_backgroundJobClient.Enqueue(() => _collectionServices.CheckAndDeactivateEmptyCollectionsAsync(productid));
		}
		private void RemoveCartItem(string userid,int? ProductVariantId, int ProductId)
		{
			_backgroundJobClient.Enqueue(() => _cartServices.RemoveItemFromCartAsync(userid, new DtoModels.CartDtos.RemoveCartItemDto { ProductId = ProductId, ProductVariantId = ProductVariantId }));
		}
		private void DeActiveSubcategory(int subcategoryid,string userid)
		{
			_backgroundJobClient.Enqueue(() =>  _subCategoryServices.DeactivateSubCategoryIfAllProductsAreInactiveAsync(subcategoryid, userid));
		}

		private static Expression<Func<E_Commerce.Models.Product, ProductDto>> maptoProductDtoexpression = p =>
		 new ProductDto
		 {
			 Id = p.Id,
			 Name = p.Name,
			 IsActive = p.IsActive,
			 AvailableQuantity = p.Quantity,
			 Price = p.Price,
			 Description = p.Description,
			 SubCategoryId = p.SubCategoryId,
			 CreatedAt = p.CreatedAt,
			 DiscountPrecentage = (p.Discount != null && p.Discount.IsActive && (p.Discount.DeletedAt == null) && (  p.Discount.EndDate > DateTime.UtcNow)) ? p.Discount.DiscountPercent : null,
			 FinalPrice = (p.Discount != null && p.Discount.IsActive && (p.Discount.DeletedAt == null) && (p.Discount.EndDate > DateTime.UtcNow)) ? Math.Round(p.Price - (((p.Discount.DiscountPercent) / 100) * p.Price)) : p.Price,
			 DiscountName = (p.Discount != null && p.Discount.IsActive && (p.Discount.DeletedAt == null) && (   p.Discount.EndDate > DateTime.UtcNow)) ? p.Discount.Name : null,
			 EndAt = (p.Discount != null && p.Discount.IsActive && (p.Discount.DeletedAt == null) && (   p.Discount.EndDate > DateTime.UtcNow)) ? p.Discount.EndDate : null,
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
		 };
		private static Expression<Func< E_Commerce.Models.Product, ProductDetailDto>> maptoProductDetailDtoexpression = p =>
		 new ProductDetailDto
		 {
			 Id = p.Id,
			 Name = p.Name,
			 Description = p.Description,
			 AvailableQuantity = p.Quantity,
			 Gender = p.Gender,
			 CreatedAt = p.CreatedAt,
			 DeletedAt= p.DeletedAt,
			 ModifiedAt = p.ModifiedAt,
			 IsActive = p.IsActive,
			 Price = p.Price,
			 FinalPrice = (p.Discount != null && p.Discount.IsActive && (p.Discount.DeletedAt == null) && (p.Discount.EndDate > DateTime.UtcNow)) ? Math.Round(p.Price - (((p.Discount.DiscountPercent) / 100) * p.Price)) : p.Price,

			 fitType = p.fitType,

			 SubCategoryId = p.SubCategoryId,
			 Discount = (p.Discount != null && p.Discount.IsActive && (p.Discount.DeletedAt == null) && (  p.Discount.EndDate > DateTime.UtcNow)) ? new DiscountDto
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
			 }),
			 Variants = p.ProductVariants.Where(v => v.DeletedAt == null &&v.IsActive&& v.Quantity != 0).Select(v => new ProductVariantDto
			 {
				 Id = v.Id,
				 IsActive = v.IsActive,
				 CreatedAt = v.CreatedAt,
				 ModifiedAt = v.ModifiedAt,
				 DeletedAt= v.DeletedAt,
				 Color = v.Color,
				 Size = v.Size,
				 Waist = v.Waist,
				 Length = v.Length,
				 Quantity = v.Quantity,
				 ProductId = v.ProductId
			 })
		 };




		public async Task<Result<ProductDetailDto>> GetProductByIdAsync(int id, bool? isActive, bool? deletedOnly)
		{
			_logger.LogInformation($"Retrieving product by id: {id}, isActive: {isActive}, deletedOnly: {deletedOnly}");
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

				_logger.LogInformation($"Product found: {product.Name} (ID: {product.Id})");

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

		private  void RemoveProductCaches()
		{
			
			BackgroundJob.Enqueue(() => _cacheManager.RemoveByTagsAsync(PRODUCT_CACHE_TAGS));
		}
		private ProductDto Maptoproductdto(E_Commerce.Models. Product p)
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
				FinalPrice = (p.Discount != null && p.Discount.IsActive && (p.Discount.DeletedAt == null) && (p.Discount.EndDate > DateTime.UtcNow)) ? Math.Round(p.Price - (((p.Discount.DiscountPercent) / 100) * p.Price)) : p.Price,

				fitType = p.fitType,
				Gender = p.Gender,
				ModifiedAt = p.ModifiedAt,
				DeletedAt = p.DeletedAt,
			};
			if (p.Discount != null && p.Discount.IsActive && (p.Discount.DeletedAt == null) && ( p.Discount.EndDate > DateTime.UtcNow)){
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
			if (dto == null)
			{
				_logger.LogWarning("CreateProductAsync called with null dto");
				return Result<ProductDto>.Fail("Product data is required.", 400);
			}
			if (string.IsNullOrWhiteSpace(userId))
			{
				_logger.LogWarning("CreateProductAsync called with null/empty userId");
				return Result<ProductDto>.Fail("User ID is required.", 400);
			}
			if (dto.Subcategoryid <= 0)
			{
				_logger.LogWarning("CreateProductAsync called with invalid Subcategoryid: {Subcategoryid}", dto.Subcategoryid);
				return Result<ProductDto>.Fail("Valid subcategory ID is required.", 400);
			}
			if (string.IsNullOrWhiteSpace(dto.Name))
			{
				_logger.LogWarning("CreateProductAsync called with empty product name");
				return Result<ProductDto>.Fail("Product name is required.", 400);
			}
			_logger.LogInformation($"Creating new product: {dto.Name}");
			using var transaction = await _unitOfWork.BeginTransactionAsync();
			try
			{
				// Validate category exists
				var categoryExists = await _unitOfWork.SubCategory.IsExsistAsync(dto.Subcategoryid);
				if (!categoryExists)
				{
					await transaction.RollbackAsync();
					_logger.LogWarning("CreateProductAsync: Subcategory not found: {Subcategoryid}", dto.Subcategoryid);
					return Result<ProductDto>.Fail("Category not found.", 404);
				}
				var exists = await _unitOfWork.Product.IsExsistByNameAsync(dto.Name);
				if (exists)
				{
					await transaction.RollbackAsync();
					_logger.LogWarning("CreateProductAsync: Product with same name exists: {ProductName}", dto.Name);
					return Result<ProductDto>.Fail($"There's already a product with the same name: {dto.Name}", 409);
				}
				var product = new Models.Product
				{
					Name = dto.Name,
					Description = dto.Description,
					SubCategoryId = dto.Subcategoryid,
					Gender = dto.Gender,
					IsActive = false,
					Price = dto.Price,
					fitType = dto.fitType,
				};
				var result = await _unitOfWork.Product.CreateAsync(product);
				if (result == null)
				{
					await transaction.RollbackAsync();
					_logger.LogError("CreateProductAsync: Failed to create product {ProductName}", dto.Name);
					return Result<ProductDto>.Fail("Failed to create product.", 400);
				}
				var adminOpResult = await _adminOpreationServices.AddAdminOpreationAsync(
					$"Create Product {product.Id}",
					E_Commerce.Enums.Opreations.AddOpreation,
					userId,
					product.Id
				);
				if (adminOpResult == null)
				{
					await transaction.RollbackAsync();
					return Result<ProductDto>.Fail("Unexpected error occurred while creating product", 500);
				}
				await _unitOfWork.CommitAsync();
				await transaction.CommitAsync();
				RemoveProductCaches();
				var productdto = Maptoproductdto(product);
				return Result<ProductDto>.Ok(productdto, "Product created successfully.", 201);
			}
			catch (Exception ex)
			{
				await transaction.RollbackAsync();
				_logger.LogError(ex, $"Unexpected error in CreateProductAsync for product {dto?.Name}");
				_backgroundJobClient.Enqueue(() => _errorNotificationService.SendErrorNotificationAsync(ex.Message, ex.StackTrace));
				return Result<ProductDto>.Fail("Unexpected error occurred while creating product.", 500);
			}
		}

		public async Task<Result<ProductDto>> UpdateProductAsync(int id, UpdateProductDto dto, string userId)
		{
			if (id <= 0)
			{
				_logger.LogWarning("UpdateProductAsync called with invalid id: {Id}", id);
				return Result<ProductDto>.Fail("Valid product ID is required.", 400);
			}
			if (dto == null)
			{
				_logger.LogWarning("UpdateProductAsync called with null dto");
				return Result<ProductDto>.Fail("Product update data is required.", 400);
			}
			if (string.IsNullOrWhiteSpace(userId))
			{
				_logger.LogWarning("UpdateProductAsync called with null/empty userId");
				return Result<ProductDto>.Fail("User ID is required.", 400);
			}
			_logger.LogInformation($"Updating product: {id}");
			using var transaction = await _unitOfWork.BeginTransactionAsync();
			try
			{
				var updates = new List<string>();
				var product = await _unitOfWork.Product.GetProductByIdAsync(id, null, false);
				if (product == null || product.DeletedAt != null)
				{
					await transaction.RollbackAsync();
					_logger.LogWarning("UpdateProductAsync: Product not found or deleted: {Id}", id);
					return Result<ProductDto>.Fail("Product not found.", 404);
				}
				if (!string.IsNullOrEmpty(dto.Name))
				{
					updates.Add($"change name from: {product.Name} to {dto.Name}");
					product.Name = dto.Name;
				}
				if (!string.IsNullOrEmpty(dto.Description))
				{
					updates.Add($"change description from: {product.Description} to {dto.Description}");
					product.Description = dto.Description;
				}
				if (dto.SubCategoryid.HasValue)
				{
					var subCatCheck = await _subCategoryServices.IsExsistAsync(dto.SubCategoryid.Value);
					if (!subCatCheck.Success)
					{
						await transaction.RollbackAsync();
						_logger.LogWarning("UpdateProductAsync: SubCategory not found: {SubCategoryId}", dto.SubCategoryid.Value);
						return Result<ProductDto>.Fail("SubCategory not found.", 404);
					}
					updates.Add($"change SubCategory from: {product.SubCategoryId} to {dto.SubCategoryid.Value}");
					product.SubCategoryId = dto.SubCategoryid.Value;
				}
				if (dto.Price.HasValue)
				{
					updates.Add($"change Price from: {product.Price} to {dto.Price.Value}");
					product.Price = dto.Price.Value;
				}
				if (dto.Gender.HasValue)
				{
					updates.Add($"change Gender from: {product.Gender} to {dto.Gender.Value}");
					product.Gender = dto.Gender.Value;
				}
				if (dto.fitType.HasValue)
				{
					updates.Add($"change fitType from: {product.fitType} to {dto.fitType.Value}");
					product.fitType = dto.fitType.Value;
				}
				if (updates.Count == 0)
				{
					await transaction.RollbackAsync();
					_logger.LogWarning("UpdateProductAsync: No updates provided for product: {Id}", id);
					return Result<ProductDto>.Fail("No updates provided.", 400);
				}
				var result = _unitOfWork.Product.Update(product);
				if (!result)
				{
					await transaction.RollbackAsync();
					_logger.LogError("UpdateProductAsync: Failed to update product {Id}", id);
					return Result<ProductDto>.Fail("Failed to update product.", 400);
				}
				var adminOpResult = await _adminOpreationServices.AddAdminOpreationAsync(
					string.Join("; ", updates),
					E_Commerce.Enums.Opreations.UpdateOpreation,
					userId,
					id
				);
				if (adminOpResult == null || !adminOpResult.Success)
				{
					await transaction.RollbackAsync();
					_logger.LogError("UpdateProductAsync: Failed to log admin operation for product {Id}", id);
					return Result<ProductDto>.Fail("Failed to log admin operation. Product update rolled back.", 500);
				}
				RemoveProductCaches();
				await _unitOfWork.CommitAsync();
				await transaction.CommitAsync();
				var productDetailDto = Maptoproductdto(product);
				return Result<ProductDto>.Ok(productDetailDto, "Product updated successfully.", 200);
			}
			catch (Exception ex)
			{
				await transaction.RollbackAsync();
				_logger.LogError(ex, $"Error in UpdateProductAsync for id: {id}");
				_backgroundJobClient.Enqueue(() => _errorNotificationService.SendErrorNotificationAsync(ex.Message, ex.StackTrace));
				return Result<ProductDto>.Fail("Error updating product.", 500);
			}
		}
		public async Task<Result<bool>> DeleteProductAsync(int id, string userId)
		{
			_logger.LogInformation($"Deleting product: {id}");
			var transacrion = await _unitOfWork.BeginTransactionAsync();
			try
			{
				var product = await _unitOfWork.Product.GetByIdAsync(id);
				if (product == null){
					await transacrion.RollbackAsync();
					return Result<bool>.Fail("Product not found", 404);
				}

				product.IsActive = false;
				var result = await _unitOfWork.Product.SoftDeleteAsync(id);
				if (!result){
					await transacrion.RollbackAsync();
					return Result<bool>.Fail("Failed to delete product", 500);
}
				// Log admin operation
				var isadded= await _adminOpreationServices.AddAdminOpreationAsync(
					$"Delete Product {id}",
					E_Commerce.Enums.Opreations.DeleteOpreation,
					userId,
					id
				);

				if(isadded==null||!isadded.Success)
				{
					await transacrion.RollbackAsync();
					return Result<bool>.Fail("Failed to delete product", 500);
				}
				 await _unitOfWork.CommitAsync();
				await transacrion.CommitAsync();
				RemoveProductCaches();
				DeactiveCollectionMethod(id);

				DeActiveSubcategory(product.SubCategoryId, userId);
				RemoveCartItem(userId, null, product.Id);

				return Result<bool>.Ok(true, "Product deleted", 200);
			}
			catch (Exception ex)
			{
				await transacrion.CommitAsync();
				_logger.LogError(ex, $"Error in DeleteProductAsync for id: {id}");
				 	_backgroundJobClient.Enqueue(()=> _errorNotificationService.SendErrorNotificationAsync(ex.Message, ex.StackTrace));
				return Result<bool>.Fail("Error deleting product", 500);
			}
		}

		public async Task<Result<bool>> RestoreProductAsync(int id, string userId)
		{
			_logger.LogInformation($"Restoring product: {id}");
			var transaction = await _unitOfWork.BeginTransactionAsync();
			try
			{
				var restored = await _unitOfWork.Product.RestoreProductAsync(id);
				if (!restored)
					return Result<bool>.Fail("Product not found or not deleted", 404);
	

		
			var isadded=	await _adminOpreationServices.AddAdminOpreationAsync(
					$"Restore Product {id}",
					E_Commerce.Enums.Opreations.UpdateOpreation,
					userId,
					id
				);
				if(isadded==null ||!isadded.Success)
				{
					_logger.LogError("Can't Add Admin Oprearion");
					await transaction.RollbackAsync();
					return Result<bool>.Fail("Error restoring product", 500);

				}
				 RemoveProductCaches();
				await _unitOfWork.CommitAsync();
				await transaction.CommitAsync();
			
				return Result<bool>.Ok(true, "Product restored successfully", 200);
			}
			catch (Exception ex)
			{
				await transaction.RollbackAsync();
				_logger.LogError(ex, $"Error in RestoreProductAsync for id: {id}");
				BackgroundJob.Enqueue(()=> _errorNotificationService.SendErrorNotificationAsync(ex.Message, ex.StackTrace));
				return Result<bool>.Fail("Error restoring product", 500);
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
					.Select(maptoProductDtoexpression)
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
			using var transaction = await _unitOfWork.BeginTransactionAsync();
			try
			{
				var productInfo = await _unitOfWork.Product.GetAll()
					.Where(p => p.Id == productId)
					.Select(p => new {
						IsActive = p.IsActive,
						isdeleted= p.DeletedAt!=null,
						HasImages = p.Images.Any(i => i.DeletedAt == null),
						HasActiveVariants = p.ProductVariants.Any(v => v.IsActive && v.DeletedAt == null)
					}).FirstOrDefaultAsync();

				if (productInfo == null)
					return Result<bool>.Fail("Product not found", 404);

				if (productInfo.IsActive)
					return Result<bool>.Ok(true, "Product already active", 200);

				if (!productInfo.HasImages)
					return Result<bool>.Fail("Product has no images", 400);

				if (!productInfo.HasActiveVariants)
					return Result<bool>.Fail("Product has no active variants", 400);
				if (productInfo.isdeleted)
					return Result<bool>.Fail("Product is Deleted Restore it first", 400);

				var result = await _unitOfWork.Product.ActiveProductAsync(productId);
				if (!result)
					return Result<bool>.Fail("Failed to activate product", 400);

				var isAdded = await _adminOpreationServices.AddAdminOpreationAsync(
					$"Activate Product {productId}",
					E_Commerce.Enums.Opreations.UpdateOpreation,
					userId,
					productId);

				if (isAdded == null)
				{
					await transaction.RollbackAsync();
					return Result<bool>.Fail("Failed to log admin operation", 500);
				}

				await _unitOfWork.CommitAsync();
				await transaction.CommitAsync();

				RemoveProductCaches();

				return Result<bool>.Ok(true, "Product activated successfully", 200);
			}
			catch (Exception ex)
			{
				await transaction.RollbackAsync();
				_logger.LogError(ex, "Error while activating product");
				return Result<bool>.Fail("Unexpected error occurred", 500);
			}
		}

		public async Task<Result<bool>> DeactivateProductAsync(int productId, string userId)
		{
			using var transaction = await _unitOfWork.BeginTransactionAsync();
			try
			{
				var productInfo = await _unitOfWork.Product.GetAll()
					.Where(p => p.Id == productId)
					.Select(p => new {
						IsActive = p.IsActive,
						HasActiveVariants = p.ProductVariants.Any(v => v.IsActive && v.DeletedAt == null),
						SubCategoryId = p.SubCategoryId
					})
					.FirstOrDefaultAsync();

				if (productInfo == null)
					return Result<bool>.Fail("Product not found", 404);

				if (!productInfo.IsActive)
					return Result<bool>.Ok(true, "Product already deactivated", 200);

				if (productInfo.HasActiveVariants)
					return Result<bool>.Fail("Product still has active variants. Please deactivate them first.", 400);

				var result = await _unitOfWork.Product.DeactiveProductAsync(productId);
				if (!result)
					return Result<bool>.Fail("Failed to deactivate product", 400);

				var isAdded = await _adminOpreationServices.AddAdminOpreationAsync(
					$"Deactivate Product {productId}",
					E_Commerce.Enums.Opreations.UpdateOpreation,
					userId,
					productId);

				if (isAdded == null)
				{
					await transaction.RollbackAsync();
					return Result<bool>.Fail("Failed to log admin operation", 500);
				}

				await _unitOfWork.CommitAsync();
				await transaction.CommitAsync();

			
				RemoveProductCaches();
				DeactiveCollectionMethod(productId);
				DeActiveSubcategory(productInfo.SubCategoryId, userId);
				RemoveCartItem(userId, null, productId);

				_logger.LogInformation($"Product {productId} deactivated. Triggered background jobs for collection and subcategory checks.");

				return Result<bool>.Ok(true, "Product deactivated successfully", 200);
			}
			catch (Exception ex)
			{
				await transaction.RollbackAsync();
				_logger.LogError(ex, $"Error while deactivating product {productId}");
				return Result<bool>.Fail("Unexpected error occurred", 500);
			}
		}

		public void UpdateProductQuantity(int productid)
		{

			_unitOfWork.Product.UpdateProductQuntity( productid);
		}

	}
} 