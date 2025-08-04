using E_Commerce.DtoModels.DiscoutDtos;
using E_Commerce.DtoModels.ImagesDtos;
using E_Commerce.DtoModels.ProductDtos;
using E_Commerce.DtoModels.Responses;
using E_Commerce.Enums;
using E_Commerce.ErrorHnadling;
using E_Commerce.Interfaces;
using E_Commerce.Models;
using E_Commerce.Services.AdminOpreationServices;
using E_Commerce.Services.Cache;
using E_Commerce.Services.EmailServices;
using E_Commerce.UOW;
using Hangfire;
using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;

namespace E_Commerce.Services.ProductServices
{
	public interface IProductDiscountService
	{
		Task<Result<DiscountDto>> GetProductDiscountAsync(int productId);
		Task<Result<bool>> AddDiscountToProductAsync(int productId, int discountId, string userId);
		Task<Result<bool>> UpdateProductDiscountAsync(int productId, int discountId, string userId);
		Task<Result<bool>> RemoveDiscountFromProductAsync(int productId, string userId);
		Task<Result<List<ProductDto>>> GetProductsWithActiveDiscountsAsync();
		Task<Result<List<ProductDto>>> ApplyDiscountToProductsAsync(ApplyDiscountToProductsDto dto, string userId);
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
		private const string CACHE_TAG_CART = "cart";
		private const string CACHE_TAG_SUBCATEGORY = "subcategory";
		private static readonly string[] PRODUCT_CACHE_TAGS = new[] { CACHE_TAG_PRODUCT_SEARCH, CACHE_TAG_SUBCATEGORY, PRODUCT_WITH_VARIANT_TAG ,CACHE_TAG_CART};
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

		public async Task<Result<List<ProductDto>>> ApplyDiscountToProductsAsync(ApplyDiscountToProductsDto dto, string userId)
        {
   
            if (dto == null)
                return Result<List<ProductDto>>.Fail("DTO cannot be null.", 400);
            
            if (dto.Discountid <= 0)
                return Result<List<ProductDto>>.Fail("Invalid discount ID.", 400);
            
            if (dto.ProductsId == null || !dto.ProductsId.Any())
                return Result<List<ProductDto>>.Fail("Product IDs cannot be null or empty.", 400);
            
            if (string.IsNullOrWhiteSpace(userId))
                return Result<List<ProductDto>>.Fail("User ID cannot be null or empty.", 400);

            using var transaction = await _unitOfWork.BeginTransactionAsync();
            try
            {
                var discountExists = await _unitOfWork.Repository<Models.Discount>().IsExsistAsync(dto.Discountid);
                if (!discountExists)
                    return Result<List<ProductDto>>.Fail("Discount not found or deleted.", 404);
               

                var warnings = await _unitOfWork.Product.AddDiscountToProductsAsync(dto.ProductsId, dto.Discountid);

                await _unitOfWork.CommitAsync();

				var adminOpResult = await _adminOpreationServices.AddAdminOpreationAsync(
					 $"Add Discount to Product {string.Join(",", dto.ProductsId)}",
					 Opreations.AddOpreation,
					 userId,
					 dto.ProductsId.FirstOrDefault()
				);
				if (adminOpResult == null || !adminOpResult.Success)
                {
                    await transaction.RollbackAsync();
                    return Result<List<ProductDto>>.Fail("Failed to log admin operation. Discount application rolled back.", 500);
                }

                await _unitOfWork.CommitAsync();
                await transaction.CommitAsync();

                var updatedProducts = await _unitOfWork.Product
                    .GetAll()
                    .Where(p => dto.ProductsId.Contains(p.Id))
                    .Select(maptoProductDtoexpression)
                    .ToListAsync();

                return Result<List<ProductDto>>.Ok(updatedProducts, warnings: warnings);
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Error in ApplyDiscountToProductsAsync");
                _backgroundJobClient.Enqueue(() => _errorNotificationService.SendErrorNotificationAsync(ex.Message, ex.StackTrace));
                return Result<List<ProductDto>>.Fail("Error applying discount to products.", 500);
            }
        }


