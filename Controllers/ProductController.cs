using E_Commerce.DtoModels;
using E_Commerce.DtoModels.CategoryDtos;
using E_Commerce.DtoModels.DiscoutDtos;
using E_Commerce.DtoModels.ProductDtos;
using E_Commerce.Enums;
using E_Commerce.Services;
using E_Commerce.Models;
using E_Commerce.UOW;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System.Security.Claims;
using E_Commerce.Services.Product;
using E_Commerce.Interfaces;
using E_Commerce.ErrorHnadling;
using E_Commerce.DtoModels.Responses;
using E_Commerce.Services.ProductServices;
using E_Commerce.DtoModels.ImagesDtos;

namespace E_Commerce.Controllers
{
	[Route("api/[controller]")]
	[ApiController]
	[Authorize(Roles = "Admin")]
	public class ProductController : ControllerBase
	{
		private readonly IProductsServices _productsServices;
		private readonly ILogger<ProductController> _logger;
		public ProductController(IProductsServices productsServices, ILogger<ProductController> logger)
		{
			_productsServices = productsServices;
			_logger = logger;
		}

		private ActionResult<ApiResponse<T>> HandleResult<T>(Result<T> result, string actionName = null, int? id = null) 
		{
			var apiResponse = result.Success
				? ApiResponse<T>.CreateSuccessResponse(result.Message, result.Data, result.StatusCode, warnings: result.Warnings)
				: ApiResponse<T>.CreateErrorResponse(result.Message, new ErrorResponse("Error", result.Message), result.StatusCode, warnings: result.Warnings);

			switch (result.StatusCode)
			{
				case 200:
					return Ok(apiResponse);
				case 201:
					return actionName != null && id.HasValue ? CreatedAtAction(actionName, new { id }, apiResponse) : StatusCode(201, apiResponse);
				case 400:
					return BadRequest(apiResponse);
				case 401:
					return Unauthorized(apiResponse);
				case 404:
					return NotFound(apiResponse);
				case 409:
					return Conflict(apiResponse);
				default:
					return StatusCode(result.StatusCode, apiResponse);
			}
		}

		[HttpGet("admin/{id}")]
		[Authorize(Roles = "Admin")]
		public async Task<ActionResult<ApiResponse<ProductDetailDto>>> GetProductAdmin(
			int id,
			[FromQuery] bool? isActive = null,
			[FromQuery] bool? includeDeleted = null)
		{
			_logger.LogInformation($"Executing {nameof(GetProductAdmin)} for ID: {id}");
			var response = await _productsServices.GetProductByIdAsync(id, isActive, includeDeleted);
			return HandleResult<ProductDetailDto>(response, nameof(GetProductAdmin), id);
		}

		[HttpGet("public/{id}")]
		[AllowAnonymous]
		public async Task<ActionResult<ApiResponse<ProductDetailDto>>> GetProductPublic(int id)
		{
			_logger.LogInformation($"Executing {nameof(GetProductPublic)} for ID: {id}");
			var response = await _productsServices.GetProductByIdAsync(id, isActive: true, deletedOnly: false);
			return HandleResult<ProductDetailDto>(response, nameof(GetProductPublic), id);
		}

		[HttpPost]
		public async Task<ActionResult<ApiResponse<ProductDto>>> CreateProduct(CreateProductDto model)
		{
			_logger.LogInformation($"Executing {nameof(CreateProduct)}");
			if (!ModelState.IsValid)
			{
				var errors = string.Join(", ", ModelState.Values
					.SelectMany(v => v.Errors)
					.Select(e => e.ErrorMessage)
					.ToList());
				_logger.LogError($"Validation Errors: {errors}");
				return BadRequest(ApiResponse<ProductDto>.CreateErrorResponse("Check on data", new ErrorResponse("Invalid data", errors)));
			}
			var userId = HttpContext.Items["UserId"]?.ToString();
			var response = await _productsServices.CreateProductAsync(model, userId);
			return HandleResult<ProductDto>(response, nameof(CreateProduct), response.Data?.Id);
		}
		[HttpPut("{id}")]
		public async Task<ActionResult<ApiResponse<ProductDto>>> UpdateProduct(int id, UpdateProductDto model)
		{
			_logger.LogInformation($"Executing {nameof(UpdateProduct)} for ID: {id}");
			if (!ModelState.IsValid)
			{
				var errors = string.Join(", ", ModelState.Values
					.SelectMany(v => v.Errors)
					.Select(e => e.ErrorMessage)
					.ToList());
				_logger.LogError($"Validation Errors: {errors}");
				return BadRequest(ApiResponse<ProductDto>.CreateErrorResponse("", new ErrorResponse("Invalid data", errors)));
			}
			var userId = HttpContext.Items["UserId"]?.ToString();
			var response = await _productsServices.UpdateProductAsync(id, model, userId);
			return HandleResult<ProductDto>(response, nameof(UpdateProduct), id);
		}

