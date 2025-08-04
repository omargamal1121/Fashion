using AutoMapper;
using E_Commerce.DtoModels;
using E_Commerce.DtoModels.CategoryDtos;
using E_Commerce.DtoModels.ImagesDtos;
using E_Commerce.DtoModels.Responses;
using E_Commerce.DtoModels.Shared;
using E_Commerce.ErrorHnadling;
using E_Commerce.Interfaces;
using E_Commerce.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using System.ComponentModel.DataAnnotations;
using E_Commerce.DtoModels.SubCategorydto;

namespace E_Commerce.Controllers
{
    [Route("api/subcategories")]
    [ApiController]
    public class SubCategoryController : ControllerBase
    {
        private readonly ISubCategoryServices _subCategoryServices;
        private readonly ILogger<SubCategoryController> _logger;
        private readonly ISubCategoryLinkBuilder _linkBuilder;

        public SubCategoryController(
            ISubCategoryLinkBuilder linkBuilder,
            ISubCategoryServices subCategoryServices,
            ILogger<SubCategoryController> logger)
        {
            _linkBuilder = linkBuilder;
            _subCategoryServices = subCategoryServices;
            _logger = logger;
        }

        private ActionResult<ApiResponse<T>> HandleResult<T>(Result<T> result, string apiname, int? id = null)
        {
            var links = _linkBuilder.MakeRelSelf(_linkBuilder.GenerateLinks(id), apiname);
            ApiResponse<T> apiResponse;
            if (result.Success)
            {
                apiResponse = ApiResponse<T>.CreateSuccessResponse(result.Message, result.Data, result.StatusCode, warnings: result.Warnings, links: links);
            }
            else
            {
                var errorResponse = (result.Warnings != null && result.Warnings.Count > 0)
                    ? new ErrorResponse("Error", result.Warnings)
                    : new ErrorResponse("Error", result.Message);
                apiResponse = ApiResponse<T>.CreateErrorResponse(result.Message, errorResponse, result.StatusCode, warnings: result.Warnings, links: links);
            }

            switch (result.StatusCode)
            {
                case 200: return Ok(apiResponse);
                case 201: return StatusCode(201, apiResponse);
                case 400: return BadRequest(apiResponse);
                case 401: return Unauthorized(apiResponse);
                case 409: return Conflict(apiResponse);
                default: return StatusCode(result.StatusCode, apiResponse);
            }
        }

        // User: Get active, non-deleted subcategory by ID
        [HttpGet("{id}")]
        [AllowAnonymous]
        [ActionName(nameof(GetByIdForUser))]
        public async Task<ActionResult<ApiResponse<SubCategoryDtoWithData>>> GetByIdForUser(int id)
        {
            _logger.LogInformation($"Executing {nameof(GetByIdForUser)} for id: {id}");
            if (id <= 0)
                return BadRequest(ApiResponse<SubCategoryDtoWithData>.CreateErrorResponse("Invalid subcategory ID", new ErrorResponse("Validation", new List<string> { "ID must be greater than 0" }), 400));
            var result = await _subCategoryServices.GetSubCategoryByIdAsync(id, true, false);
            return HandleResult(result, nameof(GetByIdForUser), id);
        }

        // Admin: Get subcategory by ID with filters
        [HttpGet("{id}/admin")]
        [Authorize(Roles = "Admin")]
        [ActionName(nameof(GetByIdForAdmin))]
        public async Task<ActionResult<ApiResponse<SubCategoryDtoWithData>>> GetByIdForAdmin(int id, [FromQuery] bool? isActive = null, [FromQuery] bool? isDeleted = null)
        {
            _logger.LogInformation($"Executing {nameof(GetByIdForAdmin)} for id: {id}");
            if (id <= 0)
                return BadRequest(ApiResponse<SubCategoryDtoWithData>.CreateErrorResponse("Invalid subcategory ID", new ErrorResponse("Validation", new List<string> { "ID must be greater than 0" }), 400));
            var result = await _subCategoryServices.GetSubCategoryByIdAsync(id, isActive, isDeleted);
            return HandleResult(result, nameof(GetByIdForAdmin), id);
        }

