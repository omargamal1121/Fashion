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
using Hangfire;
using E_Commers.Services.Cache;
using E_Commers.Services.Product;

namespace E_Commers.Services.Product
{
	public interface IProductVariantService
	{
		Task<Result<List<ProductVariantDto>>> GetProductVariantsAsync(int productId);
		Task<Result<List<ProductVariantDto>>> GetProductVariantsAsync(int productId, bool? isActive, bool? deletedOnly);
		Task<Result<ProductVariantDto>> GetVariantByIdAsync(int id);
		Task<Result<ProductVariantDto>> AddVariantAsync(int productId, CreateProductVariantDto dto, string userId);
		Task<Result<ProductVariantDto>> UpdateVariantAsync(int id, UpdateProductVariantDto dto, string userId);
		Task<Result<string>> DeleteVariantAsync(int id, string userId);
		Task<Result<string>> UpdateVariantQuantityAsync(int id, int newQuantity, string userId);
		Task<Result<List<ProductVariantDto>>> GetVariantsBySearchAsync(int productId, string color = null, VariantSize? size = null, bool? isActive = null, bool? deletedOnly = null);
		Task<Result<string>> ActivateVariantAsync(int id, string userId);
		Task<Result<string>> DeactivateVariantAsync(int id, string userId);
		Task<Result<string>> AddVariantQuantityAsync(int id, int addQuantity, string userId);
		Task<Result<string>> RemoveVariantQuantityAsync(int id, int removeQuantity, string userId);
		Task<Result<string>> RestoreVariantAsync(int id, string userId);
	}

	public class ProductVariantService : IProductVariantService
	{
		private readonly IUnitOfWork _unitOfWork;
		private readonly ILogger<ProductVariantService> _logger;
		private readonly IAdminOpreationServices _adminOpreationServices;
		private readonly IErrorNotificationService _errorNotificationService;
		private readonly ICacheManager _cacheManager;
		private readonly IBackgroundJobClient _backgroundJobClient;
		private readonly IProductCatalogService _productCatalogService;

		public ProductVariantService(
			IUnitOfWork unitOfWork,
			ILogger<ProductVariantService> logger,
			IAdminOpreationServices adminOpreationServices,
			IErrorNotificationService errorNotificationService,
			ICacheManager cacheManager,
			IBackgroundJobClient backgroundJobClient,
			IProductCatalogService productCatalogService)
		{
			_unitOfWork = unitOfWork;
			_logger = logger;
			_adminOpreationServices = adminOpreationServices;
			_errorNotificationService = errorNotificationService;
			_cacheManager = cacheManager;
			_backgroundJobClient = backgroundJobClient;
			_productCatalogService = productCatalogService;
		}
		private string GetVariantCacheKey(int id) => $"variant:{id}";
		private string GetProductVariantsCacheKey(int productId) => $"product:{productId}:variants";
		private string GetProductCacheTag(int productId) => $"product:{productId}";
	

		private const string PRODUCT_WITH_VARIANT_TAG = "productwithvariantdata";
		private const string CACHE_TAG_PRODUCT_SEARCH = "product_search";
		private const string VARIANT_DATA_TAG = "variantdata";
		private static readonly string[] PRODUCT_CACHE_TAGS = new[] { PRODUCT_WITH_VARIANT_TAG, CACHE_TAG_PRODUCT_SEARCH, VARIANT_DATA_TAG };

		private void RemoveProductCachesAsync()
		{
			_backgroundJobClient.Enqueue(() => _cacheManager.RemoveByTagsAsync(PRODUCT_CACHE_TAGS));
		}

		public async Task<Result<List<ProductVariantDto>>> GetProductVariantsAsync(int productId)
		{
			var cacheKey = GetProductVariantsCacheKey(productId);
			var cached = await _cacheManager.GetAsync<List<ProductVariantDto>>(cacheKey);
			if (cached != null)
				return Result<List<ProductVariantDto>>.Ok(cached, "Product variants retrieved from cache", 200);

			var result = await GetProductVariantsAsync(productId, null, null);
			if (result.Success)
			{
				_backgroundJobClient.Enqueue(() => _cacheManager.SetAsync(cacheKey, result.Data, null, new[] { GetProductCacheTag(productId), VARIANT_DATA_TAG }));
			}
			return result;
		}

