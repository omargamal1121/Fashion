using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using E_Commers.Services.Product;
using E_Commers.DtoModels.ProductDtos;
using E_Commers.Enums;
using Microsoft.AspNetCore.Authorization;
using E_Commers.DtoModels.Responses;
using E_Commers.ErrorHnadling;

namespace E_Commers.Controllers
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

        private ActionResult<ApiResponse<T>> HandleResult<T>(E_Commers.Services.Result<T> result, string actionName = null, int? id = null) 
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

        [HttpGet("{variantId}")]
        [AllowAnonymous]
        public async Task<ActionResult<ApiResponse<ProductVariantDto>>> GetVariantById(int variantId)
        {
            var result = await _variantService.GetVariantByIdAsync(variantId);
            return HandleResult<ProductVariantDto>(result);
        }

        [HttpPost("add")] 
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult<ApiResponse<ProductVariantDto>>> AddVariant([FromQuery] int productId, [FromBody] CreateProductVariantDto dto, [FromQuery] string userId)
        {
            var result = await _variantService.AddVariantAsync(productId, dto, userId);
            return HandleResult<ProductVariantDto>(result, nameof(AddVariant), productId);
        }

        [HttpPut("update/{variantId}")]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult<ApiResponse<ProductVariantDto>>> UpdateVariant(int variantId, [FromBody] UpdateProductVariantDto dto, [FromQuery] string userId)
        {
            var result = await _variantService.UpdateVariantAsync(variantId, dto, userId);
            return HandleResult<ProductVariantDto>(result);
        }

        [HttpDelete("delete/{variantId}")]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult<ApiResponse<string>>> DeleteVariant(int variantId, [FromQuery] string userId)
        {
            var result = await _variantService.DeleteVariantAsync(variantId, userId);
            return HandleResult<string>(result);
        }

        
        [HttpPut("add-quantity/{variantId}")]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult<ApiResponse<string>>> AddVariantQuantity(int variantId, [FromQuery] int addQuantity, [FromQuery] string userId)
        {
            var result = await _variantService.AddVariantQuantityAsync(variantId, addQuantity, userId);
            return HandleResult<string>(result);
        }

        [HttpPut("remove-quantity/{variantId}")]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult<ApiResponse<string>>> RemoveVariantQuantity(int variantId, [FromQuery] int removeQuantity, [FromQuery] string userId)
        {
            var result = await _variantService.RemoveVariantQuantityAsync(variantId, removeQuantity, userId);
            return HandleResult<string>(result);
        }

        [HttpPut("activate/{variantId}")]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult<ApiResponse<string>>> ActivateVariant(int variantId, [FromQuery] string userId)
        {
            var result = await _variantService.ActivateVariantAsync(variantId, userId);
            return HandleResult<string>(result);
        }

        [HttpPut("deactivate/{variantId}")]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult<ApiResponse<string>>> DeactivateVariant(int variantId, [FromQuery] string userId)
        {
            var result = await _variantService.DeactivateVariantAsync(variantId, userId);
            return HandleResult<string>(result);
        }

        [HttpPut("restore/{variantId}")]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult<ApiResponse<string>>> RestoreVariant(int variantId, [FromQuery] string userId)
        {
            var result = await _variantService.RestoreVariantAsync(variantId, userId);
            return HandleResult<string>(result);
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