        [HttpPost]
        [Authorize(Roles = "Admin")]
        [ActionName(nameof(Create))]
        public async Task<ActionResult<ApiResponse<SubCategoryDto>>> Create([FromForm] CreateSubCategoryDto subCategoryDto)
        {
            _logger.LogInformation($"Executing {nameof(Create)}");
            if (!ModelState.IsValid)
            {
                var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList();
                return BadRequest(ApiResponse<SubCategoryDto>.CreateErrorResponse("Check On Data", new ErrorResponse("Invalid Data", errors)));
            }
            var userId = HttpContext.Items["UserId"]?.ToString();
            var result = await _subCategoryServices.CreateAsync(subCategoryDto, userId);
            return HandleResult(result, nameof(Create));
        }

        [HttpDelete("{id}")]
        [Authorize(Roles = "Admin")]
        [ActionName(nameof(Delete))]
        public async Task<ActionResult<ApiResponse<bool>>> Delete(int id)
        {
            _logger.LogInformation($"Executing {nameof(Delete)} for id: {id}");
            var userId = HttpContext.Items["UserId"]?.ToString();
            var result = await _subCategoryServices.DeleteAsync(id, userId);
            return HandleResult(result, nameof(Delete));
        }

        [HttpPatch("{id}/restore")]
        [Authorize(Roles = "Admin")]
        [ActionName(nameof(Restore))]
        public async Task<ActionResult<ApiResponse<SubCategoryDto>>> Restore(int id)
        {
            _logger.LogInformation($"Executing {nameof(Restore)} for id: {id}");
            var userId = HttpContext.Items["UserId"]?.ToString();
            var result = await _subCategoryServices.ReturnRemovedSubCategoryAsync(id, userId);
            return HandleResult(result, nameof(Restore), id);
        }

        [HttpPut("{id}")]
        [Authorize(Roles = "Admin")]
        [ActionName(nameof(Update))]
        public async Task<ActionResult<ApiResponse<SubCategoryDto>>> Update(int id, [FromForm] UpdateSubCategoryDto subCategoryDto)
        {
            _logger.LogInformation($"Executing {nameof(Update)} for id: {id}");
            if (!ModelState.IsValid)
            {
                var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList();
                return BadRequest(ApiResponse<SubCategoryDto>.CreateErrorResponse("Check on data", new ErrorResponse("Invalid Data", errors)));
            }
            var userId = HttpContext.Items["UserId"]?.ToString();
            var result = await _subCategoryServices.UpdateAsync(id, subCategoryDto, userId);
            return HandleResult(result, nameof(Update), id);
        }

        [HttpPost("{id}/images/main")]
        [Authorize(Roles = "Admin")]
        [ActionName(nameof(AddMainImage))]
        public async Task<ActionResult<ApiResponse<ImageDto>>> AddMainImage(int id, [FromForm] AddMainImageDto mainImage)
        {
            _logger.LogInformation($"Executing {nameof(AddMainImage)} for id: {id}");
            if (mainImage.Image == null || mainImage.Image.Length == 0)
            {
                return BadRequest(ApiResponse<ImageDto>.CreateErrorResponse("Image Can't Empty", new ErrorResponse("Validation", new List<string> { "Main image is required." }), 400));
            }
            var userId = HttpContext.Items["UserId"]?.ToString();
            var result = await _subCategoryServices.AddMainImageToSubCategoryAsync(id, mainImage.Image, userId);
            return HandleResult(result, nameof(AddMainImage), id);
        }

        [HttpPost("{id}/images")]
        [Authorize(Roles = "Admin")]
        [ActionName(nameof(AddImages))]
        public async Task<ActionResult<ApiResponse<List<ImageDto>>>> AddImages(int id, [FromForm] AddImagesDto images)
        {
            _logger.LogInformation($"Executing {nameof(AddImages)} for id: {id}");
            if (images.Images == null || !images.Images.Any())
            {
                return BadRequest(ApiResponse<List<ImageDto>>.CreateErrorResponse("Image Can't Empty", new ErrorResponse("Validation", new List<string> { "At least one image is required." }), 400));
            }
            var userId = HttpContext.Items["UserId"]?.ToString();
            var result = await _subCategoryServices.AddImagesToSubCategoryAsync(id, images.Images, userId);
            return HandleResult(result, nameof(AddImages), id);
        }

