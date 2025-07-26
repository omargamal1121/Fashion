using E_Commerce.DtoModels.CategoryDtos;
using E_Commerce.DtoModels.CollectionDtos;
using E_Commerce.DtoModels.ImagesDtos;
using E_Commerce.DtoModels.Responses;
using E_Commerce.ErrorHnadling;
using E_Commerce.Interfaces;
using E_Commerce.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using System.Threading.Tasks;

namespace E_Commerce.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize] // Secure all endpoints by default
    public class CollectionController : ControllerBase
    {
        private readonly ICollectionServices _collectionServices;
        private readonly ILogger<CollectionController> _logger;

        public CollectionController(ICollectionServices collectionServices, ILogger<CollectionController> logger)
        {
            _collectionServices = collectionServices ?? throw new ArgumentNullException(nameof(collectionServices));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        private string GetUserId() => User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? string.Empty;

        private ActionResult<ApiResponse<T>> HandleResult<T>(Result<T> result, string? actionName = null, object? routeValues = null)
        {
            var apiResponse = result.Success
                ? ApiResponse<T>.CreateSuccessResponse(result.Message, result.Data, result.StatusCode, warnings: result.Warnings)
                : ApiResponse<T>.CreateErrorResponse(result.Message, new ErrorResponse("Error", result.Message), result.StatusCode, warnings: result.Warnings);

            return result.StatusCode switch
            {
                200 => Ok(apiResponse),
                201 => actionName != null 
                    ? CreatedAtAction(actionName, routeValues, apiResponse) 
                    : StatusCode(201, apiResponse),
                400 => BadRequest(apiResponse),
                401 => Unauthorized(apiResponse),
                403 => Forbid(),
                404 => NotFound(apiResponse),
                409 => Conflict(apiResponse),
                _ => StatusCode(result.StatusCode, apiResponse)
            };
        }
          

      

        /// <summary>
        /// Get collection by ID for users (only active and not deleted)
        /// </summary>
        [HttpGet("public/{id}")]
        [AllowAnonymous]
        public async Task<ActionResult<ApiResponse<CollectionDto>>> GetCollectionByIdPublic(int id)
        {
            _logger.LogInformation($"Executing {nameof(GetCollectionByIdPublic)} for ID: {id}");
            var result = await _collectionServices.GetCollectionByIdAsync(id, true, false);
            return HandleResult(result);
        }

        /// <summary>
        /// Get collection by ID for admin (with optional filters)
        /// </summary>
        [HttpGet("admin/{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult<ApiResponse<CollectionDto>>> GetCollectionByIdAdmin(
            int id,
            [FromQuery] bool? isActive = null,
            [FromQuery] bool? isDeleted = null)
        {
            _logger.LogInformation($"Executing {nameof(GetCollectionByIdAdmin)} for ID: {id}");
            var result = await _collectionServices.GetCollectionByIdAsync(id, isActive, isDeleted);
            return HandleResult(result);
        }

        /// <summary>
        /// Get all collections for users (only active and not deleted)
        /// </summary>
        [HttpGet("public")]
        [AllowAnonymous]
        public async Task<ActionResult<ApiResponse<List<CollectionSummaryDto>>>> GetCollectionsPublic(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 10)
        {
            _logger.LogInformation($"Executing {nameof(GetCollectionsPublic)} (user endpoint) with pagination: page {page}, size {pageSize}");
            var result = await _collectionServices.SearchCollectionsAsync(null, true, false, page, pageSize);
            return HandleResult(result);
        }

        /// <summary>
        /// Search collections for users (only active and not deleted)
        /// </summary>
        [HttpGet("public/search")]
        [AllowAnonymous]
        public async Task<ActionResult<ApiResponse<List<CollectionSummaryDto>>>> SearchCollectionsPublic(
            [FromQuery] string searchTerm,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 10)
        {
            _logger.LogInformation($"Executing {nameof(SearchCollectionsPublic)} for term: {searchTerm} with pagination: page {page}, size {pageSize}");
            var result = await _collectionServices.SearchCollectionsAsync(searchTerm, true, false, page, pageSize);
            return HandleResult(result);
        }

        /// <summary>
        /// Get all collections for admin (with optional filters)
        /// </summary>
        /// 
        [HttpGet("admin/GetAll")]
  
        public async Task<ActionResult<ApiResponse<List<CollectionSummaryDto>>>> GetCollectionsAdmin(
            [FromQuery] bool? isActive = null,
            [FromQuery] bool? isDeleted = null,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 10)
        {
            _logger.LogInformation($"Executing {nameof(GetCollectionsAdmin)} (admin endpoint) with pagination: page {page}, size {pageSize}");
            var result = await _collectionServices.SearchCollectionsAsync(null, isActive, isDeleted, page, pageSize);
            return HandleResult(result);
        }

        /// <summary>
        /// Search collections for admin (with filters)
        /// </summary>
        [HttpGet("admin/search")]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult<ApiResponse<List<CollectionSummaryDto>>>> SearchCollectionsAdmin(
            [FromQuery] string searchTerm,
            [FromQuery] bool? isActive = null,
            [FromQuery] bool? isDeleted = null,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 10)
        {
            _logger.LogInformation($"Executing {nameof(SearchCollectionsAdmin)} for term: {searchTerm} with pagination: page {page}, size {pageSize}");
            var result = await _collectionServices.SearchCollectionsAsync(searchTerm, isActive, isDeleted, page, pageSize);
            return HandleResult(result);
        }

  


        /// <summary>
        /// Activate a collection
        /// </summary>
        [HttpPatch("{id}/activate")]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult<ApiResponse<bool>>> ActivateCollection(int id)
        {
            var userId = GetUserId();
            var result = await _collectionServices.ActivateCollectionAsync(id, userId);
            return HandleResult(result);
        }

        /// <summary>
        /// Deactivate a collection
        /// </summary>
        [HttpPatch("{id}/deactivate")]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult<ApiResponse<bool>>> DeactivateCollection(int id)
        {
            var userId = GetUserId();
            var result = await _collectionServices.DeactivateCollectionAsync(id, userId);
            return HandleResult(result);
        }

        /// <summary>
        /// Add images to a collection
        /// </summary>
        [HttpPost("{id}/images")]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult<ApiResponse<List<ImageDto>>>> AddImagesToCollection(
            int id,
         [FromForm]  AddImagesDto images)
        {
			if (!ModelState.IsValid)
			{
				var errors = GetModelErrors();
				_logger.LogWarning($"ModelState errors: {string.Join(", ", errors)}");
				return BadRequest(ApiResponse<CollectionDto>.CreateErrorResponse("Invalid Data", new ErrorResponse("Invalid Data", errors), 400));
			}
			var userId = GetUserId();
            var result = await _collectionServices.AddImagesToCollectionAsync(id, images.Images, userId);
            return HandleResult(result);
        }

        /// <summary>
        /// Add main image to a collection
        /// </summary>
        [HttpPost("{id}/main-image")]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult<ApiResponse<ImageDto>>> AddMainImageToCollection(
            int id,
          AddMainImageDto image)
        {
			if (!ModelState.IsValid)
			{
				var errors = GetModelErrors();
				_logger.LogWarning($"ModelState errors: {string.Join(", ", errors)}");
				return BadRequest(ApiResponse<CollectionDto>.CreateErrorResponse("Invalid Data", new ErrorResponse("Invalid Data", errors), 400));
			}
			var userId = GetUserId();
            var result = await _collectionServices.AddMainImageToCollectionAsync(id, image.Image, userId);
            return HandleResult(result);
        }

        /// <summary>
        /// Remove image from a collection
        /// </summary>
        [HttpDelete("{id}/images/{imageId}")]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult<ApiResponse<bool>>> RemoveImageFromCollection(
            int id,
            int imageId)
        {
			if (!ModelState.IsValid)
			{
				var errors = GetModelErrors();
				_logger.LogWarning($"ModelState errors: {string.Join(", ", errors)}");
				return BadRequest(ApiResponse<CollectionDto>.CreateErrorResponse("Invalid Data", new ErrorResponse("Invalid Data", errors), 400));
			}
			var userId = GetUserId();
            var result = await _collectionServices.RemoveImageFromCollectionAsync(id, imageId, userId);
            return HandleResult(result);
        }



 

        /// <summary>
        /// Get collection by ID
        /// </summary>
        [HttpGet("{id}")]
        public async Task<ActionResult<ApiResponse<CollectionDto>>> GetCollectionById(int id)
        {
            try
            {
                _logger.LogInformation($"Executing GetCollectionById for ID: {id}");
                var result = await _collectionServices.GetCollectionByIdAsync(id);
                return HandleResult(result, nameof(GetCollectionById), id);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error in GetCollectionById: {ex.Message}");
                return StatusCode(500, ApiResponse<CollectionDto>.CreateErrorResponse("Server Error", new ErrorResponse("Server Error", "An error occurred while retrieving the collection"), 500));
            }
        }

		[HttpGet("admin")]
		[Authorize(Roles = "Admin")]
		public async Task<ActionResult<ApiResponse<List<CollectionSummaryDto>>>> GetCollections(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 10,
            [FromQuery] bool? isActive = null,
			[FromQuery] bool? isDeleted = null)
        {
            try
            {
                _logger.LogInformation($"Executing GetCollections with pagination: page {page}, size {pageSize}, active: {isActive}");
                var result = await _collectionServices.SearchCollectionsAsync(null, isActive, isDeleted,page, pageSize);
                return HandleResult(result, nameof(GetCollections));
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error in GetCollections: {ex.Message}");
                return StatusCode(500, ApiResponse<List<CollectionSummaryDto>>.CreateErrorResponse("Server Error", new ErrorResponse("Server Error", "An error occurred while retrieving collections"), 500));
            }
        }

    
     
       

        // Admin-only endpoints
        /// <summary>
        /// Create collection (Admin only)
        /// </summary>
        [HttpPost]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult<ApiResponse<CollectionSummaryDto>>> CreateCollection([FromBody] CreateCollectionDto collectionDto)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    var errors = GetModelErrors();
                    _logger.LogWarning($"ModelState errors: {string.Join(", ", errors)}");
                    return BadRequest(ApiResponse<CollectionDto>.CreateErrorResponse("Invalid Data", new ErrorResponse("Invalid Data", errors), 400));
                }

                _logger.LogInformation($"Executing CreateCollection: {collectionDto.Name}");
                var userid = HttpContext.Items["UserId"].ToString();
                var result = await _collectionServices.CreateCollectionAsync(collectionDto, userid);
                return HandleResult(result, nameof(CreateCollection));
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error in CreateCollection: {ex.Message}");
                return StatusCode(500, ApiResponse<CollectionDto>.CreateErrorResponse("Server Error", new ErrorResponse("Server Error", "An error occurred while creating the collection"), 500));
            }
        }

		private List<string> GetModelErrors()
		{
			return ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList();
		}

		/// <summary>
		/// Update collection (Admin only)
		/// </summary>
		[HttpPut("{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult<ApiResponse<CollectionSummaryDto>>> UpdateCollection(
            int id,
            [FromBody] UpdateCollectionDto collectionDto)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    var errors = GetModelErrors();
                    _logger.LogWarning($"ModelState errors: {string.Join(", ", errors)}");
                    return BadRequest(ApiResponse<CollectionDto>.CreateErrorResponse("Invalid Data", new ErrorResponse("Invalid Data", errors), 400));
                }

                _logger.LogInformation($"Executing UpdateCollection for ID: {id}");
                var userid = HttpContext.Items["UserId"].ToString();
				var result = await _collectionServices.UpdateCollectionAsync(id, collectionDto, userid);
                return HandleResult(result, nameof(UpdateCollection), id);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error in UpdateCollection: {ex.Message}");
                return StatusCode(500, ApiResponse<CollectionDto>.CreateErrorResponse("Server Error", new ErrorResponse("Server Error", "An error occurred while updating the collection"), 500));
            }
        }

        /// <summary>
        /// Delete collection (Admin only)
        /// </summary>
        [HttpDelete("{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult<ApiResponse<bool>>> DeleteCollection(int id)
        {
            try
            {
                _logger.LogInformation($"Executing DeleteCollection for ID: {id}");
               var userid = HttpContext.Items["UserId"].ToString();
                var result = await _collectionServices.DeleteCollectionAsync(id, userid);
                return HandleResult(result, nameof(DeleteCollection), id);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error in DeleteCollection: {ex.Message}");
                return StatusCode(500, ApiResponse<string>.CreateErrorResponse("Server Error", new ErrorResponse("Server Error", "An error occurred while deleting the collection"), 500));
            }
        }

        /// <summary>
        /// Add products to collection (Admin only)
        /// </summary>
        [HttpPost("{id}/products")]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult<ApiResponse<bool>>> AddProductsToCollection(
            int id,
            [FromForm] AddProductsToCollectionDto productsDto)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    var errors = GetModelErrors();
                    _logger.LogWarning($"ModelState errors: {string.Join(", ", errors)}");
                    return BadRequest(ApiResponse<bool>.CreateErrorResponse("Invalid Data", new ErrorResponse("Invalid Data", errors), 400));
                }

                _logger.LogInformation($"Executing AddProductsToCollection for collection ID: {id}");
               var userid = HttpContext.Items["UserId"].ToString();
                var result = await _collectionServices.AddProductsToCollectionAsync(id, productsDto, userid);
                return HandleResult(result, nameof(AddProductsToCollection), id);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error in AddProductsToCollection: {ex.Message}");
                return StatusCode(500, ApiResponse<bool>.CreateErrorResponse("Server Error", new ErrorResponse("Server Error", "An error occurred while adding products to collection"), 500));
            }
        }

        /// <summary>
        /// Remove products from collection (Admin only)
        /// </summary>
        [HttpDelete("{id}/products")]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult<ApiResponse<bool>>> RemoveProductsFromCollection(
            int id,
            [FromBody] RemoveProductsFromCollectionDto productsDto)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    var errors = GetModelErrors();
                    _logger.LogWarning($"ModelState errors: {string.Join(", ", errors)}");
                    return BadRequest(ApiResponse<string>.CreateErrorResponse("Invalid Data", new ErrorResponse("Invalid Data", errors), 400));
                }

                _logger.LogInformation($"Executing RemoveProductsFromCollection for collection ID: {id}");
               var userid = HttpContext.Items["UserId"].ToString();
                var result = await _collectionServices.RemoveProductsFromCollectionAsync(id, productsDto, userid);
                return HandleResult(result, nameof(RemoveProductsFromCollection), id);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error in RemoveProductsFromCollection: {ex.Message}");
                return StatusCode(500, ApiResponse<string>.CreateErrorResponse("Server Error", new ErrorResponse("Server Error", "An error occurred while removing products from collection"), 500));
            }
        }

     
        /// <summary>
        /// Update collection display order (Admin only)
        /// </summary>
        [HttpPatch("{id}/display-order")]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult<ApiResponse<bool>>> UpdateCollectionDisplayOrder(
            int id,
            [FromBody] int displayOrder)
        {
            try
            {
                _logger.LogInformation($"Executing UpdateCollectionDisplayOrder for ID: {id}, order: {displayOrder}");
               var userid = HttpContext.Items["UserId"].ToString();
                var result = await _collectionServices.UpdateCollectionDisplayOrderAsync(id, displayOrder, userid);
                return HandleResult(result, nameof(UpdateCollectionDisplayOrder), id);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error in UpdateCollectionDisplayOrder: {ex.Message}");
                return StatusCode(500, ApiResponse<bool>.CreateErrorResponse("Server Error", new ErrorResponse("Server Error", "An error occurred while updating collection display order"), 500));
            }
        }
    }
} 