		[HttpDelete("{id}")]
		[ActionName(nameof(DeleteProduct))]
		public async Task<ActionResult<ApiResponse<bool>>> DeleteProduct(int id)
		{
			_logger.LogInformation($"Executing {nameof(DeleteProduct)} for ID: {id}");
			var userId = HttpContext.Items["UserId"]?.ToString();
			var response = await _productsServices.DeleteProductAsync(id, userId);
			return HandleResult<bool>(response, nameof(DeleteProduct), id);
		}

		[HttpGet("subcategory/{subCategoryId}")]
		[AllowAnonymous]
		public async Task<ActionResult<ApiResponse<List<ProductDto>>>> GetProductsBySubCategoryId(
			int subCategoryId,
			[FromQuery] bool? isActive,
			[FromQuery] bool? deletedOnly)
		{
			var response = await _productsServices.GetProductsBySubCategoryId(subCategoryId, isActive, deletedOnly);

			return HandleResult(response, nameof(GetProductsBySubCategoryId), subCategoryId);
		}

		[HttpPatch("{id}/restore")]
		public async Task<ActionResult<ApiResponse<ProductDto>>> RestoreProductAsync(int id)
		{
			if (!ModelState.IsValid)
			{
				var errors = string.Join(", ", ModelState.Values
					.SelectMany(v => v.Errors)
					.Select(e => e.ErrorMessage)
					.ToList());
				_logger.LogError($"Validation Errors: {errors}");
				return BadRequest(ApiResponse<ProductDto>.CreateErrorResponse("", new ErrorResponse("Invalid data", errors)));
			}
			var userId = HttpContext.Items["UserId"]?.ToString();
			var result = await _productsServices.RestoreProductAsync(id, userId);
			return HandleResult(result, nameof(RestoreProductAsync), id);
		}

		[HttpGet("bestsellers")]
		public async Task<ActionResult<ApiResponse<List<ProductDto>>>> GetBestSellers([FromQuery] int page = 1, [FromQuery] int pageSize = 10)
		{
			var response = await _productsServices.GetBestSellersAsync(page, pageSize);
			return HandleResult(response, nameof(GetBestSellers));
		}

		[HttpGet("newarrivals")]
		public async Task<ActionResult<ApiResponse<List<ProductDto>>>> GetNewArrivals([FromQuery] int page = 1, [FromQuery] int pageSize = 10)
		{
			var response = await _productsServices.GetNewArrivalsAsync(page, pageSize);
			return HandleResult(response, nameof(GetNewArrivals));
		}

		[HttpPost("advanced-search")]
		public async Task<ActionResult<ApiResponse<List<ProductDto>>>> AdvancedSearch([FromBody] AdvancedSearchDto searchDto, [FromQuery] int page = 1, [FromQuery] int pageSize = 10)
		{

			if(!ModelState.IsValid)
			{
				var errors = string.Join(", ", ModelState.Values
					.SelectMany(v => v.Errors)
					.Select(e => e.ErrorMessage)
					.ToList());
				_logger.LogError($"Validation Errors: {errors}");
				return BadRequest(ApiResponse<List<ProductDto>>.CreateErrorResponse("Invalid search criteria", new ErrorResponse("Invalid data", errors)));
			}

			var response = await _productsServices.AdvancedSearchAsync(searchDto, page, pageSize);
			return HandleResult(response, nameof(AdvancedSearch));
		}