        [HttpDelete("{id}/images/{imageId}")]
        [Authorize(Roles = "Admin")]
        [ActionName(nameof(RemoveImage))]
        public async Task<ActionResult<ApiResponse<bool>>> RemoveImage(int id, int imageId)
        {
            _logger.LogInformation($"Executing {nameof(RemoveImage)} for id: {id}, imageId: {imageId}");
            var userId = HttpContext.Items["UserId"]?.ToString();
            var result = await _subCategoryServices.RemoveImageFromSubCategoryAsync(id, imageId, userId);
            return HandleResult(result, nameof(RemoveImage), id);
        }

        [HttpGet]
        [AllowAnonymous]
        [ActionName(nameof(GetAllForUser))]
        public async Task<IActionResult> GetAllForUser([FromQuery] int page = 1, [FromQuery] int pageSize = 10)
        {
            _logger.LogInformation($"Executing {nameof(GetAllForUser)} with page={page}, pageSize={pageSize}");
            var result = await _subCategoryServices.GetAllSubCategoriesAsync(true, false, page, pageSize);
            return StatusCode(result.StatusCode, result);
        }

        [HttpGet("admin")]
        [Authorize(Roles = "Admin")]
        [ActionName(nameof(GetAllForAdmin))]
        public async Task<IActionResult> GetAllForAdmin([FromQuery] bool? isActive = null, [FromQuery] bool? isDeleted = null, [FromQuery] int page = 1, [FromQuery] int pageSize = 10)
        {
            _logger.LogInformation($"Executing {nameof(GetAllForAdmin)} with isActive={isActive}, isDeleted={isDeleted}, page={page}, pageSize={pageSize}");
            var result = await _subCategoryServices.GetAllSubCategoriesAsync(isActive, isDeleted, page, pageSize);
            return StatusCode(result.StatusCode, result);
        }

        [HttpPatch("{id}/activate")]
        [Authorize(Roles = "Admin")]
        [ActionName(nameof(Activate))]
        public async Task<ActionResult<ApiResponse<bool>>> Activate(int id)
        {
            _logger.LogInformation($"Executing {nameof(Activate)} for id: {id}");
            var userId = HttpContext.Items["UserId"]?.ToString();
            var result = await _subCategoryServices.ActivateSubCategoryAsync(id, userId);
            return HandleResult(result, nameof(Activate));
        }

        [HttpPatch("{id}/deactivate")]
        [Authorize(Roles = "Admin")]
        [ActionName(nameof(Deactivate))]
        public async Task<ActionResult<ApiResponse<bool>>> Deactivate(int id)
        {
            _logger.LogInformation($"Executing {nameof(Deactivate)} for id: {id}");
            var userId = HttpContext.Items["UserId"]?.ToString();
            var result = await _subCategoryServices.DeactivateSubCategoryAsync(id, userId);
            return HandleResult(result, nameof(Deactivate));
        }

        [HttpGet("search/user")]
        [AllowAnonymous]
        public async Task<ActionResult<ApiResponse<List<SubCategoryDto>>>> SearchForUser([FromQuery] string key, [FromQuery] int page = 1, [FromQuery] int pageSize = 10)
        {
            _logger.LogInformation($"Executing {nameof(SearchForUser)} with key={key}, page={page}, pageSize={pageSize}");
            var result = await _subCategoryServices.FilterAsync(key, true, false, page, pageSize);
            return HandleResult(result, nameof(SearchForUser));
        }

        [HttpGet("search/admin")]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult<ApiResponse<List<SubCategoryDto>>>> SearchForAdmin(
            [FromQuery] string key,
            [FromQuery] bool isActive = true,
            [FromQuery] bool includeDeleted = false,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 10)
        {
            _logger.LogInformation($"Executing {nameof(SearchForAdmin)} with key={key}, isActive={isActive}, includeDeleted={includeDeleted}, page={page}, pageSize={pageSize}");
            var result = await _subCategoryServices.FilterAsync(key, isActive, includeDeleted, page, pageSize);
            return HandleResult(result, nameof(SearchForAdmin));
        }

	}
}
		

	
