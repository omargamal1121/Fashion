using E_Commers.DtoModels.DiscoutDtos;
using E_Commers.DtoModels.ImagesDtos;
using E_Commers.DtoModels.ProductDtos;
using E_Commers.DtoModels.Responses;
using E_Commers.Enums;
using E_Commers.ErrorHnadling;
using E_Commers.Interfaces;
using E_Commers.Models;
using E_Commers.Services.AdminOpreationServices;
using E_Commers.Services.Cache;
using E_Commers.Services.EmailServices;
using E_Commers.UOW;
using Hangfire;
using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;

namespace E_Commers.Services.ProductServices
{
	public interface IProductDiscountService
	{
		Task<Result<DiscountDto>> GetProductDiscountAsync(int productId);
		Task<Result<ProductDetailDto>> AddDiscountToProductAsync(int productId, int discountId, string userId);
		Task<Result<ProductDetailDto>> UpdateProductDiscountAsync(int productId, int discountId, string userId);
		Task<Result<ProductDetailDto>> RemoveDiscountFromProductAsync(int productId, string userId);
		Task<Result<List<ProductDto>>> GetProductsWithActiveDiscountsAsync();
		Task<Result<bool>> UpdateProductPriceAfteDiscount(int productId);
	}

	public class ProductDiscountService : IProductDiscountService
	{
		private readonly IUnitOfWork _unitOfWork;
		private readonly ILogger<ProductDiscountService> _logger;
		private readonly IBackgroundJobClient _backgroundJobClient;
		private readonly IAdminOpreationServices _adminOpreationServices;
		private readonly IErrorNotificationService _errorNotificationService;
		private readonly ICacheManager _cacheManager;
		private const string CACHE_TAG_PRODUCT_SEARCH = "product_search";
		private const string CACHE_TAG_SUBCATEGORY = "subcategory";
		private static readonly string[] PRODUCT_CACHE_TAGS = new[] { CACHE_TAG_PRODUCT_SEARCH, CACHE_TAG_SUBCATEGORY, PRODUCT_WITH_VARIANT_TAG };
		private const string PRODUCT_WITH_VARIANT_TAG = "productwithvariantdata";


		public ProductDiscountService(
			IBackgroundJobClient backgroundJobClient,
			ICacheManager cacheManager,
			IUnitOfWork unitOfWork,
			ILogger<ProductDiscountService> logger,
			IAdminOpreationServices adminOpreationServices,
			IErrorNotificationService errorNotificationService)
		{ 
			_backgroundJobClient = backgroundJobClient;
			_cacheManager = cacheManager;
			_unitOfWork = unitOfWork;
			_logger = logger;
			_adminOpreationServices = adminOpreationServices;
			_errorNotificationService = errorNotificationService;
		}

		public async Task<Result<DiscountDto>> GetProductDiscountAsync(int productId)
		{
			try
			{
				var productInfo = await _unitOfWork.Product.GetAll()
					.AsNoTracking()
					.Where(p => p.Id == productId)
					.Select(p => new { p.Discount })
					.FirstOrDefaultAsync();

				if (productInfo == null)
					return Result<DiscountDto>.Fail("Product not found", 404);

				if (productInfo.Discount == null)
					return Result<DiscountDto>.Fail("No discount found for this product", 404);
				
				var discount = productInfo.Discount;

				if (!discount.IsActive || discount.DeletedAt != null || (discount.EndDate != null && discount.EndDate <= DateTime.UtcNow))
				{
					return Result<DiscountDto>.Fail("No active discount found for this product", 404);
				}

				var discountDto = new DiscountDto
				{
					Id = discount.Id,
					Name = discount.Name,
					Description = discount.Description,
					DiscountPercent = discount.DiscountPercent,
					StartDate = discount.StartDate,
					EndDate = discount.EndDate,
					IsActive = discount.IsActive,
					CreatedAt = discount.CreatedAt,
					ModifiedAt = discount.ModifiedAt,
					DeletedAt = discount.DeletedAt,
				};

				return Result<DiscountDto>.Ok(discountDto, "Product discount retrieved successfully", 200);
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, $"Error in GetProductDiscountAsync for productId: {productId}");
				_backgroundJobClient.Enqueue(() => _errorNotificationService.SendErrorNotificationAsync(ex.Message, ex.StackTrace));
				return Result<DiscountDto>.Fail("Error retrieving product discount", 500);
			}
		}

		private void RemoveProductCachesAsync()
		{
			_backgroundJobClient.Enqueue(() => _cacheManager.RemoveByTagsAsync(PRODUCT_CACHE_TAGS));
		}

