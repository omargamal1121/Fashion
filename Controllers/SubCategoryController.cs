using AutoMapper;
using E_Commers.DtoModels;
using E_Commers.DtoModels.CategoryDtos;
using E_Commers.DtoModels.ImagesDtos;
using E_Commers.DtoModels.Responses;
using E_Commers.DtoModels.Shared;
using E_Commers.ErrorHnadling;
using E_Commers.Interfaces;
using E_Commers.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using System.ComponentModel.DataAnnotations;

namespace E_Commers.Controllers
{
	[Route("api/[controller]")]
	[ApiController]
	[Authorize(Roles = "Admin")]
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

		[HttpGet("{id}", Name = "GetSubCategoryById")]
		[ActionName(nameof(GetByIdAsync))]
		public async Task<ActionResult<ApiResponse<SubCategoryDto>>> GetByIdAsync(
			int id,
			[FromQuery] bool isActive = true,
			[FromQuery] bool DeletedOnly = false)
		{
			_logger.LogInformation($"Executing {nameof(GetByIdAsync)} for id: {id}, isActive: {isActive}, includeDeleted: {DeletedOnly}");
			var result = await _subCategoryServices.GetSubCategoryByIdAsync(id, isActive, DeletedOnly);
			return HandleResult(result, nameof(GetByIdAsync), id);
		}

		

		[HttpPost]
		[ActionName(nameof(CreateAsync))]
		public async Task<ActionResult<ApiResponse<SubCategoryDto>>> CreateAsync([FromForm] CreateSubCategoryDto subCategoryDto)
		{
			_logger.LogInformation($"Executing {nameof(CreateAsync)}");
			if (!ModelState.IsValid)
			{
				var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList();
				return BadRequest(ApiResponse<SubCategoryDto>.CreateErrorResponse("Check On Data", new ErrorResponse("Invalid Data", errors)));
			}
			var userId = HttpContext.Items["UserId"]?.ToString();
			var result = await _subCategoryServices.CreateAsync(subCategoryDto, userId);
			return HandleResult(result, nameof(CreateAsync));
		}

		[HttpDelete("{id}")]
		[ActionName(nameof(DeleteAsync))]
		public async Task<ActionResult<ApiResponse<string>>> DeleteAsync(int id)
		{
			_logger.LogInformation($"Executing {nameof(DeleteAsync)} for id: {id}");
			var userId = HttpContext.Items["UserId"]?.ToString();
			var result = await _subCategoryServices.DeleteAsync(id, userId);
			return HandleResult(result, nameof(DeleteAsync));
		}

		[HttpPatch("{id}/Restore")]
		[ActionName(nameof(ReturnRemovedSubCategoryAsync))]
		public async Task<ActionResult<ApiResponse<SubCategoryDto>>> ReturnRemovedSubCategoryAsync(int id)
		{
			_logger.LogInformation($"Executing {nameof(ReturnRemovedSubCategoryAsync)} for id: {id}");
			var userId = HttpContext.Items["UserId"]?.ToString();
			var result = await _subCategoryServices.ReturnRemovedSubCategoryAsync(id, userId);
			return HandleResult(result, nameof(ReturnRemovedSubCategoryAsync), id);
		}

		[HttpPut("{id}")]
		[ActionName(nameof(UpdateAsync))]
		public async Task<ActionResult<ApiResponse<SubCategoryDto>>> UpdateAsync(int id, [FromForm] UpdateSubCategoryDto subCategoryDto)
		{
			_logger.LogInformation($"Executing {nameof(UpdateAsync)} for id: {id}");
			if (!ModelState.IsValid)
			{
				var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList();
				return BadRequest(ApiResponse<SubCategoryDto>.CreateErrorResponse("Check on data", new ErrorResponse("Invalid Data", errors)));
			}
			var userId = HttpContext.Items["UserId"]?.ToString();
			var result = await _subCategoryServices.UpdateAsync(id, subCategoryDto, userId);
			return HandleResult(result, nameof(UpdateAsync), id);
		}

		[HttpPost("{id}/AddMainImage")]
		[ActionName(nameof(AddMainImageAsync))]
		public async Task<ActionResult<ApiResponse<ImageDto>>> AddMainImageAsync(int id, [FromForm] AddMainImageDto mainImage)
		{
			_logger.LogInformation($"Executing {nameof(AddMainImageAsync)} for id: {id}");
			if (mainImage.Image == null || mainImage.Image.Length == 0)
			{
				return BadRequest(ApiResponse<ImageDto>.CreateErrorResponse("Image Can't Empty", new ErrorResponse("Validation", new List<string> { "Main image is required." }), 400));
			}
			var userId = HttpContext.Items["UserId"]?.ToString();
			var result = await _subCategoryServices.AddMainImageToSubCategoryAsync(id, mainImage.Image, userId);
			return HandleResult(result, nameof(AddMainImageAsync), id);
		}