		[HttpGet("{id}/images")]
		public async Task<ActionResult<ApiResponse<List<ImageDto>>>> GetProductImages(int id)
		{
			var response = await _productsServices.GetProductImagesAsync(id);
			return HandleResult(response, nameof(GetProductImages), id);
		}

		[HttpPost("{id}/images")]
		public async Task<ActionResult<ApiResponse<List<ImageDto>>>> AddProductImages(int id, [FromForm] List<IFormFile> images)
		{
			if (!ModelState.IsValid || images == null || !images.Any())
			{
				var errors = string.Join(", ", ModelState.Values
					.SelectMany(v => v.Errors)
					.Select(e => e.ErrorMessage)
					.ToList());
				_logger.LogError($"Validation Errors: {errors}");
				return BadRequest(ApiResponse<List<ImageDto>>.CreateErrorResponse("Invalid image data", new ErrorResponse("Invalid data", errors ?? "No images provided")));
			}
			var userId = HttpContext.Items["UserId"]?.ToString();
			var response = await _productsServices.AddProductImagesAsync(id, images, userId);
			return HandleResult(response, nameof(AddProductImages), id);
		}

		[HttpDelete("{id}/images/{imageId}")]
		public async Task<ActionResult<ApiResponse<bool>>> RemoveProductImage(int id, int imageId)
		{
			var userId = HttpContext.Items["UserId"]?.ToString();
			var response = await _productsServices.RemoveProductImageAsync(id, imageId, userId);
			return HandleResult(response, nameof(RemoveProductImage), id);
		}

		[HttpPost("{id}/main-image")]
		public async Task<ActionResult<ApiResponse<ImageDto>>> UploadAndSetMainImage(int id, [FromForm] CreateImageDto mainImage)
		{
			if (!ModelState.IsValid || mainImage?.Files == null || !mainImage.Files.Any())
			{
				var errors = string.Join(", ", ModelState.Values
					.SelectMany(v => v.Errors)
					.Select(e => e.ErrorMessage)
					.ToList());
				_logger.LogError($"Validation Errors: {errors}");
				return BadRequest(ApiResponse<bool>.CreateErrorResponse("Invalid image data", new ErrorResponse("Invalid data", errors ?? "No main image provided")));
			}

			var userId = HttpContext.Items["UserId"]?.ToString();
			var response = await _productsServices.UploadAndSetMainImageAsync(id, mainImage.Files.First(), userId);
			return HandleResult(response, nameof(UploadAndSetMainImage), id);
		}

		[HttpGet("{id}/variants")]
		public async Task<ActionResult<ApiResponse<List<ProductVariantDto>>>> GetProductVariants(int id)
		{
			var response = await _productsServices.GetProductVariantsAsync(id);
			return HandleResult(response, nameof(GetProductVariants), id);
		}

		
		


		[HttpGet("{id}/discount")]
		public async Task<ActionResult<ApiResponse<DiscountDto>>> GetProductDiscount(int id)
		{
			var response = await _productsServices.GetProductDiscountAsync(id);
			return HandleResult(response, nameof(GetProductDiscount), id);
		}

		[HttpPost("{id}/discount")]
		public async Task<ActionResult<ApiResponse<ProductDetailDto>>> AddDiscountToProduct(int id, [FromBody] int discountId)
		{
			if (!ModelState.IsValid || discountId <= 0)
			{
				var errors = string.Join(", ", ModelState.Values
					.SelectMany(v => v.Errors)
					.Select(e => e.ErrorMessage)
					.ToList());
				_logger.LogError($"Validation Errors: {errors}");
				return BadRequest(ApiResponse<ProductDetailDto>.CreateErrorResponse("Invalid discount data", new ErrorResponse("Invalid data", errors ?? "Invalid discount ID")));
			}
			var userId = HttpContext.Items["UserId"]?.ToString();
			var response = await _productsServices.AddDiscountToProductAsync(id, discountId, userId);
			return HandleResult(response, nameof(AddDiscountToProduct), id);
		}

