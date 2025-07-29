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

namespace E_Commerce.Services.ProductServices
{
	public interface IProductImageService
	{

		Task<Result<List<ImageDto>>> GetProductImagesAsync(int productId);
		Task<Result<List<ImageDto>>> AddProductImagesAsync(int productId, List<IFormFile> images, string userId);
		Task<Result<bool>> RemoveProductImageAsync(int productId, int imageId, string userId);
		Task<Result<ImageDto>> UploadAndSetMainImageAsync(int productId, Microsoft.AspNetCore.Http.IFormFile mainImage, string userId);
		
	}

	public class ProductImageService : IProductImageService
	{
		private readonly IBackgroundJobClient _backgroundJobClient;
		private readonly IUnitOfWork _unitOfWork;
		private readonly ILogger<ProductImageService> _logger;
		private readonly IAdminOpreationServices _adminOpreationServices;
		private readonly IErrorNotificationService _errorNotificationService;
		private readonly IImagesServices _imagesServices;
		private readonly ISubCategoryServices _subCategoryServices;
		private readonly ICacheManager _cacheManager;
		private readonly IProductCatalogService _productCatalogService;
		private const string CACHE_TAG_PRODUCT_SEARCH = "product_search";

		public const string CACHE_TAG_CATEGORY_WITH_DATA = "categorywithdata";
		private static readonly string[] PRODUCT_CACHE_TAGS = new[] { CACHE_TAG_PRODUCT_SEARCH, CACHE_TAG_CATEGORY_WITH_DATA, PRODUCT_WITH_VARIANT_TAG };
		private const string PRODUCT_WITH_VARIANT_TAG = "productwithvariantdata";

		public ProductImageService(
			ICacheManager cacheManager,
			IBackgroundJobClient backgroundJobClient,
			IUnitOfWork unitOfWork,
			ILogger<ProductImageService> logger,
			IAdminOpreationServices adminOpreationServices,
			IErrorNotificationService errorNotificationService,
			IImagesServices imagesServices,
			ISubCategoryServices subCategoryServices,
			IProductCatalogService productCatalogService)
		{
			_cacheManager = cacheManager;
			_backgroundJobClient = backgroundJobClient;
			_unitOfWork = unitOfWork;
			_logger = logger;
			_adminOpreationServices = adminOpreationServices;
			_errorNotificationService = errorNotificationService;
			_imagesServices = imagesServices;
			_subCategoryServices = subCategoryServices;
			_productCatalogService = productCatalogService;
		}
		private void RemoveProductCaches()
		{

			BackgroundJob.Enqueue(() => _cacheManager.RemoveByTagsAsync(PRODUCT_CACHE_TAGS));
		}

		public async Task<Result<List<ImageDto>>> GetProductImagesAsync(int productId)
		{
			try
			{
				var images = await _unitOfWork.Image.GetAll()
					.Where(i => i.ProductId == productId && i.DeletedAt == null)
					.Select(i => new ImageDto
					{
						Id = i.Id,
						Url = i.Url,
						IsMain = i.IsMain
					})
					.ToListAsync();

				if (!images.Any())
					return Result<List<ImageDto>>.Fail("No images found for this product", 404);

				return Result<List<ImageDto>>.Ok(images, "Product images retrieved successfully", 200);
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, $"Error in GetProductImagesAsync for productId: {productId}");
				 	_backgroundJobClient.Enqueue(()=> _errorNotificationService.SendErrorNotificationAsync(ex.Message, ex.StackTrace));
				return Result<List<ImageDto>>.Fail("Error retrieving product images", 500);
			}
		}

		public async Task<Result<List<ImageDto>>> AddProductImagesAsync(int productId, List<IFormFile> images, string userId)
		{
			_logger.LogInformation($"Adding images to product: {productId}");
			
			if (images == null || !images.Any())
				return Result<List<ImageDto>>.Fail("No images provided", 400);
			
			var product = await _unitOfWork.Product.GetByIdAsync(productId);
			if (product == null)
				return Result<List<ImageDto>>.Fail("Product not found", 404);
			
			using var transaction = await _unitOfWork.BeginTransactionAsync();
			try
			{
				var saveResult = await _imagesServices.SaveProductImagesAsync(images,productId, userId);
				if (!saveResult.Success || saveResult.Data == null)
				{
					await transaction.RollbackAsync();
					return Result<List<ImageDto>>.Fail("Failed to save images", 500, saveResult.Warnings);
				}

				foreach (var image in saveResult.Data)
					image.ProductId = productId;

				_unitOfWork.Image.UpdateList(saveResult.Data);

				var addedImages = saveResult.Data.Select(i => new ImageDto
				{
					Id = i.Id,
					Url = i.Url,
					IsMain = i.IsMain
				}).ToList();

				await _adminOpreationServices.AddAdminOpreationAsync(
					$"Add Images to Product {productId}",
					Opreations.AddOpreation,
					userId,
					productId
				);

				await _unitOfWork.CommitAsync();
				await transaction.CommitAsync();
				RemoveProductCaches();



				_logger.LogInformation($"Successfully added {addedImages.Count} images to product {productId}");

				return Result<List<ImageDto>>.Ok(addedImages, "Images added successfully", 201, saveResult.Warnings);
			}
			catch (Exception ex)
			{
				await transaction.RollbackAsync();
				_logger.LogError(ex, $"Error in AddProductImageAsync for productId: {productId}");
				 	_backgroundJobClient.Enqueue(()=> _errorNotificationService.SendErrorNotificationAsync(ex.Message, ex.StackTrace));
				return Result<List<ImageDto>>.Fail("Error adding images", 500);
			}
		}

