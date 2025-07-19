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

namespace E_Commers.Services.Product
{
	public interface IProductImageService
	{
		Task<Result<List<ImageDto>>> GetProductImagesAsync(int productId);
		Task<Result<List<ImageDto>>> AddProductImagesAsync(int productId, List<IFormFile> images, string userId);
		Task<Result<bool>> RemoveProductImageAsync(int productId, int imageId, string userId);
		Task<Result<bool>> UploadAndSetMainImageAsync(int productId, Microsoft.AspNetCore.Http.IFormFile mainImage, string userId);
		
	}

	public class ProductImageService : IProductImageService
	{
		private readonly IUnitOfWork _unitOfWork;
		private readonly ILogger<ProductImageService> _logger;
		private readonly IAdminOpreationServices _adminOpreationServices;
		private readonly IErrorNotificationService _errorNotificationService;
		private readonly IImagesServices _imagesServices;

		public ProductImageService(
			IUnitOfWork unitOfWork,
			ILogger<ProductImageService> logger,
			IAdminOpreationServices adminOpreationServices,
			IErrorNotificationService errorNotificationService,
			IImagesServices imagesServices)
		{
			_unitOfWork = unitOfWork;
			_logger = logger;
			_adminOpreationServices = adminOpreationServices;
			_errorNotificationService = errorNotificationService;
			_imagesServices = imagesServices;
		}

		public async Task<Result<List<ImageDto>>> GetProductImagesAsync(int productId)
		{
			try
			{
				var images = await _unitOfWork.Product.GetAll()
					.Where(p => p.Id == productId)
					.SelectMany(p => p.Images.Where(i => i.DeletedAt == null))
					.Select(i => new ImageDto
					{
						Id = i.Id,
						Url = i.Url,
					
					})
					.ToListAsync();

				if (!images.Any())
					return Result<List<ImageDto>>.Fail("No images found for this product", 404);

				return Result<List<ImageDto>>.Ok(images, "Product images retrieved successfully", 200);
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, $"Error in GetProductImagesAsync for productId: {productId}");
				await _errorNotificationService.SendErrorNotificationAsync(ex.Message, ex.StackTrace);
				return Result<List<ImageDto>>.Fail("Error retrieving product images", 500);
			}
		}

		public async Task<Result<List<ImageDto>>> AddProductImagesAsync(int productId, List<IFormFile> images, string userId)
		{
			_logger.LogInformation($"Adding images to product: {productId}");

			try
			{
				if (images == null || !images.Any())
					return Result<List<ImageDto>>.Fail("No images provided", 400);

				var product = await _unitOfWork.Product.GetByIdAsync(productId);
				if (product == null)
					return Result<List<ImageDto>>.Fail("Product not found", 404);

				using var transaction = await _unitOfWork.BeginTransactionAsync();

				var saveResult = await _imagesServices.SaveProductImagesAsync(images, userId);
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

				_logger.LogInformation($"Successfully added {addedImages.Count} images to product {productId}");

				return Result<List<ImageDto>>.Ok(addedImages, "Images added successfully", 201, saveResult.Warnings);
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, $"Error in AddProductImageAsync for productId: {productId}");
				await _errorNotificationService.SendErrorNotificationAsync(ex.Message, ex.StackTrace);
				return Result<List<ImageDto>>.Fail("Error adding images", 500);
			}
		}

		public async Task<Result<bool>> RemoveProductImageAsync(int productId, int imageId, string userId)
		{
			_logger.LogInformation($"Removing image {imageId} from product: {productId}");

				var image = await _unitOfWork.Image.GetAll()
					.Where(i => i.Id == imageId && i.ProductId == productId && i.DeletedAt == null)
					.FirstOrDefaultAsync();

				if (image == null)
					return Result<bool>.Fail("Image not found", 404);
				using var transaction = await _unitOfWork.BeginTransactionAsync();
			try
			{


				var deleteResult = await _imagesServices.DeleteImageAsync(image);
				if (!deleteResult.Success)
				{
					await transaction.RollbackAsync();
					return Result<bool>.Fail(deleteResult.Message ?? "Failed to remove image", 400);
				}

				if (image.IsMain)
				{
					var nextMainImage = await _unitOfWork.Image.GetAll()
						.Where(i => i.ProductId == productId && i.Id != imageId && i.DeletedAt == null)
						.FirstOrDefaultAsync();

					if (nextMainImage != null)
					{
						nextMainImage.IsMain = true;
						_unitOfWork.Image.Update(nextMainImage);
					}
				}

				await _adminOpreationServices.AddAdminOpreationAsync(
					$"Remove Image {imageId} from Product {productId}",
					Opreations.DeleteOpreation,
					userId,
					productId
				);

				await _unitOfWork.CommitAsync();

				_logger.LogInformation($"Image {imageId} removed from product {productId}");

				return Result<bool>.Ok(true, "Image removed", 200);
			}
			catch (Exception ex)
			{
				await transaction.RollbackAsync();
				_logger.LogError(ex, $"Error in RemoveProductImageAsync for productId: {productId}, imageId: {imageId}");
				await _errorNotificationService.SendErrorNotificationAsync(ex.Message, ex.StackTrace);
				return Result<bool>.Fail("Unexpected error while removing image", 500);
			}
		}


		public async Task<Result<bool>> UploadAndSetMainImageAsync(int productId, IFormFile mainImage, string userId)
		{
			_logger.LogInformation($"Setting new main image for product: {productId}");

				var product = await _unitOfWork.Product.GetByIdAsync(productId);
				if (product == null)
					return Result<bool>.Fail("Product not found", 404);

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
				var saveResult = await _imagesServices.SaveMainProductImageAsync(mainImage, userId);
				if (!saveResult.Success || saveResult.Data == null)
				{
					await transaction.RollbackAsync();
					var errorMsg = !string.IsNullOrWhiteSpace(saveResult.Message)
						? saveResult.Message
						: "Image saving failed. No image returned.";
					return Result<bool>.Fail(errorMsg, 400);
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
				_logger.LogInformation($"Main image set successfully for product {productId}");
				return Result<bool>.Ok(true, "Main image updated", 200);
			}
			catch (Exception ex)
			{
				await transaction.RollbackAsync();
				_logger.LogError(ex, $"Error in AddMainImageAsync for productId: {productId}");
				await _errorNotificationService.SendErrorNotificationAsync(ex.Message, ex.StackTrace);
				return Result<bool>.Fail("Error setting main image", 500);
			}
		}

	
	}
} 