		[HttpPut("{id}/discount")]
		public async Task<ActionResult<ApiResponse<ProductDetailDto>>> UpdateProductDiscount(int id, [FromBody] int discountId)
		{
			if (!ModelState.IsValid || discountId <= 0)
			{
				var errors = string.Join(", ", ModelState.Values
					.SelectMany(v => v.Errors)
					.Select(e => e.ErrorMessage)
					.ToList());
				_logger.LogError($"Validation Errors: {errors}");
				return BadRequest(ApiResponse<ProductDetailDto>.CreateErrorResponse("Invalid discount data", new ErrorResponse("Invalid data", errors ?? "Invalid discount ID")));
			}
			var userId = HttpContext.Items["UserId"]?.ToString();
			var response = await _productsServices.UpdateProductDiscountAsync(id, discountId, userId);
			return HandleResult(response, nameof(UpdateProductDiscount), id);
		}

		[HttpDelete("{id}/discount")]
		public async Task<ActionResult<ApiResponse<ProductDetailDto>>> RemoveDiscountFromProduct(int id)
		{
			var userId = HttpContext.Items["UserId"]?.ToString();
			var response = await _productsServices.RemoveDiscountFromProductAsync(id, userId);
			return HandleResult(response, nameof(RemoveDiscountFromProduct), id);
		}

		[HttpGet("admin/list")]
		[Authorize(Roles = "Admin")]
		public async Task<ActionResult<ApiResponse<List<ProductDto>>>> GetProductsForAdmin(
			[FromQuery] bool? isActive = null,
			[FromQuery] bool? includeDeleted = null,
			[FromQuery] int page = 1,
			[FromQuery] int pageSize = 10)
		{
			var response = await _productsServices.AdvancedSearchAsync(
				new AdvancedSearchDto(), page, pageSize, isActive, includeDeleted);
			return HandleResult(response, nameof(GetProductsForAdmin));
		}

		[HttpGet("public/list")]
		[AllowAnonymous]
		public async Task<ActionResult<ApiResponse<List<ProductDto>>>> GetProductsForPublic(
			[FromQuery] int page = 1,
			[FromQuery] int pageSize = 10)
		{
			var response = await _productsServices.AdvancedSearchAsync(
				new AdvancedSearchDto(), page, pageSize, isActive: true, deletedOnly: false);
			return HandleResult(response, nameof(GetProductsForPublic));
		}

		[HttpGet("admin/subcategory/{subCategoryId}")]
		[Authorize(Roles = "Admin")]
		public async Task<ActionResult<ApiResponse<List<ProductDto>>>> GetProductsBySubCategoryAdmin(
			int subCategoryId,
			[FromQuery] bool? isActive = null,
			[FromQuery] bool? includeDeleted = null,
			[FromQuery] int page = 1,
			[FromQuery] int pageSize = 10)
		{
			// If your service supports filtering by isActive/includeDeleted for subcategory, pass them; otherwise, just call as is
			var response = await _productsServices.AdvancedSearchAsync(
				new AdvancedSearchDto { Subcategoryid = subCategoryId }, page, pageSize, isActive, includeDeleted);
			return HandleResult(response, nameof(GetProductsBySubCategoryAdmin), subCategoryId);
		}

		[HttpGet("public/subcategory/{subCategoryId}")]
		[AllowAnonymous]
		public async Task<ActionResult<ApiResponse<List<ProductDto>>>> GetProductsBySubCategoryPublic(
			int subCategoryId,
			[FromQuery] int page = 1,
			[FromQuery] int pageSize = 10)
		{
			var response = await _productsServices.AdvancedSearchAsync(
				new AdvancedSearchDto { Subcategoryid = subCategoryId }, page, pageSize, isActive: true, deletedOnly: false);
			return HandleResult(response, nameof(GetProductsBySubCategoryPublic), subCategoryId);
		}

		[HttpGet("admin/bestsellers")]
		[Authorize(Roles = "Admin")]
		public async Task<ActionResult<ApiResponse<List<ProductDto>>>> GetBestSellersAdmin(
			[FromQuery] int page = 1,
			[FromQuery] int pageSize = 10,
			[FromQuery] bool? isActive = null,
			[FromQuery] bool? includeDeleted = null)
		{
			var response = await _productsServices.GetBestSellersAsync(page, pageSize, isActive, includeDeleted);
			return HandleResult(response, nameof(GetBestSellersAdmin));
		}

