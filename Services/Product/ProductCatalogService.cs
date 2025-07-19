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

namespace E_Commers.Services.Product
{
	public interface IProductCatalogService
	{
		
		Task<Result<ProductDetailDto>> GetProductByIdAsync(int id, bool? isActive, bool? deletedOnly);
		Task<Result<ProductListItemDto>> CreateProductAsync(CreateProductDto dto, string userId);
		Task<Result<ProductListItemDto>> UpdateProductAsync(int id, UpdateProductDto dto, string userId);
		Task<Result<string>> DeleteProductAsync(int id, string userId);
		Task<Result<ProductListItemDto>> RestoreProductAsync(int id, string userId);
		Task<Result<List<ProductListItemDto>>> GetProductsBySubCategoryId(int SubCategoryid);
	}

	public class ProductCatalogService : IProductCatalogService
	{
		private readonly IUnitOfWork _unitOfWork;
		private readonly ISubCategoryServices _subCategoryServices;
		private readonly ILogger<ProductCatalogService> _logger;
		private readonly IAdminOpreationServices _adminOpreationServices;
		private readonly IErrorNotificationService _errorNotificationService;
		private readonly ICacheManager _cacheManager;
		private const string CACHE_TAG_PRODUCT_SEARCH = "product_search";
		private const string CACHE_TAG_SUBCATEGORY = "subcategory";
		private static readonly string[] PRODUCT_CACHE_TAGS = new[] { CACHE_TAG_PRODUCT_SEARCH, CACHE_TAG_SUBCATEGORY };