		public async Task<Result<List<ProductVariantDto>>> GetProductVariantsAsync(int productId, bool? isActive, bool? deletedOnly)
		{
			try
			{
				var query = _unitOfWork.Product.GetAll().Where(p => p.Id == productId);
				if (isActive.HasValue)
					query = query.Where(p => p.IsActive == isActive.Value);
				if (deletedOnly.HasValue)
				{
					if (deletedOnly.Value)
						query = query.Where(p => p.DeletedAt != null);
					else
						query = query.Where(p => p.DeletedAt == null);
				}
				var variants = await query
					.SelectMany(p => p.ProductVariants.Where(v => v.DeletedAt == null))
					.Select(v => new ProductVariantDto
					{
						Id = v.Id,
						Color = v.Color,
						Size = v.Size,
						Waist = v.Waist,
						Length = v.Length,
						Quantity = v.Quantity,
						ProductId = v.ProductId
					})
					.ToListAsync();

				if (!variants.Any())
					return Result<List<ProductVariantDto>>.Fail("No variants found for this product", 404);

				return Result<List<ProductVariantDto>>.Ok(variants, "Product variants retrieved successfully", 200);
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, $"Error in GetProductVariantsAsync for productId: {productId}");
				 	_backgroundJobClient.Enqueue(()=> _errorNotificationService.SendErrorNotificationAsync(ex.Message, ex.StackTrace));
				return Result<List<ProductVariantDto>>.Fail("Error retrieving product variants", 500);
			}
		}