		public async Task<Result<DiscountDto>> GetProductDiscountAsync(int productId)
		{
			try
			{

				if(! await _unitOfWork.Product.IsExsistAndHasDiscountAsync(productId))
				{
					  return Result<DiscountDto>.Fail("Product not found Or No discount found for this product", 404);
				}

				var discount = await _unitOfWork.Product.GetDiscountofProduct(productId);
				if(discount==null)
					return Result<DiscountDto>.Fail("Product not found Or No discount found for this product", 404);
			







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

		private static Expression<Func<Models.Product, ProductDetailDto>> maptoProductDetailDtoexpression = p =>
		 new ProductDetailDto
		 {
			 Id = p.Id,
			 Name = p.Name,
			 Description = p.Description,
			 AvailableQuantity = p.Quantity,
			 Gender = p.Gender,
			 CreatedAt = p.CreatedAt,
			 DeletedAt = p.DeletedAt,
			 ModifiedAt = p.ModifiedAt,
			 fitType = p.fitType,
			 IsActive= p.IsActive,
			 FinalPrice = (p.Discount != null && p.Discount.IsActive && (p.Discount.DeletedAt == null) && (p.Discount.EndDate > DateTime.UtcNow)) ? Math.Round(p.Price - (((p.Discount.DiscountPercent) / 100) * p.Price)) : p.Price,

			 Price = p.Price,
			 SubCategoryId = p.SubCategoryId,
			 Discount = p.Discount != null  && p.Discount.DeletedAt == null && p.Discount.EndDate > DateTime.UtcNow ? new DiscountDto
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



		public async Task<Result<bool>> AddDiscountToProductAsync(int productId, int discountId, string userId)
		{
			_logger.LogInformation($"Adding discount to product: {productId} with discount: {discountId}");
				using var transaction = await _unitOfWork.BeginTransactionAsync();
			try
			{
				var product = await _unitOfWork.Product.IsExsistAsync(productId);
				if (!product )
					return Result<bool>.Fail("Product not found", 404);

				var discount = await _unitOfWork.Repository<Models.Discount>().IsExsistAsync(discountId);
				if (!discount)
					return Result<bool>.Fail("Discount not found", 404);


				if( !await	 _unitOfWork.Product.AddDiscountToProductAsync(productId, discountId))
				{
					return Result<bool>.Fail("Product not found", 404);
				}
				
			

                var adminOpResult = await _adminOpreationServices.AddAdminOpreationAsync(
                    $"Add Discount to Product {productId} , Discount id: {discountId}",
                    Opreations.AddOpreation,
                    userId,
                    productId
                );
                if (adminOpResult == null || !adminOpResult.Success)
                {
                    await transaction.RollbackAsync();
                    return Result<bool>.Fail(" log admin operation. Discount application rolled back.", 500);
                }

                await _unitOfWork.CommitAsync();
                await transaction.CommitAsync();
                RemoveProductCachesAsync();

       

                return Result<bool>.Ok(true, "Discount added successfully", 201);
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, $"Error in AddDiscountToProductAsync for productId: {productId}, discountId: {discountId}");
			 	_backgroundJobClient.Enqueue(()=> _errorNotificationService.SendErrorNotificationAsync(ex.Message, ex.StackTrace));
				return Result<bool>.Fail("Error adding discount", 500);
			}
		}

		public async Task<Result<bool>> UpdateProductDiscountAsync(int productId, int discountId, string userId)
		{
			_logger.LogInformation($"[UpdateProductDiscountAsync] Called with productId={productId}, discountId={discountId}, userId={userId}");
			// Input validation
			if (productId <= 0)
			{
				_logger.LogWarning("[UpdateProductDiscountAsync] Invalid product ID: {productId}", productId);
				return Result<bool>.Fail("Invalid product ID.", 400);
			}
			if (discountId <= 0)
			{
				_logger.LogWarning("[UpdateProductDiscountAsync] Invalid discount ID: {discountId}", discountId);
				return Result<bool>.Fail("Invalid discount ID.", 400);
			}
			if (string.IsNullOrWhiteSpace(userId))
			{
				_logger.LogWarning("[UpdateProductDiscountAsync] User ID is null or empty.");
				return Result<bool>.Fail("User ID cannot be null or empty.", 400);
			}

			try
			{
				
				if (await _unitOfWork.Product.IsExsistAsync(productId))
				{
					_logger.LogWarning($"[UpdateProductDiscountAsync] Product not found: {productId}");
					return Result<bool>.Fail("Product not found", 404);
				}
			
				if (await _unitOfWork.Repository<Models.Discount>().IsExsistAsync(discountId))
				{
					_logger.LogWarning($"[UpdateProductDiscountAsync] Discount not found or deleted: {discountId}");
					return Result<bool>.Fail("Discount not found", 404);
				}

				using var transaction = await _unitOfWork.BeginTransactionAsync();
				
					if(!await _unitOfWork.Product.AddDiscountToProductAsync(productId,discountId))
					{
						return Result<bool>.Fail("Error updating discount", 500);

					}

			

				var adminOpResult = await _adminOpreationServices.AddAdminOpreationAsync(
					$"Update Discount for Product {productId}",
					Opreations.UpdateOpreation,
					userId,
					productId
				);
				if (adminOpResult == null || !adminOpResult.Success)
				{
					await transaction.RollbackAsync();
					_logger.LogWarning($"[UpdateProductDiscountAsync] Failed to log admin operation for productId={productId}");
					return Result<bool>.Fail("Failed to log admin operation. Discount update rolled back.", 500);
				}

				await _unitOfWork.CommitAsync();
				await transaction.CommitAsync();
				RemoveProductCachesAsync();

				_logger.LogInformation($"[UpdateProductDiscountAsync] Discount updated successfully for productId={productId}");
				return Result<bool>.Ok(true, "Discount updated successfully", 200);
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, $"Error in UpdateProductDiscountAsync for productId: {productId}, discountId: {discountId}");
			 	_backgroundJobClient.Enqueue(()=> _errorNotificationService.SendErrorNotificationAsync(ex.Message, ex.StackTrace));
				return Result<bool>.Fail("Error updating discount", 500);
			}
		}

		public async Task<Result<bool>> RemoveDiscountFromProductAsync(int productId, string userId)
		{
			_logger.LogInformation($"[RemoveDiscountFromProductAsync] Called with productId={productId}, userId={userId}");
			// Input validation
			if (productId <= 0)
			{
				_logger.LogWarning("[RemoveDiscountFromProductAsync] Invalid product ID: {productId}", productId);
				return Result<bool>.Fail("Invalid product ID.", 400);
			}
			if (string.IsNullOrWhiteSpace(userId))
			{
				_logger.LogWarning("[RemoveDiscountFromProductAsync] User ID is null or empty.");
				return Result<bool>.Fail("User ID cannot be null or empty.", 400);
			}

			try
			{
				var productExists = await _unitOfWork.Product.IsExsistAndHasDiscountAsync(productId);
				if (!productExists)
				{
					_logger.LogWarning($"[RemoveDiscountFromProductAsync] Product not found or has no discount to remove: {productId}");
					return Result<bool>.Fail("Product not found Or Product has no discount to remove", 404);
				}

				using var transaction = await _unitOfWork.BeginTransactionAsync();
				if (!await _unitOfWork.Product.RemoveDiscountFromProduct(productId))
				{
					await transaction.RollbackAsync();
					_logger.LogWarning($"[RemoveDiscountFromProductAsync] Failed to remove discount for productId={productId}");
					return Result<bool>.Fail("Product not found Or Product has no discount to remove", 404);
				}

				var adminOpResult = await _adminOpreationServices.AddAdminOpreationAsync(
					$"Remove Discount from Product {productId}",
					Opreations.DeleteOpreation,
					userId,
					productId
				);
				if (adminOpResult == null || !adminOpResult.Success)
				{
					await transaction.RollbackAsync();
					_logger.LogWarning($"[RemoveDiscountFromProductAsync] Failed to log admin operation for productId={productId}");
					return Result<bool>.Fail("Failed to log admin operation. Discount removal rolled back.", 500);
				}

				await _unitOfWork.CommitAsync();
				await transaction.CommitAsync();
				RemoveProductCachesAsync();

				_logger.LogInformation($"[RemoveDiscountFromProductAsync] Discount removed successfully for productId={productId}");
				return Result<bool>.Ok(true, "Discount removed successfully", 200);
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, $"Error in RemoveDiscountFromProductAsync for productId: {productId}");
			 	_backgroundJobClient.Enqueue(()=> _errorNotificationService.SendErrorNotificationAsync(ex.Message, ex.StackTrace));
				return Result<bool>.Fail("Error removing discount", 500);
			}
		}

		private static Expression<Func<Models.Product, ProductDto>> maptoProductDtoexpression =
			p => new ProductDto
			{
				Id = p.Id,
				Name = p.Name,
				Description = p.Description,
				AvailableQuantity = p.Quantity,
				Gender = p.Gender,
				SubCategoryId = p.SubCategoryId,
				Price = p.Price,
				FinalPrice = (p.Discount != null && p.Discount.IsActive && (p.Discount.DeletedAt == null) && (p.Discount.EndDate > DateTime.UtcNow)) ? Math.Round(p.Price - (((p.Discount.DiscountPercent) / 100) * p.Price)) : p.Price,
				DiscountPrecentage = p.Discount != null && p.Discount.IsActive && p.Discount.DeletedAt == null ?  p.Discount.DiscountPercent : null,
				DiscountName = p.Discount != null && p.Discount.DeletedAt==null ? p.Discount.Name : null,
				EndAt = p.Discount != null && p.Discount.IsActive && p.Discount.DeletedAt == null ? p.Discount.EndDate : null,
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
			};
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
					.Select(maptoProductDtoexpression)
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


	
	}
} 