		public ProductCatalogService(
			IUnitOfWork unitOfWork,
			 ISubCategoryServices subCategoryServices,
			ILogger<ProductCatalogService> logger,
			IAdminOpreationServices adminOpreationServices,
			IErrorNotificationService errorNotificationService,
			ICacheManager cacheManager)
		{
			_unitOfWork = unitOfWork;
			_subCategoryServices = subCategoryServices;
			_logger = logger;
			_adminOpreationServices = adminOpreationServices;
			_errorNotificationService = errorNotificationService;
			_cacheManager = cacheManager;
		}
		public async Task<Result<ProductDetailDto>> GetProductByIdAsync(int id, bool? isActive, bool? deletedOnly)
		{
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
					.Select(p => new ProductDetailDto
					{
						Id = p.Id,
						Name = p.Name,
						Description = p.Description,
						AvailableQuantity = p.Quantity,
						Gender = p.Gender,
						SubCategoryId = p.SubCategoryId,
						Discount = p.Discount != null ? new DiscountDto 
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
						Variants = p.ProductVariants.Where(v => v.DeletedAt == null&&v.Quantity!=0).Select(v => new ProductVariantDto 
						{ 
							Id = v.Id, 

							Color = v.Color, 
							Size = v.Size, 
							Waist = v.Waist,
							Length = v.Length,
							FitType = v.FitType,
							Quantity = v.Quantity,
							ProductId = v.ProductId
						}).ToList()
					})
					.FirstOrDefaultAsync();

				if (product == null)
					return Result<ProductDetailDto>.Fail("Product not found", 404);

				return Result<ProductDetailDto>.Ok(product, "Product retrieved successfully", 200);
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, $"Error in GetProductByIdAsync for id: {id}");
				await _errorNotificationService.SendErrorNotificationAsync(ex.Message, ex.StackTrace);
				return Result<ProductDetailDto>.Fail("Error retrieving product", 500);
			}
		}

		private async Task SaveAndRemoveProductCacheAsync()
		{
			await _unitOfWork.CommitAsync();
			BackgroundJob.Enqueue(() => _cacheManager.RemoveByTagsAsync(PRODUCT_CACHE_TAGS));
		}

		public async Task<Result<ProductListItemDto>> CreateProductAsync(CreateProductDto dto, string userId)
		{
			_logger.LogInformation($"Creating new product: {dto.Name}");
			try
			{
				// Validate category exists
				var categoryExists = await _subCategoryServices.IsExsistAsync(dto.Subcategoryid);
				if (!categoryExists.Success)
					return Result<ProductListItemDto>.Fail("Category not found", 404);

				// Validate variants have prices

				using var transaction = await _unitOfWork.BeginTransactionAsync();
				
				// Create product
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
					return Result<ProductListItemDto>.Fail("Failed to create product", 400);
				}

				// Log admin operation
				await _adminOpreationServices.AddAdminOpreationAsync(
					$"Create Product {product.Id}",
					E_Commers.Enums.Opreations.AddOpreation,
					userId,
					product.Id
				);
				await SaveAndRemoveProductCacheAsync();
				var productDetailDto = await GetProductByIdAsync(product.Id, false, false);
				var productListItemDto = ProductListItemDto.FromDetail(productDetailDto.Data);
				return Result<ProductListItemDto>.Ok(productListItemDto, "Product created successfully", 201);
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, $"Unexpected error in CreateProductAsync for product {dto.Name}");
				await _errorNotificationService.SendErrorNotificationAsync(ex.Message, ex.StackTrace);
				return Result<ProductListItemDto>.Fail("Unexpected error occurred while creating product", 500);
			}
		}

		public async Task<Result<ProductListItemDto>> UpdateProductAsync(int id, UpdateProductDto dto, string userId)
		{
			_logger.LogInformation($"Updating product: {id}");

			try
			{
				var product = await _unitOfWork.Product.GetAll()
					.Where(p => p.Id == id && p.DeletedAt == null)
					.FirstOrDefaultAsync();

				if (product == null)
					return Result<ProductListItemDto>.Fail("Product not found", 404);

				// Update fields
				if (!string.IsNullOrEmpty(dto.Name))
					product.Name = dto.Name;

				if (!string.IsNullOrEmpty(dto.Description))
					product.Description = dto.Description;

				if (dto.SubCategoryid	.HasValue)
				{
					var subCatCheck = await _subCategoryServices.IsExsistAsync(dto.SubCategoryid.Value);
					if (!subCatCheck.Success)
						return Result<ProductListItemDto>.Fail("SubCategory not found", 404);

					product.SubCategoryId = dto.SubCategoryid.Value;
				}

				// (اختياري) تحديث خصائص إضافية
				if (dto.Price.HasValue)
					product.Price = dto.Price.Value;

				if (dto.Gender.HasValue)
					product.Gender = dto.Gender.Value;

				if (dto.fitType.HasValue)
					product.fitType = dto.fitType.Value;

				var result = _unitOfWork.Product.Update(product);
				if (!result)
					return Result<ProductListItemDto>.Fail("Failed to update product", 400);

				await _adminOpreationServices.AddAdminOpreationAsync(
					$"Update Product {id}",
					E_Commers.Enums.Opreations.UpdateOpreation,
					userId,
					id
				);
				await SaveAndRemoveProductCacheAsync();
				
				var productDetailDto = await GetProductByIdAsync(id, null, null);
				if (!productDetailDto.Success)
					return Result<ProductListItemDto>.Fail("Failed to retrieve updated product", 500);
				var productListItemDto = ProductListItemDto.FromDetail(productDetailDto.Data);
				return Result<ProductListItemDto>.Ok(productListItemDto, "Product updated successfully", 200);
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, $"Error in UpdateProductAsync for id: {id}");
				await _errorNotificationService.SendErrorNotificationAsync(ex.Message, ex.StackTrace);
				return Result<ProductListItemDto>.Fail("Error updating product", 500);
			}
		}
		public async Task<Result<string>> DeleteProductAsync(int id, string userId)
		{
			_logger.LogInformation($"Deleting product: {id}");
			try
			{
				var product = await _unitOfWork.Product.GetByIdAsync(id);
				if (product == null)
					return Result<string>.Fail("Product not found", 404);

				product.IsActive = false;
				var result = await _unitOfWork.Product.SoftDeleteAsync(id);
				if (!result)
					return Result<string>.Fail("Failed to delete product", 400);

				// Log admin operation
				await _adminOpreationServices.AddAdminOpreationAsync(
					$"Delete Product {id}",
					E_Commers.Enums.Opreations.DeleteOpreation,
					userId,
					id
				);
				await SaveAndRemoveProductCacheAsync();
				return Result<string>.Ok("Product deleted successfully", "Product deleted", 200);
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, $"Error in DeleteProductAsync for id: {id}");
				await _errorNotificationService.SendErrorNotificationAsync(ex.Message, ex.StackTrace);
				return Result<string>.Fail("Error deleting product", 500);
			}
		}

		public async Task<Result<ProductListItemDto>> RestoreProductAsync(int id, string userId)
		{
			_logger.LogInformation($"Restoring product: {id}");
			try
			{
				var product = await _unitOfWork.Product.GetAll().FirstOrDefaultAsync(p=>p.Id==id);
				if (product == null)
					return Result<ProductListItemDto>.Fail("Product not found", 404);

				product.DeletedAt = null;
				var result = _unitOfWork.Product.Update(product);
				if (!result)
					return Result<ProductListItemDto>.Fail("Failed to restore product", 400);

				// Log admin operation
				await _adminOpreationServices.AddAdminOpreationAsync(
					$"Restore Product {id}",
					E_Commers.Enums.Opreations.UpdateOpreation,
					userId,
					id
				);
				await SaveAndRemoveProductCacheAsync();
				var productDetailDto = await GetProductByIdAsync(id, true, false);
				var productListItemDto = ProductListItemDto.FromDetail(productDetailDto.Data);
				return Result<ProductListItemDto>.Ok(productListItemDto, "Product restored successfully", 200);
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, $"Error in RestoreProductAsync for id: {id}");
				await _errorNotificationService.SendErrorNotificationAsync(ex.Message, ex.StackTrace);
				return Result<ProductListItemDto>.Fail("Error restoring product", 500);
			}
		}

		public async Task<Result<List<ProductListItemDto>>> GetProductsBySubCategoryId(int SubCategoryid)
		{
			try
			{
				var isfound = await _subCategoryServices.IsExsistAsync(SubCategoryid);
				if (!isfound.Success)
					return Result<List<ProductListItemDto>>.Fail($"No Category with this id:{SubCategoryid}", 404);

				var productsQuery = _unitOfWork.Product.GetAll().AsNoTracking().Where(p=>p.SubCategoryId== SubCategoryid);
				if (productsQuery == null)
					return Result<List<ProductListItemDto>>.Fail("No Products Found", 404);

				var products = await productsQuery
					.Select(p => new ProductListItemDto
					{
						Id = p.Id,
						Name = p.Name,
						Description = p.Description,
						AvailableQuantity = p.Quantity,
						Gender = p.Gender,
						
						Discount = p.Discount != null ? new DiscountDto { Id = p.Discount.Id, DiscountPercent = p.Discount.DiscountPercent, IsActive = p.Discount.IsActive, StartDate = p.Discount.StartDate, EndDate = p.Discount.EndDate, Name = p.Discount.Name, Description = p.Discount.Description } : null,
						Images = p.Images.Where(i => i.DeletedAt == null).Select(i => new ImageDto { Id = i.Id, Url = i.Url }).ToList()
					})
					
					.ToListAsync();

				return Result<List<ProductListItemDto>>.Ok(products, "Products by Category", 200);
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, $"Error in GetProductsByCategoryId for categoryId: {SubCategoryid}");
				await _errorNotificationService.SendErrorNotificationAsync(ex.Message, ex.StackTrace);
				return Result<List<ProductListItemDto>>.Fail("Error retrieving products by category", 500);
			}
		}

		// Updates the product's quantity based on the sum of all non-deleted variant quantities
		private void UpdateProductQuantity(Models.Product product)
		{
			if (product.ProductVariants != null)
			{
				product.Quantity = product.ProductVariants
					.Where(v => v.DeletedAt == null)
					.Sum(v => v.Quantity);
			}
		}

		// Updates the product's FinalPrice based on the discount, if present
		private void UpdateProductPriceAfterDiscount(Models.Product product)
		{
			if (product.Discount != null && product.Discount.IsActive && product.Discount.DiscountPercent > 0)
			{
				product.FinalPrice = product.Price - (product.Price * (product.Discount.DiscountPercent / 100m));
			}
			else
			{
				product.FinalPrice = product.Price;
			}
		}
	}
} 