using E_Commers.DtoModels.OrderDtos;
using E_Commers.DtoModels.Responses;
using E_Commers.Enums;
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
    [Authorize]
    public class OrderController : ControllerBase
    {
        private readonly IOrderServices _orderServices;
        private readonly ILogger<OrderController> _logger;

        public OrderController(IOrderServices orderServices, ILogger<OrderController> logger)
        {
            _orderServices = orderServices;
            _logger = logger;
        }

        private string GetUserId()
        {
            return User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? string.Empty;
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
        /// Get order by ID
        /// </summary>
        [HttpGet("{orderId}")]
        public async Task<ActionResult<ApiResponse<OrderDto>>> GetOrderById(int orderId)
        {
            try
            {
                _logger.LogInformation($"Executing GetOrderById for ID: {orderId}");
                var userId = GetUserId();
                var result = await _orderServices.GetOrderByIdAsync(orderId, userId);
                return HandleResult(result, nameof(GetOrderById), orderId);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error in GetOrderById: {ex.Message}");
                return StatusCode(500, ApiResponse<OrderDto>.CreateErrorResponse("Server Error", new ErrorResponse("Server Error", "An error occurred while retrieving the order"), 500));
            }
        }

        /// <summary>
        /// Get order by order number
        /// </summary>
        [HttpGet("number/{orderNumber}")]
        public async Task<ActionResult<ApiResponse<OrderDto>>> GetOrderByNumber(string orderNumber)
        {
            try
            {
                _logger.LogInformation($"Executing GetOrderByNumber for number: {orderNumber}");
                var userId = GetUserId();
                var result = await _orderServices.GetOrderByNumberAsync(orderNumber, userId);
                return HandleResult(result, nameof(GetOrderByNumber));
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error in GetOrderByNumber: {ex.Message}");
                return StatusCode(500, ApiResponse<OrderDto>.CreateErrorResponse("Server Error", new ErrorResponse("Server Error", "An error occurred while retrieving the order"), 500));
            }
        }

        /// <summary>
        /// Get customer orders with pagination
        /// </summary>
        [HttpGet("customer")]
        public async Task<ActionResult<ApiResponse<List<OrderDto>>>> GetCustomerOrders(
            [FromQuery] int page = 1, 
            [FromQuery] int pageSize = 10)
        {
            try
            {
                _logger.LogInformation($"Executing GetCustomerOrders with pagination: page {page}, size {pageSize}");
                var userId = GetUserId();
                var result = await _orderServices.GetCustomerOrdersAsync(userId, page, pageSize);
                return HandleResult(result, nameof(GetCustomerOrders));
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error in GetCustomerOrders: {ex.Message}");
                return StatusCode(500, ApiResponse<List<OrderDto>>.CreateErrorResponse("Server Error", new ErrorResponse("Server Error", "An error occurred while retrieving customer orders"), 500));
            }
        }

        /// <summary>
        /// Create order from cart
        /// </summary>
        [HttpPost("create-from-cart")]
        public async Task<ActionResult<ApiResponse<OrderDto>>> CreateOrderFromCart([FromBody] CreateOrderDto orderDto)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    var errors = GetModelErrors();
                    _logger.LogWarning($"ModelState errors: {string.Join(", ", errors)}");
                    return BadRequest(ApiResponse<OrderDto>.CreateErrorResponse("Invalid Data", new ErrorResponse("Invalid Data", errors), 400));
                }

                _logger.LogInformation("Executing CreateOrderFromCart");
                var userId = GetUserId();
                var result = await _orderServices.CreateOrderFromCartAsync(userId, orderDto);
                return HandleResult(result, nameof(CreateOrderFromCart));
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error in CreateOrderFromCart: {ex.Message}");
                return StatusCode(500, ApiResponse<OrderDto>.CreateErrorResponse("Server Error", new ErrorResponse("Server Error", "An error occurred while creating the order"), 500));
            }
        }

        /// <summary>
        /// Cancel an order
        /// </summary>
        [HttpPost("{orderId}/cancel")]
        public async Task<ActionResult<ApiResponse<string>>> CancelOrder(int orderId, [FromBody] CancelOrderDto cancelDto)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    var errors = GetModelErrors();
                    _logger.LogWarning($"ModelState errors: {string.Join(", ", errors)}");
                    return BadRequest(ApiResponse<string>.CreateErrorResponse("Invalid Data", new ErrorResponse("Invalid Data", errors), 400));
                }

                _logger.LogInformation($"Executing CancelOrder for ID: {orderId}");
                var userId = GetUserId();
                var result = await _orderServices.CancelOrderAsync(orderId, cancelDto, userId);
                return HandleResult(result, nameof(CancelOrder), orderId);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error in CancelOrder: {ex.Message}");
                return StatusCode(500, ApiResponse<string>.CreateErrorResponse("Server Error", new ErrorResponse("Server Error", "An error occurred while canceling the order"), 500));
            }
        }

        /// <summary>
        /// Get customer order count
        /// </summary>
        [HttpGet("customer/count")]
        public async Task<ActionResult<ApiResponse<int?>>> GetOrderCount()
        {
            try
            {
                _logger.LogInformation("Executing GetOrderCount");
                var userId = GetUserId();
                var result = await _orderServices.GetOrderCountByCustomerAsync(userId);
                return HandleResult(result, nameof(GetOrderCount));
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error in GetOrderCount: {ex.Message}");
                return StatusCode(500, ApiResponse<int?>.CreateErrorResponse("Server Error", new ErrorResponse("Server Error", "An error occurred while getting order count"), 500));
            }
        }

        /// <summary>
        /// Get customer total revenue
        /// </summary>
        [HttpGet("customer/revenue")]
        public async Task<ActionResult<ApiResponse<decimal>>> GetCustomerRevenue()
        {
            try
            {
                _logger.LogInformation("Executing GetCustomerRevenue");
                var userId = GetUserId();
                var result = await _orderServices.GetTotalRevenueByCustomerAsync(userId);
                return HandleResult(result, nameof(GetCustomerRevenue));
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error in GetCustomerRevenue: {ex.Message}");
                return StatusCode(500, ApiResponse<decimal>.CreateErrorResponse("Server Error", new ErrorResponse("Server Error", "An error occurred while getting customer revenue"), 500));
            }
        }

        // Admin-only endpoints
        /// <summary>
        /// Get orders by status (Admin only)
        /// </summary>
        [HttpGet("status/{status}")]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult<ApiResponse<List<OrderDto>>>> GetOrdersByStatus(
            OrderStatus status,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 10)
        {
            try
            {
                _logger.LogInformation($"Executing GetOrdersByStatus for status: {status}, page: {page}, size: {pageSize}");
                var userRole = GetUserRole();
                var result = await _orderServices.GetOrdersByStatusAsync(status, userRole, page, pageSize);
                return HandleResult(result, nameof(GetOrdersByStatus));
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error in GetOrdersByStatus: {ex.Message}");
                return StatusCode(500, ApiResponse<List<OrderDto>>.CreateErrorResponse("Server Error", new ErrorResponse("Server Error", "An error occurred while retrieving orders by status"), 500));
            }
        }

        /// <summary>
        /// Update order status (Admin only)
        /// </summary>
        [HttpPut("{orderId}/status")]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult<ApiResponse<OrderDto>>> UpdateOrderStatus(
            int orderId, 
            [FromBody] UpdateOrderStatusDto statusDto)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    var errors = GetModelErrors();
                    _logger.LogWarning($"ModelState errors: {string.Join(", ", errors)}");
                    return BadRequest(ApiResponse<OrderDto>.CreateErrorResponse("Invalid Data", new ErrorResponse("Invalid Data", errors), 400));
                }

                _logger.LogInformation($"Executing UpdateOrderStatus for ID: {orderId}, status: {statusDto.Status}");
                var userRole = GetUserRole();
                var result = await _orderServices.UpdateOrderStatusAsync(orderId, statusDto, userRole);
                return HandleResult(result, nameof(UpdateOrderStatus), orderId);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error in UpdateOrderStatus: {ex.Message}");
                return StatusCode(500, ApiResponse<OrderDto>.CreateErrorResponse("Server Error", new ErrorResponse("Server Error", "An error occurred while updating order status"), 500));
            }
        }

        /// <summary>
        /// Ship an order (Admin only)
        /// </summary>
        [HttpPost("{orderId}/ship")]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult<ApiResponse<string>>> ShipOrder(int orderId)
        {
            try
            {
                _logger.LogInformation($"Executing ShipOrder for ID: {orderId}");
                var userRole = GetUserRole();
                var result = await _orderServices.ShipOrderAsync(orderId, userRole);
                return HandleResult(result, nameof(ShipOrder), orderId);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error in ShipOrder: {ex.Message}");
                return StatusCode(500, ApiResponse<string>.CreateErrorResponse("Server Error", new ErrorResponse("Server Error", "An error occurred while shipping the order"), 500));
            }
        }

        /// <summary>
        /// Deliver an order (Admin only)
        /// </summary>
        [HttpPost("{orderId}/deliver")]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult<ApiResponse<string>>> DeliverOrder(int orderId)
        {
            try
            {
                _logger.LogInformation($"Executing DeliverOrder for ID: {orderId}");
                var userRole = GetUserRole();
                var result = await _orderServices.DeliverOrderAsync(orderId, userRole);
                return HandleResult(result, nameof(DeliverOrder), orderId);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error in DeliverOrder: {ex.Message}");
                return StatusCode(500, ApiResponse<string>.CreateErrorResponse("Server Error", new ErrorResponse("Server Error", "An error occurred while delivering the order"), 500));
            }
        }

        /// <summary>
        /// Get revenue by date range (Admin only)
        /// </summary>
        [HttpGet("revenue")]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult<ApiResponse<decimal>>> GetRevenueByDateRange(
            [FromQuery] DateTime startDate,
            [FromQuery] DateTime endDate)
        {
            try
            {
                _logger.LogInformation($"Executing GetRevenueByDateRange from {startDate} to {endDate}");
                var userRole = GetUserRole();
                var result = await _orderServices.GetTotalRevenueByDateRangeAsync(startDate, endDate, userRole);
                return HandleResult(result, nameof(GetRevenueByDateRange));
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error in GetRevenueByDateRange: {ex.Message}");
                return StatusCode(500, ApiResponse<decimal>.CreateErrorResponse("Server Error", new ErrorResponse("Server Error", "An error occurred while getting revenue by date range"), 500));
            }
        }

        /// <summary>
        /// Get orders with pagination (Admin only)
        /// </summary>
        [HttpGet("admin")]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult<ApiResponse<List<OrderDto>>>> GetOrdersWithPagination(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 10,
            [FromQuery] OrderStatus? status = null)
        {
            try
            {
                _logger.LogInformation($"Executing GetOrdersWithPagination: page {page}, size {pageSize}, status: {status}");
                var userRole = GetUserRole();
                var result = await _orderServices.GetOrdersWithPaginationAsync(page, pageSize, status, userRole);
                return HandleResult(result, nameof(GetOrdersWithPagination));
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error in GetOrdersWithPagination: {ex.Message}");
                return StatusCode(500, ApiResponse<List<OrderDto>>.CreateErrorResponse("Server Error", new ErrorResponse("Server Error", "An error occurred while retrieving orders with pagination"), 500));
            }
        }

        /// <summary>
        /// Get total order count (Admin only)
        /// </summary>
        [HttpGet("admin/count")]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult<ApiResponse<int?>>> GetTotalOrderCount(
            [FromQuery] OrderStatus? status = null)
        {
            try
            {
                _logger.LogInformation($"Executing GetTotalOrderCount, status: {status}");
                var userRole = GetUserRole();
                var result = await _orderServices.GetTotalOrderCountAsync(status, userRole);
                return HandleResult(result, nameof(GetTotalOrderCount));
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error in GetTotalOrderCount: {ex.Message}");
                return StatusCode(500, ApiResponse<int?>.CreateErrorResponse("Server Error", new ErrorResponse("Server Error", "An error occurred while getting total order count"), 500));
            }
        }
    }
} 