		private static Expression<Func<E_Commers.Models.Product, ProductDetailDto>> maptoProductDetailDtoexpression = p =>
		 new ProductDetailDto
		 {
			 Id = p.Id,
			 Name = p.Name,
			 Description = p.Description,
			 AvailableQuantity = p.Quantity,
			 Gender = p.Gender,
			 SubCategoryId = p.SubCategoryId,
			 Discount = (p.Discount != null && p.Discount.IsActive && p.Discount.DeletedAt == null && (p.Discount.EndDate == null || p.Discount.EndDate > DateTime.UtcNow)) ? new DiscountDto
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



		public async Task<Result<ProductDetailDto>> AddDiscountToProductAsync(int productId, int discountId, string userId)
		{
			_logger.LogInformation($"Adding discount to product: {productId} with discount: {discountId}");
			try
			{
				var product = await _unitOfWork.Product.GetByIdAsync(productId);
				if (product == null)
					return Result<ProductDetailDto>.Fail("Product not found", 404);

				var discount = await _unitOfWork.Repository<E_Commers.Models.Discount>().GetByIdAsync(discountId);
				if (discount == null||discount.DeletedAt!=null)
					return Result<ProductDetailDto>.Fail("Discount not found", 404);

				using var transaction = await _unitOfWork.BeginTransactionAsync();

				product.DiscountId = discount.Id;
				var productResult = _unitOfWork.Product.Update(product);
				if (!productResult)
				{
					await transaction.RollbackAsync();
					return Result<ProductDetailDto>.Fail("Failed to assign discount to product", 400);
				}

				await _adminOpreationServices.AddAdminOpreationAsync(
					$"Add Discount to Product {productId}",
					Opreations.AddOpreation,
					userId,
					productId
				);

				await _unitOfWork.CommitAsync();
				await UpdateProductPriceAfteDiscount(productId);
				RemoveProductCachesAsync();

				var updatedProduct = await _unitOfWork.Product.GetAll()
					.Where(p => p.Id == productId)
					.Select(maptoProductDetailDtoexpression)
					
					.FirstOrDefaultAsync();

				return Result<ProductDetailDto>.Ok(updatedProduct, "Discount added successfully", 201);
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, $"Error in AddDiscountToProductAsync for productId: {productId}, discountId: {discountId}");
			 	_backgroundJobClient.Enqueue(()=> _errorNotificationService.SendErrorNotificationAsync(ex.Message, ex.StackTrace));
				return Result<ProductDetailDto>.Fail("Error adding discount", 500);
			}
		}