		public async Task<Result<ProductVariantDto>> GetVariantByIdAsync(int id)
		{
			var cacheKey = GetVariantCacheKey(id);
			var cached = await _cacheManager.GetAsync<ProductVariantDto>(cacheKey);
			if (cached != null)
				return Result<ProductVariantDto>.Ok(cached, "Variant retrieved from cache", 200);

			try
			{
				var variant = await _unitOfWork.Repository<ProductVariant>().GetAll()
					.Where(v => v.Id == id && v.DeletedAt == null)
					.Select(v => new ProductVariantDto
					{
						Id = v.Id,
						Color = v.Color,
						Size = v.Size,
						Waist = v.Waist,
						Length = v.Length,
						Quantity = v.Quantity,
						ProductId = v.ProductId
					})
					.FirstOrDefaultAsync();

				if (variant == null)
					return Result<ProductVariantDto>.Fail("Variant not found", 404);

				_backgroundJobClient.Enqueue(() => _cacheManager.SetAsync(cacheKey, variant,null,  new[] { GetProductCacheTag(variant.ProductId), VARIANT_DATA_TAG }));
				return Result<ProductVariantDto>.Ok(variant, "Variant retrieved successfully", 200);
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, $"Error in GetVariantByIdAsync for id: {id}");
				 	_backgroundJobClient.Enqueue(()=> _errorNotificationService.SendErrorNotificationAsync(ex.Message, ex.StackTrace));
				return Result<ProductVariantDto>.Fail("Error retrieving variant", 500);
			}
		}

		public async Task<Result<ProductVariantDto>> AddVariantAsync(int productId, CreateProductVariantDto dto, string userId)
		{
			_logger.LogInformation($"Adding variant to product: {productId}");
			try
			{
				// Validate product exists
				var product = await _unitOfWork.Product.GetByIdAsync(productId);
				if (product == null)
					return Result<ProductVariantDto>.Fail("Product not found", 404);

				//// Validate required fields
				if (string.IsNullOrEmpty(dto.Color) || dto.Size == null)
					return Result<ProductVariantDto>.Fail("Color and Size are required", 400);

				if (dto.Price <= 0)
					return Result<ProductVariantDto>.Fail("Price must be greater than zero", 400);

				if (dto.Quantity < 0)
					return Result<ProductVariantDto>.Fail("Quantity cannot be negative", 400);

				// Check for duplicate variant
				var existingVariant = await _unitOfWork.Product.GetAll()
					.Where(p => p.Id == productId)
					.SelectMany(p => p.ProductVariants.Where(v => v.DeletedAt == null))
					.FirstOrDefaultAsync(v => v.Color == dto.Color && v.Size == dto.Size);

				if (existingVariant != null)
					return Result<ProductVariantDto>.Fail("Variant with this color and size already exists", 400);

				using var transaction = await _unitOfWork.BeginTransactionAsync();

				var variant = new ProductVariant
				{
					ProductId = productId,
					Color = dto.Color,
					Size = dto.Size,
					Quantity = dto.Quantity
				};

				var result = await _unitOfWork.Repository<ProductVariant>().CreateAsync(variant);
				if (result == null)
				{
					await transaction.RollbackAsync();
					return Result<ProductVariantDto>.Fail("Failed to add variant", 400);
				}

				// Log admin operation
				await _adminOpreationServices.AddAdminOpreationAsync(
					$"Add Variant to Product {productId}",
					Opreations.AddOpreation,
					userId,
					productId
				);

				await _unitOfWork.CommitAsync();

				var variantDto = new ProductVariantDto
				{
					Id = variant.Id,
					Color = variant.Color,
					Size = variant.Size,
					Waist = variant.Waist,
					Length = variant.Length,
					Quantity = variant.Quantity,
					ProductId = variant.ProductId
				};

				// Remove cache for product and subcategory
				RemoveProductCachesAsync();

				return Result<ProductVariantDto>.Ok(variantDto, "Variant added successfully", 201);
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, $"Error in AddVariantAsync for productId: {productId}");
				 	_backgroundJobClient.Enqueue(()=> _errorNotificationService.SendErrorNotificationAsync(ex.Message, ex.StackTrace));
				return Result<ProductVariantDto>.Fail("Error adding variant", 500);
			}
		}

		public async Task<Result<ProductVariantDto>> UpdateVariantAsync(int id, UpdateProductVariantDto dto, string userId)
		{
			_logger.LogInformation($"Updating variant: {id}");
			try
			{
				var variant = await _unitOfWork.Repository<ProductVariant>().GetByIdAsync(id);
				if (variant == null)
					return Result<ProductVariantDto>.Fail("Variant not found", 404);

				// Check for duplicate variant (excluding current variant)
				if (!string.IsNullOrEmpty(dto.Color) && dto.Size != null)
				{
					var existingVariant = await _unitOfWork.Repository<ProductVariant>().GetAll()
						.Where(v => v.ProductId == variant.ProductId && v.Id != id && v.DeletedAt == null)
						.FirstOrDefaultAsync(v => v.Color == dto.Color && v.Size == dto.Size);

					if (existingVariant != null)
						return Result<ProductVariantDto>.Fail("Variant with this color and size already exists", 400);
				}

				using var transaction = await _unitOfWork.BeginTransactionAsync();

				// Update fields
				if (!string.IsNullOrEmpty(dto.Color))
					variant.Color = dto.Color;
				variant.Size = dto.Size;
				
				var result = _unitOfWork.Repository<ProductVariant>().Update(variant);
				if (!result)
				{
					await transaction.RollbackAsync();
					return Result<ProductVariantDto>.Fail("Failed to update variant", 400);
				}

				// Log admin operation
				await _adminOpreationServices.AddAdminOpreationAsync(
					$"Update Variant {id}",
					Opreations.UpdateOpreation,
					userId,
					variant.ProductId
				);

				await _unitOfWork.CommitAsync();

				var variantDto = new ProductVariantDto
				{
					Id = variant.Id,
					Color = variant.Color,
					Size = variant.Size,
					Waist = variant.Waist,
					Length = variant.Length,
					Quantity = variant.Quantity,
					ProductId = variant.ProductId
				};
				var product = await _unitOfWork.Product.GetByIdAsync(variant.ProductId);
				RemoveProductCachesAsync();

				return Result<ProductVariantDto>.Ok(variantDto, "Variant updated successfully", 200);
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, $"Error in UpdateVariantAsync for id: {id}");
				 	_backgroundJobClient.Enqueue(()=> _errorNotificationService.SendErrorNotificationAsync(ex.Message, ex.StackTrace));
				return Result<ProductVariantDto>.Fail("Error updating variant", 500);
			}
		}

		public async Task<Result<string>> DeleteVariantAsync(int id, string userId)
		{
			_logger.LogInformation($"Deleting variant: {id}");
			try
			{
				var variant = await _unitOfWork.Repository<ProductVariant>().GetByIdAsync(id);
				if (variant == null)
					return Result<string>.Fail("Variant not found", 404);

				using var transaction = await _unitOfWork.BeginTransactionAsync();

				var result = await _unitOfWork.Repository<ProductVariant>().SoftDeleteAsync(id);
				if (!result)
				{
					await transaction.RollbackAsync();
					return Result<string>.Fail("Failed to delete variant", 400);
				}

		
				await _adminOpreationServices.AddAdminOpreationAsync(
					$"Delete Variant {id}",
					Opreations.DeleteOpreation,
					userId,
					variant.ProductId
				);

				await _unitOfWork.CommitAsync();
				// Remove cache for product and subcategory
				var product = await _unitOfWork.Product.GetByIdAsync(variant.ProductId);
				RemoveProductCachesAsync();
				return Result<string>.Ok("Variant deleted successfully", "Variant deleted", 200);
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, $"Error in DeleteVariantAsync for id: {id}");
				 	_backgroundJobClient.Enqueue(()=> _errorNotificationService.SendErrorNotificationAsync(ex.Message, ex.StackTrace));
				return Result<string>.Fail("Error deleting variant", 500);
			}
		}

		public async Task<Result<string>> UpdateVariantQuantityAsync(int id, int newQuantity, string userId)
		{
			_logger.LogInformation($"Updating quantity for variant: {id}");
			try
			{
				if (newQuantity < 0)
					return Result<string>.Fail("Quantity cannot be negative", 400);

				var variant = await _unitOfWork.Repository<ProductVariant>().GetByIdAsync(id);
				if (variant == null)
					return Result<string>.Fail("Variant not found", 404);

				using var transaction = await _unitOfWork.BeginTransactionAsync();

				variant.Quantity = newQuantity;
				var result = _unitOfWork.Repository<ProductVariant>().Update(variant);
				if (!result)
				{
					await transaction.RollbackAsync();
					return Result<string>.Fail("Failed to update quantity", 400);
				}

				// Log admin operation
				await _adminOpreationServices.AddAdminOpreationAsync(
					$"Update Quantity for Variant {id}",
					Opreations.UpdateOpreation,
					userId,
					variant.ProductId
				);

				await _unitOfWork.CommitAsync();
				// Update parent product's quantity using injected catalog service
				var product = await _unitOfWork.Product.GetByIdAsync(variant.ProductId);
				if (product != null)
				{
					_productCatalogService.UpdateProductQuantity(product);
				}
				// Remove cache for product
				RemoveProductCachesAsync();
				return Result<string>.Ok("Quantity updated successfully", "Quantity updated", 200);
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, $"Error in UpdateVariantQuantityAsync for id: {id}");
				 	_backgroundJobClient.Enqueue(()=> _errorNotificationService.SendErrorNotificationAsync(ex.Message, ex.StackTrace));
				return Result<string>.Fail("Error updating quantity", 500);
			}
		}

		public async Task<Result<List<ProductVariantDto>>> GetVariantsBySearchAsync(int productId, string color = null, VariantSize? size = null, bool? isActive = null, bool? deletedOnly = null)
		{
			// No cache for search queries (unless you want to cache by all params)
			try
			{
				var query = _unitOfWork.Product.GetAll().Where(p => p.Id == productId);
				if (isActive.HasValue)
					query = query.Where(p => p.IsActive == isActive.Value);
				if (deletedOnly.HasValue)
				{
					if (deletedOnly.Value)
						query = query.Where(p => p.DeletedAt != null);
					else
						query = query.Where(p => p.DeletedAt == null);
				}
				var variantsQuery = query.SelectMany(p => p.ProductVariants.Where(v => v.DeletedAt == null));
				if (!string.IsNullOrEmpty(color))
					variantsQuery = variantsQuery.Where(v => v.Color == color);
				if (size.HasValue)
					variantsQuery = variantsQuery.Where(v => v.Size == size.Value);
				var variants = await variantsQuery
					.Select(v => new ProductVariantDto
					{
						Id = v.Id,
						Color = v.Color,
						Size = v.Size,
						Waist = v.Waist,
						Length = v.Length,
						Quantity = v.Quantity,
						ProductId = v.ProductId
					})
					.ToListAsync();
				if (!variants.Any())
					return Result<List<ProductVariantDto>>.Fail("No variants found matching the search criteria", 404);
				return Result<List<ProductVariantDto>>.Ok(variants, "Variants retrieved successfully", 200);
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, $"Error in GetVariantsBySearchAsync for productId: {productId}");
				 	_backgroundJobClient.Enqueue(()=> _errorNotificationService.SendErrorNotificationAsync(ex.Message, ex.StackTrace));
				return Result<List<ProductVariantDto>>.Fail("Error retrieving variants by search", 500);
			}
		}

		public async Task<Result<string>> ActivateVariantAsync(int id, string userId)
		{
			_logger.LogInformation($"Activating variant: {id}");
			try
			{
				var variant = await _unitOfWork.Repository<ProductVariant>().GetByIdAsync(id);
				if (variant == null)
					return Result<string>.Fail("Variant not found", 404);

				// To activate: must have (length and waist) or size, and quantity > 0
				bool hasLengthAndWaist = variant.Length.HasValue && variant.Waist.HasValue;
				bool hasSize = variant.Size.HasValue;
				if (!(variant.Quantity > 0 && (hasLengthAndWaist || hasSize)))
					return Result<string>.Fail("To activate, variant must have (length and waist) or size, and quantity > 0", 400);

				variant.IsActive = true;
				var result = _unitOfWork.Repository<ProductVariant>().Update(variant);
				if (!result)
					return Result<string>.Fail("Failed to activate variant", 400);

				await _adminOpreationServices.AddAdminOpreationAsync(
					$"Activate Variant {id}",
					Opreations.UpdateOpreation,
					userId,
					variant.ProductId
				);

				await _unitOfWork.CommitAsync();

				// Remove cache for product and subcategory
				var product = await _unitOfWork.Product.GetByIdAsync(variant.ProductId);
				RemoveProductCachesAsync();

				return Result<string>.Ok("Variant activated", "Status updated", 200);
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, $"Error in ActivateVariantAsync for id: {id}");
				 	_backgroundJobClient.Enqueue(()=> _errorNotificationService.SendErrorNotificationAsync(ex.Message, ex.StackTrace));
				return Result<string>.Fail("Error activating variant", 500);
			}
		}

		public async Task<Result<string>> DeactivateVariantAsync(int id, string userId)
		{
			_logger.LogInformation($"Deactivating variant: {id}");
			try
			{
				var variant = await _unitOfWork.Repository<ProductVariant>().GetByIdAsync(id);
				if (variant == null)
					return Result<string>.Fail("Variant not found", 404);

				variant.IsActive = false;
				var result = _unitOfWork.Repository<ProductVariant>().Update(variant);
				if (!result)
					return Result<string>.Fail("Failed to deactivate variant", 400);

				await _adminOpreationServices.AddAdminOpreationAsync(
					$"Deactivate Variant {id}",
					Opreations.UpdateOpreation,
					userId,
					variant.ProductId
				);

				await _unitOfWork.CommitAsync();

				var product = await _unitOfWork.Product.GetByIdAsync(variant.ProductId);
				RemoveProductCachesAsync();

				return Result<string>.Ok("Variant deactivated", "Status updated", 200);
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, $"Error in DeactivateVariantAsync for id: {id}");
				 	_backgroundJobClient.Enqueue(()=> _errorNotificationService.SendErrorNotificationAsync(ex.Message, ex.StackTrace));
				return Result<string>.Fail("Error deactivating variant", 500);
			}
		}

		public async Task<Result<string>> AddVariantQuantityAsync(int id, int addQuantity, string userId)
		{
			_logger.LogInformation($"Adding quantity for variant: {id}");
			try
			{
				if (addQuantity <= 0)
					return Result<string>.Fail("Add quantity must be positive", 400);

				var variant = await _unitOfWork.Repository<ProductVariant>().GetByIdAsync(id);
				if (variant == null)
					return Result<string>.Fail("Variant not found", 404);

				using var transaction = await _unitOfWork.BeginTransactionAsync();

				variant.Quantity += addQuantity;
				var result = _unitOfWork.Repository<ProductVariant>().Update(variant);
				if (!result)
				{
					await transaction.RollbackAsync();
					return Result<string>.Fail("Failed to add quantity", 400);
				}

				await _adminOpreationServices.AddAdminOpreationAsync(
					$"Add Quantity for Variant {id}",
					Opreations.UpdateOpreation,
					userId,
					variant.ProductId
				);

				await _unitOfWork.CommitAsync();
				var product = await _unitOfWork.Product.GetByIdAsync(variant.ProductId);
				if (product != null)
				{
					_productCatalogService.UpdateProductQuantity(product);
				}
				RemoveProductCachesAsync();
				return Result<string>.Ok("Quantity added successfully", "Quantity added", 200);
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, $"Error in AddVariantQuantityAsync for id: {id}");
				 	_backgroundJobClient.Enqueue(()=> _errorNotificationService.SendErrorNotificationAsync(ex.Message, ex.StackTrace));
				return Result<string>.Fail("Error adding quantity", 500);
			}
		}

		public async Task<Result<string>> RemoveVariantQuantityAsync(int id, int removeQuantity, string userId)
		{
			_logger.LogInformation($"Removing quantity for variant: {id}");
			try
			{
				if (removeQuantity <= 0)
					return Result<string>.Fail("Remove quantity must be positive", 400);

				var variant = await _unitOfWork.Repository<ProductVariant>().GetByIdAsync(id);
				if (variant == null)
					return Result<string>.Fail("Variant not found", 404);

				if (variant.Quantity < removeQuantity)
					return Result<string>.Fail("Not enough quantity to remove", 400);

				using var transaction = await _unitOfWork.BeginTransactionAsync();

				variant.Quantity -= removeQuantity;
				if (variant.Quantity == 0)
				{
					variant.IsActive = false;
				}
				var result = _unitOfWork.Repository<ProductVariant>().Update(variant);
				if (!result)
				{
					await transaction.RollbackAsync();
					return Result<string>.Fail("Failed to remove quantity", 400);
				}

				await _adminOpreationServices.AddAdminOpreationAsync(
					$"Remove Quantity for Variant {id}",
					Opreations.UpdateOpreation,
					userId,
					variant.ProductId
				);

				await _unitOfWork.CommitAsync();
				var product = await _unitOfWork.Product.GetByIdAsync(variant.ProductId);
				if (product != null)
				{
					_productCatalogService.UpdateProductQuantity(product);
				}
				RemoveProductCachesAsync();
				return Result<string>.Ok("Quantity removed successfully", "Quantity removed", 200);
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, $"Error in RemoveVariantQuantityAsync for id: {id}");
				 	_backgroundJobClient.Enqueue(()=> _errorNotificationService.SendErrorNotificationAsync(ex.Message, ex.StackTrace));
				return Result<string>.Fail("Error removing quantity", 500);
			}
		}

		public async Task<Result<string>> RestoreVariantAsync(int id, string userId)
		{
			_logger.LogInformation($"Restoring variant: {id}");
			try
			{
				var variant = await _unitOfWork.Repository<ProductVariant>().GetAll()
					.Where(v => v.Id == id && v.DeletedAt != null)
					.FirstOrDefaultAsync();
				if (variant == null)
					return Result<string>.Fail("Variant not found or not deleted", 404);

				using var transaction = await _unitOfWork.BeginTransactionAsync();

				variant.DeletedAt = null;
				variant.IsActive = false;
				var result = _unitOfWork.Repository<ProductVariant>().Update(variant);
				if (!result)
				{
					await transaction.RollbackAsync();
					return Result<string>.Fail("Failed to restore variant", 400);
				}

				await _adminOpreationServices.AddAdminOpreationAsync(
					$"Restore Variant {id}",
					Opreations.UpdateOpreation,
					userId,
					variant.ProductId
				);

				await _unitOfWork.CommitAsync();
				var product = await _unitOfWork.Product.GetByIdAsync(variant.ProductId);
				if (product != null)
				{
					_productCatalogService.UpdateProductQuantity(product);
				}
				RemoveProductCachesAsync();
				return Result<string>.Ok("Variant restored successfully", "Variant restored", 200);
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, $"Error in RestoreVariantAsync for id: {id}");
				await _errorNotificationService.SendErrorNotificationAsync(ex.Message, ex.StackTrace);
				return Result<string>.Fail("Error restoring variant", 500);
			}
		}
	}
} 