		public async Task<Result<bool>> RemoveProductImageAsync(int productId, int imageId, string userId)
		{
			_logger.LogInformation($"Removing image {imageId} from product: {productId}");

			using var transaction = await _unitOfWork.BeginTransactionAsync();
			try
			{
				var product = await _unitOfWork.Product.GetByIdAsync(productId);
				if (product == null)
				{
					await transaction.RollbackAsync();
					return Result<bool>.Fail("Product not found", 404);
				}

				var productImages = await _unitOfWork.Image.GetAll()
					.Where(i => i.ProductId == productId && i.DeletedAt == null)
					.ToListAsync();

				var imageToRemove = productImages.FirstOrDefault(i => i.Id == imageId);

				if (imageToRemove == null)
				{
					await transaction.RollbackAsync();
					return Result<bool>.Fail("Image not found on this product or already deleted", 404);
				}

				var deleteResult = await _imagesServices.DeleteImageAsync(imageToRemove);
				if (!deleteResult.Success)
				{
					await transaction.RollbackAsync();
					return Result<bool>.Fail(deleteResult.Message ?? "Failed to remove image", 400);
				}

				if (imageToRemove.IsMain)
				{
					var nextMainImage = productImages.FirstOrDefault(i => i.Id != imageId);
					if (nextMainImage != null)
					{
						nextMainImage.IsMain = true;
						_unitOfWork.Image.Update(nextMainImage);
					}
				}

				bool productShouldBeDeactivated = (productImages.Count == 1);
				var warnings = new List<string>();

				if (productShouldBeDeactivated && product.IsActive)
				{
					await _productCatalogService.DeactivateProductAsync(productId, userId);
					warnings.Add("Product was deactivated because its last image was removed.");
				}

				await _adminOpreationServices.AddAdminOpreationAsync(
					$"Remove Image {imageId} from Product {productId}",
					Opreations.DeleteOpreation,
					userId,
					productId
				);

				await _unitOfWork.CommitAsync();
				await transaction.CommitAsync();

				if (productShouldBeDeactivated)
				{
					await _subCategoryServices.DeactivateSubCategoryIfAllProductsAreInactiveAsync(product.SubCategoryId, userId);
				}

				RemoveProductCaches();

				_logger.LogInformation($"Image {imageId} removed from product {productId}");

				return Result<bool>.Ok(true, "Image removed", 200, warnings: warnings);
			}
			catch (Exception ex)
			{
				await transaction.RollbackAsync();
				_logger.LogError(ex, $"Error in RemoveProductImageAsync for productId: {productId}, imageId: {imageId}");
				 	_backgroundJobClient.Enqueue(()=> _errorNotificationService.SendErrorNotificationAsync(ex.Message, ex.StackTrace));
				return Result<bool>.Fail("Unexpected error while removing image", 500);
			}
		}


		public async Task<Result<ImageDto>> UploadAndSetMainImageAsync(int productId, IFormFile mainImage, string userId)
		{
			_logger.LogInformation($"Setting new main image for product: {productId}");
			
			var product = await _unitOfWork.Product.GetByIdAsync(productId);
			if (product == null)
				return Result<ImageDto>.Fail("Product not found", 404);
			
			using var transaction = await _unitOfWork.BeginTransactionAsync();
			try
			{
				// Unset previous main images
				var existingMainImages = await _unitOfWork.Image.GetAll()
					.Where(i => i.ProductId == productId && i.DeletedAt == null && i.IsMain)
					.ToListAsync();

				foreach (var existingImage in existingMainImages)
					existingImage.IsMain = false;

				_unitOfWork.Image.UpdateList(existingMainImages);

				// Save new image
				var saveResult = await _imagesServices.SaveMainProductImageAsync(mainImage,productId, userId);
				if (!saveResult.Success || saveResult.Data == null)
				{
					await transaction.RollbackAsync();
					var errorMsg = !string.IsNullOrWhiteSpace(saveResult.Message)
						? saveResult.Message
						: "Image saving failed. No image returned.";
					return Result<ImageDto>.Fail(errorMsg, 400);
				}

				saveResult.Data.ProductId = productId;
				saveResult.Data.IsMain = true;
				_unitOfWork.Image.Update(saveResult.Data);

				await _adminOpreationServices.AddAdminOpreationAsync(
					$"Set Main Image for Product {productId}",
					Opreations.UpdateOpreation,
					userId,
					productId
				);

				await _unitOfWork.CommitAsync();
				await transaction.CommitAsync();
				_logger.LogInformation($"Main image set successfully for product {productId}");
				var imageDto = new ImageDto
				{
					Id = saveResult.Data.Id,
					Url = saveResult.Data.Url,
					IsMain = saveResult.Data.IsMain
				};
				RemoveProductCaches();
				return Result<ImageDto>.Ok(imageDto, "Main image updated", 200);
			}
			catch (Exception ex)
			{
				await transaction.RollbackAsync();
				_logger.LogError(ex, $"Error in AddMainImageAsync for productId: {productId}");
				 	_backgroundJobClient.Enqueue(()=> _errorNotificationService.SendErrorNotificationAsync(ex.Message, ex.StackTrace));
				return Result<ImageDto>.Fail("Error setting main image", 500);
			}
		}

	
	}
} 