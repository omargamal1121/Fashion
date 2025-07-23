using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using E_Commerce.Services.Product;
using E_Commerce.DtoModels.ProductDtos;
using E_Commerce.Enums;
using Microsoft.AspNetCore.Authorization;
using E_Commerce.DtoModels.Responses;
using E_Commerce.ErrorHnadling;
using E_Commerce.Services.ProductServices;

namespace E_Commerce.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ProductVariantController : ControllerBase
    {
        private readonly IProductVariantService _variantService;
        private readonly ILogger<ProductVariantController> _logger;

        public ProductVariantController(IProductVariantService variantService, ILogger<ProductVariantController> logger)
        {
            _variantService = variantService;
            _logger = logger;
        }

        private ActionResult<ApiResponse<T>> HandleResult<T>(E_Commerce.Services.Result<T> result, string actionName = null, int? id = null) 
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

        [HttpGet("by-product/{productId}")]
        [AllowAnonymous]
        public async Task<ActionResult<ApiResponse<List<ProductVariantDto>>>> GetProductVariants(int productId)
        {
            var result = await _variantService.GetProductVariantsAsync(productId, true, false);
            return HandleResult<List<ProductVariantDto>>(result);
        }

        [HttpGet("by-product-admin/{productId}")]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult<ApiResponse<List<ProductVariantDto>>>> GetProductVariantsAdmin(int productId, [FromQuery] bool? isActive = null, [FromQuery] bool? deletedOnly = null)
        {
            var result = await _variantService.GetProductVariantsAsync(productId, isActive, deletedOnly);
            return HandleResult<List<ProductVariantDto>>(result);
        }

        [HttpGet("{id}")]
        [AllowAnonymous]
        public async Task<ActionResult<ApiResponse<ProductVariantDto>>> GetVariantById(int id)
        {
            var result = await _variantService.GetVariantByIdAsync(id);
            return HandleResult<ProductVariantDto>(result);
        }

        [HttpPost("add")]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult<ApiResponse<ProductVariantDto>>> AddVariant([FromQuery] int productId, [FromBody] CreateProductVariantDto dto)
        {
            var userId = HttpContext.Items["UserId"]?.ToString();
            var result = await _variantService.AddVariantAsync(productId, dto, userId);
            return HandleResult<ProductVariantDto>(result, nameof(GetVariantById), result.Data?.Id);
        }

        [HttpPut("update/{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult<ApiResponse<ProductVariantDto>>> UpdateVariant(int id, [FromBody] UpdateProductVariantDto dto)
        {
            var userId = HttpContext.Items["UserId"]?.ToString();
            var result = await _variantService.UpdateVariantAsync(id, dto, userId);
            return HandleResult<ProductVariantDto>(result);
        }

        [HttpDelete("delete/{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult<ApiResponse<bool>>> DeleteVariant(int id)
        {
            var userId = HttpContext.Items["UserId"]?.ToString();
            var result = await _variantService.DeleteVariantAsync(id, userId);
            return HandleResult<bool>(result);
        }

        
        [HttpPut("add-quantity/{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult<ApiResponse<bool>>> AddVariantQuantity(int id, [FromQuery] int addQuantity)
        {
            var userId = HttpContext.Items["UserId"]?.ToString();
            var result = await _variantService.AddVariantQuantityAsync(id, addQuantity, userId);
            return HandleResult<bool>(result);
        }

        [HttpPut("remove-quantity/{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult<ApiResponse<bool>>> RemoveVariantQuantity(int id, [FromQuery] int removeQuantity)
        {
            var userId = HttpContext.Items["UserId"]?.ToString();
            var result = await _variantService.RemoveVariantQuantityAsync(id, removeQuantity, userId);
            return HandleResult<bool>(result);
        }

        [HttpPut("activate/{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult<ApiResponse<bool>>> ActivateVariant(int id)
        {
            var userId = HttpContext.Items["UserId"]?.ToString();
            var result = await _variantService.ActivateVariantAsync(id, userId);
            return HandleResult<bool>(result);
        }

        [HttpPut("deactivate/{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult<ApiResponse<bool>>> DeactivateVariant(int id)
        {
            var userId = HttpContext.Items["UserId"]?.ToString();
            var result = await _variantService.DeactivateVariantAsync(id, userId);
            return HandleResult<bool>(result);
        }

        [HttpPut("restore/{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult<ApiResponse<bool>>> RestoreVariant(int id)
        {
            var userId = HttpContext.Items["UserId"]?.ToString();
            var result = await _variantService.RestoreVariantAsync(id, userId);
            return HandleResult<bool>(result);
        }

        [HttpGet("search")]
        [AllowAnonymous]
        public async Task<ActionResult<ApiResponse<List<ProductVariantDto>>>> GetVariantsBySearch([FromQuery] int productId, [FromQuery] string? color = null, [FromQuery] VariantSize? size = null)
        {
            var result = await _variantService.GetVariantsBySearchAsync(productId, color, size, true, false);
            return HandleResult<List<ProductVariantDto>>(result);
        }

        [HttpGet("search-admin")]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult<ApiResponse<List<ProductVariantDto>>>> GetVariantsBySearchAdmin([FromQuery] int productId, [FromQuery] string? color = null, [FromQuery] VariantSize? size = null, [FromQuery] bool? isActive = null, [FromQuery] bool? deletedOnly = null)
        {
            var result = await _variantService.GetVariantsBySearchAsync(productId, color, size, isActive, deletedOnly);
            return HandleResult<List<ProductVariantDto>>(result);
        }
    }
} 