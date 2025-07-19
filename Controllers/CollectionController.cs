using E_Commers.DtoModels.CollectionDtos;
using E_Commers.DtoModels.Responses;
using E_Commers.ErrorHnadling;
using E_Commers.Interfaces;
using E_Commers.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace E_Commers.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class CollectionController : ControllerBase
    {
        private readonly ICollectionServices _collectionServices;
        private readonly ILogger<CollectionController> _logger;

        public CollectionController(ICollectionServices collectionServices, ILogger<CollectionController> logger)
        {
            _collectionServices = collectionServices;
            _logger = logger;
        }

        private string GetUserRole()
        {
            return User.FindFirst(ClaimTypes.Role)?.Value ?? "Customer";
        }

        private List<string> GetModelErrors()
        {
            return ModelState.Values
                .SelectMany(v => v.Errors)
                .Select(e => e.ErrorMessage)
                .ToList();
        }

        private ActionResult<ApiResponse<T>> HandleResult<T>(Result<T> result, string? actionName = null, int? id = null)
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

        /// <summary>
        /// Get collection by name
        /// </summary>
        [HttpGet("name/{name}")]
        public async Task<ActionResult<ApiResponse<CollectionDto>>> GetCollectionByName(string name)
        {
            try
            {
                _logger.LogInformation($"Executing GetCollectionByName for name: {name}");
                var result = await _collectionServices.GetCollectionByNameAsync(name);
                return HandleResult(result, nameof(GetCollectionByName));
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error in GetCollectionByName: {ex.Message}");
                return StatusCode(500, ApiResponse<CollectionDto>.CreateErrorResponse("Server Error", new ErrorResponse("Server Error", "An error occurred while retrieving the collection"), 500));
            }
        }

        /// <summary>
        /// Get active collections
        /// </summary>
        [HttpGet("active")]
        public async Task<ActionResult<ApiResponse<List<CollectionDto>>>> GetActiveCollections()
        {
            try
            {
                _logger.LogInformation("Executing GetActiveCollections");
                var result = await _collectionServices.GetActiveCollectionsAsync();
                return HandleResult(result, nameof(GetActiveCollections));
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error in GetActiveCollections: {ex.Message}");
                return StatusCode(500, ApiResponse<List<CollectionDto>>.CreateErrorResponse("Server Error", new ErrorResponse("Server Error", "An error occurred while retrieving active collections"), 500));
            }
        }

        /// <summary>
        /// Get collections by display order
        /// </summary>
        [HttpGet("ordered")]
        public async Task<ActionResult<ApiResponse<List<CollectionDto>>>> GetCollectionsByDisplayOrder()
        {
            try
            {
                _logger.LogInformation("Executing GetCollectionsByDisplayOrder");
                var result = await _collectionServices.GetCollectionsByDisplayOrderAsync();
                return HandleResult(result, nameof(GetCollectionsByDisplayOrder));
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error in GetCollectionsByDisplayOrder: {ex.Message}");
                return StatusCode(500, ApiResponse<List<CollectionDto>>.CreateErrorResponse("Server Error", new ErrorResponse("Server Error", "An error occurred while retrieving collections"), 500));
            }
        }

        /// <summary>
        /// Get collections with pagination
        /// </summary>
        [HttpGet]
        public async Task<ActionResult<ApiResponse<List<CollectionDto>>>> GetCollections(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 10,
            [FromQuery] bool? isActive = null)
        {
            try
            {
                _logger.LogInformation($"Executing GetCollections with pagination: page {page}, size {pageSize}, active: {isActive}");
                var result = await _collectionServices.GetCollectionsWithPaginationAsync(page, pageSize, isActive);
                return HandleResult(result, nameof(GetCollections));
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error in GetCollections: {ex.Message}");
                return StatusCode(500, ApiResponse<List<CollectionDto>>.CreateErrorResponse("Server Error", new ErrorResponse("Server Error", "An error occurred while retrieving collections"), 500));
            }
        }

        /// <summary>
        /// Get total collection count
        /// </summary>
        [HttpGet("count")]
        public async Task<ActionResult<ApiResponse<int?>>> GetCollectionCount([FromQuery] bool? isActive = null)
        {
            try
            {
                _logger.LogInformation($"Executing GetCollectionCount, active: {isActive}");
                var result = await _collectionServices.GetTotalCollectionCountAsync(isActive);
                return HandleResult(result, nameof(GetCollectionCount));
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error in GetCollectionCount: {ex.Message}");
                return StatusCode(500, ApiResponse<int>.CreateErrorResponse("Server Error", new ErrorResponse("Server Error", "An error occurred while getting collection count"), 500));
            }
        }

        /// <summary>
        /// Search collections
        /// </summary>
        [HttpGet("search")]
        public async Task<ActionResult<ApiResponse<List<CollectionDto>>>> SearchCollections([FromQuery] string searchTerm)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(searchTerm))
                {
                    return BadRequest(ApiResponse<List<CollectionDto>>.CreateErrorResponse("Invalid Data", new ErrorResponse("Invalid Data", "Search term is required"), 400));
                }

                _logger.LogInformation($"Executing SearchCollections for term: {searchTerm}");
                var result = await _collectionServices.SearchCollectionsAsync(searchTerm);
                return HandleResult(result, nameof(SearchCollections));
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error in SearchCollections: {ex.Message}");
                return StatusCode(500, ApiResponse<List<CollectionDto>>.CreateErrorResponse("Server Error", new ErrorResponse("Server Error", "An error occurred while searching collections"), 500));
            }
        }

        /// <summary>
        /// Get collections by product
        /// </summary>
        [HttpGet("product/{productId}")]
        public async Task<ActionResult<ApiResponse<List<CollectionDto>>>> GetCollectionsByProduct(int productId)
        {
            try
            {
                _logger.LogInformation($"Executing GetCollectionsByProduct for product ID: {productId}");
                var result = await _collectionServices.GetCollectionsByProductAsync(productId);
                return HandleResult(result, nameof(GetCollectionsByProduct));
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error in GetCollectionsByProduct: {ex.Message}");
                return StatusCode(500, ApiResponse<List<CollectionDto>>.CreateErrorResponse("Server Error", new ErrorResponse("Server Error", "An error occurred while retrieving collections"), 500));
            }
        }

        /// <summary>
        /// Get collection summary
        /// </summary>
        [HttpGet("{id}/summary")]
        public async Task<ActionResult<ApiResponse<CollectionSummaryDto>>> GetCollectionSummary(int id)
        {
            try
            {
                _logger.LogInformation($"Executing GetCollectionSummary for ID: {id}");
                var result = await _collectionServices.GetCollectionSummaryAsync(id);
                return HandleResult(result, nameof(GetCollectionSummary), id);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error in GetCollectionSummary: {ex.Message}");
                return StatusCode(500, ApiResponse<CollectionSummaryDto>.CreateErrorResponse("Server Error", new ErrorResponse("Server Error", "An error occurred while getting collection summary"), 500));
            }
        }

     
        [HttpGet("summaries")]
        public async Task<ActionResult<ApiResponse<List<CollectionSummaryDto>>>> GetCollectionSummaries(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 10,
            [FromQuery] bool? isActive = null)
        {
            try
            {
                _logger.LogInformation($"Executing GetCollectionSummaries with pagination: page {page}, size {pageSize}, active: {isActive}");
                var result = await _collectionServices.GetCollectionSummariesAsync(page, pageSize, isActive);
                return HandleResult(result, nameof(GetCollectionSummaries));
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error in GetCollectionSummaries: {ex.Message}");
                return StatusCode(500, ApiResponse<List<CollectionSummaryDto>>.CreateErrorResponse("Server Error", new ErrorResponse("Server Error", "An error occurred while getting collection summaries"), 500));
            }
        }

        // Admin-only endpoints
        /// <summary>
        /// Create collection (Admin only)
        /// </summary>
        [HttpPost]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult<ApiResponse<CollectionDto>>> CreateCollection([FromBody] CreateCollectionDto collectionDto)
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
                var userRole = GetUserRole();
                var result = await _collectionServices.CreateCollectionAsync(collectionDto, userRole);
                return HandleResult(result, nameof(CreateCollection));
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error in CreateCollection: {ex.Message}");
                return StatusCode(500, ApiResponse<CollectionDto>.CreateErrorResponse("Server Error", new ErrorResponse("Server Error", "An error occurred while creating the collection"), 500));
            }
        }

        /// <summary>
        /// Update collection (Admin only)
        /// </summary>
        [HttpPut("{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult<ApiResponse<CollectionDto>>> UpdateCollection(
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
                var userRole = GetUserRole();
                var result = await _collectionServices.UpdateCollectionAsync(id, collectionDto, userRole);
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
        public async Task<ActionResult<ApiResponse<string>>> DeleteCollection(int id)
        {
            try
            {
                _logger.LogInformation($"Executing DeleteCollection for ID: {id}");
                var userRole = GetUserRole();
                var result = await _collectionServices.DeleteCollectionAsync(id, userRole);
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
        public async Task<ActionResult<ApiResponse<string>>> AddProductsToCollection(
            int id,
            [FromBody] AddProductsToCollectionDto productsDto)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    var errors = GetModelErrors();
                    _logger.LogWarning($"ModelState errors: {string.Join(", ", errors)}");
                    return BadRequest(ApiResponse<string>.CreateErrorResponse("Invalid Data", new ErrorResponse("Invalid Data", errors), 400));
                }

                _logger.LogInformation($"Executing AddProductsToCollection for collection ID: {id}");
                var userRole = GetUserRole();
                var result = await _collectionServices.AddProductsToCollectionAsync(id, productsDto, userRole);
                return HandleResult(result, nameof(AddProductsToCollection), id);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error in AddProductsToCollection: {ex.Message}");
                return StatusCode(500, ApiResponse<string>.CreateErrorResponse("Server Error", new ErrorResponse("Server Error", "An error occurred while adding products to collection"), 500));
            }
        }

        /// <summary>
        /// Remove products from collection (Admin only)
        /// </summary>
        [HttpDelete("{id}/products")]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult<ApiResponse<string>>> RemoveProductsFromCollection(
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
                var userRole = GetUserRole();
                var result = await _collectionServices.RemoveProductsFromCollectionAsync(id, productsDto, userRole);
                return HandleResult(result, nameof(RemoveProductsFromCollection), id);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error in RemoveProductsFromCollection: {ex.Message}");
                return StatusCode(500, ApiResponse<string>.CreateErrorResponse("Server Error", new ErrorResponse("Server Error", "An error occurred while removing products from collection"), 500));
            }
        }

        /// <summary>
        /// Update collection status (Admin only)
        /// </summary>
        [HttpPut("{id}/status")]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult<ApiResponse<string>>> UpdateCollectionStatus(
            int id,
            [FromBody] bool isActive)
        {
            try
            {
                _logger.LogInformation($"Executing UpdateCollectionStatus for ID: {id}, active: {isActive}");
                var userRole = GetUserRole();
                var result = await _collectionServices.UpdateCollectionStatusAsync(id, isActive, userRole);
                return HandleResult(result, nameof(UpdateCollectionStatus), id);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error in UpdateCollectionStatus: {ex.Message}");
                return StatusCode(500, ApiResponse<string>.CreateErrorResponse("Server Error", new ErrorResponse("Server Error", "An error occurred while updating collection status"), 500));
            }
        }

        /// <summary>
        /// Update collection display order (Admin only)
        /// </summary>
        [HttpPut("{id}/display-order")]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult<ApiResponse<string>>> UpdateCollectionDisplayOrder(
            int id,
            [FromBody] int displayOrder)
        {
            try
            {
                _logger.LogInformation($"Executing UpdateCollectionDisplayOrder for ID: {id}, order: {displayOrder}");
                var userRole = GetUserRole();
                var result = await _collectionServices.UpdateCollectionDisplayOrderAsync(id, displayOrder, userRole);
                return HandleResult(result, nameof(UpdateCollectionDisplayOrder), id);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error in UpdateCollectionDisplayOrder: {ex.Message}");
                return StatusCode(500, ApiResponse<string>.CreateErrorResponse("Server Error", new ErrorResponse("Server Error", "An error occurred while updating collection display order"), 500));
            }
        }
    }
} 