		[HttpGet("public/bestsellers")]
		[AllowAnonymous]
		public async Task<ActionResult<ApiResponse<List<ProductDto>>>> GetBestSellersPublic(
			[FromQuery] int page = 1,
			[FromQuery] int pageSize = 10)
		{
			var response = await _productsServices.GetBestSellersAsync(page, pageSize, isActive: true, deletedOnly: false);
			return HandleResult(response, nameof(GetBestSellersPublic));
		}

		[HttpGet("admin/newarrivals")]
		[Authorize(Roles = "Admin")]
		public async Task<ActionResult<ApiResponse<List<ProductDto>>>> GetNewArrivalsAdmin(
			[FromQuery] int page = 1,
			[FromQuery] int pageSize = 10,
			[FromQuery] bool? isActive = null,
			[FromQuery] bool? includeDeleted = null)
		{
			var response = await _productsServices.GetNewArrivalsAsync(page, pageSize, isActive, includeDeleted);
			return HandleResult(response, nameof(GetNewArrivalsAdmin));
		}

		[HttpGet("public/newarrivals")]
		[AllowAnonymous]
		public async Task<ActionResult<ApiResponse<List<ProductDto>>>> GetNewArrivalsPublic(
			[FromQuery] int page = 1,
			[FromQuery] int pageSize = 10)
		{
			var response = await _productsServices.GetNewArrivalsAsync(page, pageSize, isActive: true, deletedOnly: false);
			return HandleResult(response, nameof(GetNewArrivalsPublic));
		}
		[HttpPatch("{id}/activate")]
		public async Task<ActionResult<ApiResponse<bool>>> ActiveProduct(int id)
		{
			string userId = HttpContext.Items["UserId"]?.ToString();
			var response = await _productsServices.ActivateProductAsync(id, userId);
			return HandleResult(response, nameof(ActiveProduct), id);
		}

		[HttpPatch("{id}/deactivate")]
		public async Task<ActionResult<ApiResponse<bool>>> DeActiveProduct(int id)
		{
			string userId = HttpContext.Items["UserId"]?.ToString();
			var response = await _productsServices.DeactivateProductAsync(id, userId);
			return HandleResult(response, nameof(DeActiveProduct), id);
		}



		[HttpPost("admin/advanced-search")]
		[Authorize(Roles = "Admin")]
		public async Task<ActionResult<ApiResponse<List<ProductDto>>>> AdvancedSearchAdmin(
			[FromBody] AdvancedSearchDto searchDto,
			[FromQuery] int page = 1,
			[FromQuery] int pageSize = 10,
			[FromQuery] bool? isActive = null,
			[FromQuery] bool? includeDeleted = null)
		{


			if (!ModelState.IsValid)
			{
				var errors = string.Join(", ", ModelState.Values
					.SelectMany(v => v.Errors)
					.Select(e => e.ErrorMessage)
					.ToList());
				_logger.LogError($"Validation Errors: {errors}");
				return BadRequest(ApiResponse<List<ProductDto>>.CreateErrorResponse("Invalid search criteria", new ErrorResponse("Invalid data", errors)));
			}
			var response = await _productsServices.AdvancedSearchAsync(searchDto, page, pageSize, isActive, includeDeleted);
			return HandleResult(response, nameof(AdvancedSearchAdmin));
		}

		[HttpPost("public/advanced-search")]
		[AllowAnonymous]
		public async Task<ActionResult<ApiResponse<List<ProductDto>>>> AdvancedSearchPublic(
			[FromBody] AdvancedSearchDto searchDto,
			[FromQuery] int page = 1,
			[FromQuery] int pageSize = 10)
		{
			if (!ModelState.IsValid)
			{
				var errors = string.Join(", ", ModelState.Values
					.SelectMany(v => v.Errors)
					.Select(e => e.ErrorMessage)
					.ToList());
				_logger.LogError($"Validation Errors: {errors}");
				return BadRequest(ApiResponse<List<ProductDto>>.CreateErrorResponse("Invalid search criteria", new ErrorResponse("Invalid data", errors)));
			}
			var response = await _productsServices.AdvancedSearchAsync(searchDto, page, pageSize, isActive: true, deletedOnly: false);
			return HandleResult(response, nameof(AdvancedSearchPublic));
		}
	}
}