		[HttpPost("{id}/AddExtraImages")]
		[ActionName(nameof(AddExtraImagesAsync))]
	
		public async Task<ActionResult<ApiResponse<List<ImageDto>>>> AddExtraImagesAsync(int id, [FromForm] AddImagesDto images)
		{
			_logger.LogInformation($"Executing {nameof(AddExtraImagesAsync)} for id: {id}");
			if (images.Images == null || !images.Images.Any())
			{
				return BadRequest(ApiResponse<List<ImageDto>>.CreateErrorResponse("Image Can't Empty", new ErrorResponse("Validation", new List<string> { "At least one image is required." }), 400));
			}
			var userId = HttpContext.Items["UserId"]?.ToString();
			var result = await _subCategoryServices.AddImagesToSubCategoryAsync(id, images.Images, userId);
			return HandleResult(result, nameof(AddExtraImagesAsync), id);
		}

		[HttpDelete("{id}/RemoveImage/{imageId}")]
		[ActionName(nameof(RemoveImageAsync))]
		public async Task<ActionResult<ApiResponse<SubCategoryDto>>> RemoveImageAsync(int id, int imageId)
		{
			_logger.LogInformation($"Executing {nameof(RemoveImageAsync)} for id: {id}, imageId: {imageId}");
			var userId = HttpContext.Items["UserId"]?.ToString();
			var result = await _subCategoryServices.RemoveImageFromSubCategoryAsync(id, imageId, userId);
			return HandleResult(result, nameof(RemoveImageAsync), id);
		}

		

		[HttpGet("test-links")]
		[ActionName(nameof(TestLinks))]
		public ActionResult<ApiResponse<List<LinkDto>>> TestLinks()
		{
			_logger.LogInformation($"Executing {nameof(TestLinks)}");
			var links = _linkBuilder.GenerateLinks(1);
			return Ok(ApiResponse<List<LinkDto>>.CreateSuccessResponse("Test Links", links, 200, links: links));
		}

		[HttpGet("all/user")]
		[AllowAnonymous]
		public async Task<IActionResult> GetAllForUser([FromQuery] int page = 1, [FromQuery] int pageSize = 10)
		{
			_logger.LogInformation($"Executing {nameof(GetAllForUser)} with page={page}, pageSize={pageSize}");
			var result = await _subCategoryServices.GetAllSubCategoriesAsync(true, false, page, pageSize);
			return StatusCode(result.StatusCode, result);
		}

		[HttpGet("all/admin")]
		[Authorize(Roles = "Admin")]
		public async Task<IActionResult> GetAllForAdmin([FromQuery] bool? isActive = null, [FromQuery] bool? isDeleted = null, [FromQuery] int page = 1, [FromQuery] int pageSize = 10)
		{
			_logger.LogInformation($"Executing {nameof(GetAllForAdmin)} with isActive={isActive}, isDeleted={isDeleted}, page={page}, pageSize={pageSize}");
			var result = await _subCategoryServices.GetAllSubCategoriesAsync(isActive, isDeleted, page, pageSize);
			return StatusCode(result.StatusCode, result);
		}

		[HttpPatch("{id}/activate")]
		[Authorize(Roles = "Admin")]
		[ActionName(nameof(Activate))]

		public async Task<IActionResult> Activate(int id)
		{
			_logger.LogInformation($"Executing {nameof(Activate)} for id: {id}");
			var userId = HttpContext.Items["UserId"]?.ToString();
			var result = await _subCategoryServices.ActivateSubCategoryAsync(id, userId);
			return StatusCode(result.StatusCode, result);
		}

		[HttpPatch("{id}/deactivate")]
		[Authorize(Roles = "Admin")]
		[ActionName(nameof(Deactivate))]
		public async Task<IActionResult> Deactivate(int id)
		{
			_logger.LogInformation($"Executing {nameof(Deactivate)} for id: {id}");
			var userId = HttpContext.Items["UserId"]?.ToString();
			var result = await _subCategoryServices.DeactivateSubCategoryAsync(id, userId);
			return StatusCode(result.StatusCode, result);
		}

		// Only keep SearchForUser and SearchForAdmin endpoints for searching subcategories
		[HttpGet("search/user")]
		[AllowAnonymous]
		public async Task<ActionResult<ApiResponse<List<SubCategoryDto>>>> SearchForUser([FromQuery] string key, [FromQuery] int page = 1, [FromQuery] int pageSize = 10)
		{
			_logger.LogInformation($"Executing {nameof(SearchForUser)} with key={key}, page={page}, pageSize={pageSize}");
			// Only active and not deleted for users
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