		public async Task<Result<ProductDetailDto>> UpdateProductDiscountAsync(int productId, int discountId, string userId)
		{
			_logger.LogInformation($"Updating discount for product: {productId} with discount: {discountId}");
			try
			{
				var product = await _unitOfWork.Product.GetAll()
					.Where(p => p.Id == productId)
					.FirstOrDefaultAsync();

				if (product == null)
					return Result<ProductDetailDto>.Fail("Product not found", 404);

				var discount = await _unitOfWork.Repository<E_Commers.Models.Discount>().GetByIdAsync(discountId);
				if (discount == null||discount.DeletedAt!=null)
					return Result<ProductDetailDto>.Fail("Discount not found", 404);

				using var transaction = await _unitOfWork.BeginTransactionAsync();

				if (product.DiscountId != discountId)
					product.DiscountId = discountId;

				var productResult = _unitOfWork.Product.Update(product);
				if (!productResult)
				{
					await transaction.RollbackAsync();
					return Result<ProductDetailDto>.Fail("Failed to update product discount", 400);
				}

				await _adminOpreationServices.AddAdminOpreationAsync(
					$"Update Discount for Product {productId}",
					Opreations.UpdateOpreation,
					userId,
					productId
				);

				await _unitOfWork.CommitAsync();
				await UpdateProductPriceAfteDiscount(productId);
				RemoveProductCachesAsync();

				// Retrieve updated product
				var updatedProduct = await _unitOfWork.Product.GetAll()
					.Where(p => p.Id == productId)
					.Select(maptoProductDetailDtoexpression)
					.FirstOrDefaultAsync();

				return Result<ProductDetailDto>.Ok(updatedProduct, "Discount updated successfully", 200);
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, $"Error in UpdateProductDiscountAsync for productId: {productId}, discountId: {discountId}");
			 	_backgroundJobClient.Enqueue(()=> _errorNotificationService.SendErrorNotificationAsync(ex.Message, ex.StackTrace));
				return Result<ProductDetailDto>.Fail("Error updating discount", 500);
			}
		}

		public async Task<Result<ProductDetailDto>> RemoveDiscountFromProductAsync(int productId, string userId)
		{
			_logger.LogInformation($"Removing discount from product: {productId}");
			try
			{
				var product = await _unitOfWork.Product.GetAll()
					.Where(p => p.Id == productId)
					.FirstOrDefaultAsync();

				if (product == null)
					return Result<ProductDetailDto>.Fail("Product not found", 404);

				if (product.DiscountId == null)
					return Result<ProductDetailDto>.Fail("Product has no discount to remove", 404);

				using var transaction = await _unitOfWork.BeginTransactionAsync();

				product.DiscountId = null;
				var productResult = _unitOfWork.Product.Update(product);
				if (!productResult)
				{
					await transaction.RollbackAsync();
					return Result<ProductDetailDto>.Fail("Failed to remove discount from product", 400);
				}

				await _adminOpreationServices.AddAdminOpreationAsync(
					$"Remove Discount from Product {productId}",
					Opreations.DeleteOpreation,
					userId,
					productId
				);

				await _unitOfWork.CommitAsync();
				await UpdateProductPriceAfteDiscount(productId);
				RemoveProductCachesAsync();

				// Retrieve updated product
				var updatedProduct = await _unitOfWork.Product.GetAll()
					.Where(p => p.Id == productId)
					.Select(maptoProductDetailDtoexpression)
					.FirstOrDefaultAsync();

				return Result<ProductDetailDto>.Ok(updatedProduct, "Discount removed successfully", 200);
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, $"Error in RemoveDiscountFromProductAsync for productId: {productId}");
			 	_backgroundJobClient.Enqueue(()=> _errorNotificationService.SendErrorNotificationAsync(ex.Message, ex.StackTrace));
				return Result<ProductDetailDto>.Fail("Error removing discount", 500);
			}
		}


		public async Task<Result<List<ProductDto>>> GetProductsWithActiveDiscountsAsync()
		{
			try
			{
				var now = DateTime.UtcNow;

				var products = await _unitOfWork.Product.GetAll()
					.Where(p => p.Discount != null
						&& p.Discount.IsActive
						&& p.Discount.DeletedAt == null
						&& p.Discount.StartDate <= now
						&& p.Discount.EndDate > now)
					.Select(p => new ProductDto
					{
						Id = p.Id,
						Name = p.Name,
						Description = p.Description,
						AvailableQuantity = p.Quantity,
						Gender = p.Gender,
						SubCategoryId = p.SubCategoryId,
						Price = p.Price,
						FinalPrice = p.Discount != null && p.Discount.IsActive ? p.Price - (p.Price * (p.Discount.DiscountPercent / 100m)) : p.Price,
						DiscountPrecentage = p.Discount.DiscountPercent,
						DiscountName = p.Discount.Name,
						EndAt = p.Discount.EndDate,
						IsActive = p.IsActive,
						CreatedAt = p.CreatedAt,
						ModifiedAt = p.ModifiedAt,
						DeletedAt = p.DeletedAt,
						fitType = p.fitType,
						images = p.Images
							.Where(i => i.DeletedAt == null)
							.Select(i => new ImageDto
							{
								Id = i.Id,
								Url = i.Url,
								IsMain = i.IsMain
							}).ToList()
					})
					.ToListAsync();

				if (!products.Any())
					return Result<List<ProductDto>>.Fail("No products with active discounts found", 404);

				return Result<List<ProductDto>>.Ok(products, "Products with active discounts retrieved successfully", 200);
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error in GetProductsWithActiveDiscountsAsync");
			 	_backgroundJobClient.Enqueue(()=> _errorNotificationService.SendErrorNotificationAsync(ex.Message, ex.StackTrace));
				return Result<List<ProductDto>>.Fail("Error retrieving products with active discounts", 500);
			}
		}


		public async Task<Result<bool>> UpdateProductPriceAfteDiscount(int productId)
		{
			try
			{
				var product = await _unitOfWork.Product.GetAll()
					.Where(p => p.Id == productId)
					.Include(p => p.Discount)
					.FirstOrDefaultAsync();

				if (product == null)
					return Result<bool>.Fail("Product not found", 404);

				var originalPrice = product.Price;
				var discountedPrice = originalPrice;

				// Check if discount is active and valid
				if (product.Discount != null && 
					product.Discount.IsActive && 
					product.Discount.DeletedAt == null &&
					product.Discount.StartDate <= DateTime.UtcNow &&
					product.Discount.EndDate > DateTime.UtcNow)
				{
					var discountAmount = originalPrice * (product.Discount.DiscountPercent / 100m);
					discountedPrice = originalPrice - discountAmount;
					product.FinalPrice = discountedPrice;
				}
				else
				{
					product.FinalPrice = originalPrice; 
				}
				var updateResult = _unitOfWork.Product.Update(product);
				if (!updateResult)
				{
					return Result<bool>.Fail("Failed to update product price after discount", 400);
				}await _unitOfWork.CommitAsync();
				return Result<bool>.Ok(true, "Discounted price calculated successfully", 200);
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, $"Error in CalculateDiscountedPriceAsync for productId: {productId}");
				_backgroundJobClient.Enqueue(()=> _errorNotificationService.SendErrorNotificationAsync(ex.Message, ex.StackTrace));
				return Result<bool>.Fail("Error calculating discounted price", 500);
			}
		}
	}
} 