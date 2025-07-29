using AutoMapper;
using E_Commerce.DtoModels.OrderDtos;
using E_Commerce.DtoModels.ProductDtos;
using E_Commerce.DtoModels.Responses;
using E_Commerce.Enums;
using E_Commerce.ErrorHnadling;
using E_Commerce.Interfaces;
using E_Commerce.Models;
using E_Commerce.Services.AdminOpreationServices;
using E_Commerce.Services.Cache;
using E_Commerce.Services.EmailServices;
using E_Commerce.Services.UserOpreationServices;
using E_Commerce.UOW;
using Hangfire;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Linq.Expressions;

namespace E_Commerce.Services.Order
{
    public class OrderServices : IOrderServices
    {
        private readonly ILogger<OrderServices> _logger;
        private readonly IMapper _mapper;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IUserOpreationServices _userOpreationServices;
        private readonly IOrderRepository _orderRepository;
        private readonly ICartServices _cartServices;
        private readonly IAdminOpreationServices _adminOperationServices;
        private readonly ICacheManager _cacheManager;
        private readonly UserManager<Customer> _userManager;
		private const string CACHE_TAG_ORDER = "order";

        public OrderServices(
            
            UserManager<Customer> userManager,
			IUserOpreationServices userOpreationServices,
			ILogger<OrderServices> logger,
            IMapper mapper,
            IUnitOfWork unitOfWork,
            IOrderRepository orderRepository,
            ICartServices cartServices,
            IAdminOpreationServices adminOperationServices,
            ICacheManager cacheManager)
        {
            _userManager = userManager;
			_userOpreationServices = userOpreationServices;
			_logger = logger;
            _mapper = mapper;
            _unitOfWork = unitOfWork;
            _orderRepository = orderRepository;
            _cartServices = cartServices;
            _adminOperationServices = adminOperationServices;
            _cacheManager = cacheManager;
        }

		
		private void NotifyAdminOfError(string message, string? stackTrace = null)
        {
            BackgroundJob.Enqueue<IErrorNotificationService>(_ => _.SendErrorNotificationAsync(message, stackTrace));
        }

        public async Task<Result<OrderDto>> GetOrderByIdAsync(int orderId, string userId)
        {
            _logger.LogInformation($"Getting order by ID: {orderId} for user: {userId}");

            var cacheKey = $"{CACHE_TAG_ORDER}_id_{orderId}_user_{userId}";
            var cached = await _cacheManager.GetAsync<OrderDto>(cacheKey);
            if (cached != null)
            {
                _logger.LogInformation($"Cache hit for order {orderId}");
                return Result<OrderDto>.Ok(cached, "Order retrieved from cache", 200);
            }

            try
            {
                var order = await _orderRepository.GetOrderByIdAsync(orderId);
                if (order == null)
                {
                    return Result<OrderDto>.Fail("Order not found", 404);
                }

                if (order.CustomerId != userId)
                {
                    return Result<OrderDto>.Fail("Access denied", 403);
                }

				var query = _orderRepository.GetAll();
				query = query.Where(o => o.Id == order.Id);
				var mappedOrderDto = await query.Select(GetOrderFilterExpression()).FirstOrDefaultAsync();

				await _cacheManager.SetAsync(cacheKey, mappedOrderDto, tags: new[] { CACHE_TAG_ORDER });

                return Result<OrderDto>.Ok(mappedOrderDto, "Order retrieved successfully", 200);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error getting order {orderId}: {ex.Message}");
                NotifyAdminOfError($"Error getting order {orderId}: {ex.Message}", ex.StackTrace);
                return Result<OrderDto>.Fail("An error occurred while retrieving order", 500);
            }
        }


		public async Task<Result<OrderDto>> GetOrderByNumberAsync(string orderNumber, string userId)
        {
            _logger.LogInformation($"Getting order by number: {orderNumber} for user: {userId}");

            try
            {
                var order = await _orderRepository.GetOrderByNumberAsync(orderNumber);
                if (order == null)
                {
                    return Result<OrderDto>.Fail("Order not found", 404);
                }

             
                if (order.CustomerId != userId)
                {
                    return Result<OrderDto>.Fail("Access denied", 403);
                }


				var query = _orderRepository.GetAll();
				query = query.Where(o => o.Id == order.Id);
				var mappedOrderDto = await query.Select(GetOrderFilterExpression()).FirstOrDefaultAsync();

				return Result<OrderDto>.Ok(mappedOrderDto, "Order retrieved successfully", 200);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error getting order by number {orderNumber}: {ex.Message}");
                NotifyAdminOfError($"Error getting order by number {orderNumber}: {ex.Message}", ex.StackTrace);
                return Result<OrderDto>.Fail("An error occurred while retrieving order", 500);
            }
        }

        public async Task<Result<List<OrderDto>>> GetCustomerOrdersAsync(string userId, int page = 1, int pageSize = 10)
        {
            _logger.LogInformation($"Getting orders for customer: {userId}, page: {page}");

            try
            {
                var orders = await _orderRepository.GetOrdersWithPaginationAsync(page, pageSize);
                var customerOrders = orders.Where(o => o.CustomerId == userId).ToList();

                var orderDtos = _mapper.Map<List<OrderDto>>(customerOrders);
                return Result<List<OrderDto>>.Ok(orderDtos, "Customer orders retrieved successfully", 200);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error getting customer orders for {userId}: {ex.Message}");
                NotifyAdminOfError($"Error getting customer orders for {userId}: {ex.Message}", ex.StackTrace);
                return Result<List<OrderDto>>.Fail("An error occurred while retrieving customer orders", 500);
            }
        }

        public async Task<Result<List<OrderDto>>> GetOrdersByStatusAsync(OrderStatus status, string userRole, int page = 1, int pageSize = 10)
        {
            _logger.LogInformation($"Getting orders by status: {status} for role: {userRole}");

            if (userRole != "Admin")
            {
                return Result<List<OrderDto>>.Fail("Unauthorized access", 403);
            }

            try
            {
                var orders = await _orderRepository.GetOrdersWithPaginationAsync(page, pageSize, status);
                var orderDtos = _mapper.Map<List<OrderDto>>(orders);
                return Result<List<OrderDto>>.Ok(orderDtos, $"Orders with status {status} retrieved successfully", 200);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error getting orders by status {status}: {ex.Message}");
                NotifyAdminOfError($"Error getting orders by status {status}: {ex.Message}", ex.StackTrace);
                return Result<List<OrderDto>>.Fail("An error occurred while retrieving orders", 500);
            }
        }
		private Expression<Func<E_Commerce.Models.Order, OrderDto>> GetOrderFilterExpression()
		{
			return order => new OrderDto
			{
				Id = order.Id,
				OrderNumber = order.OrderNumber,
				Customer = new CustomerDto
				{
					Id = order.Customer.Id,
					FullName = order.Customer.Name,
					Email = order.Customer.Email,
					PhoneNumber = order.Customer.Addresses
						.Where(a => a.Id == order.Addressid)
						.Select(a => a.PhoneNumber)
						.FirstOrDefault() ?? order.Customer.PhoneNumber,
					Address = order.Customer.Addresses
						.Where(a => a.Id == order.Addressid)
						.Select(a => a.FullAddress)
						.FirstOrDefault()
				},
				Status = order.Status,
				Subtotal = order.Subtotal,
				TaxAmount = order.TaxAmount,
				ShippingCost = order.ShippingCost,
				DiscountAmount = order.DiscountAmount,
				Total = order.Total,
				Notes = order.Notes,
				CreatedAt = order.CreatedAt,
				ShippedAt = order.ShippedAt,
				DeliveredAt = order.DeliveredAt,
				CancelledAt = order.CancelledAt,
				DeletedAt = order.DeletedAt,
				ModifiedAt = order.ModifiedAt,
				Payment = order.Payment != null ? new PaymentDto
				{
					Id = order.Payment.Id,
					CustomerId = order.Payment.CustomerId,
					Amount = order.Payment.Amount,
					PaymentMethod = new PaymentMethodDto
					{
						Id = order.Payment.PaymentMethod.Id,
						Name = order.Payment.PaymentMethod.Name,
					},
					Status = order.Payment.Status,
					CreatedAt = order.Payment.CreatedAt,
					ModifiedAt = order.Payment.ModifiedAt
				} : null,
				Items = order.Items.Select(i => new OrderItemDto
				{
					CreatedAt = i.CreatedAt,
					Id = i.Id,
					Product = new ProductForCartDto
					{
						Id = i.Product.Id,
						Name = i.Product.Name,
						Price = i.UnitPrice,
						MainImageUrl = i.Product.Images
							.Where(img => img.IsMain && img.DeletedAt == null)
							.Select(img => img.Url)
							.FirstOrDefault() ?? string.Empty,
						productVariantForCartDto = i.Product.ProductVariants
							.Where(v => v.Id == i.ProductVariantId && v.DeletedAt == null)
							.Select(v => new ProductVariantForCartDto
							{
								Id = v.Id,
								Color = v.Color,
								Size = v.Size,
								Length = v.Length,
								Quantity = v.Quantity
							})
							.FirstOrDefault()
					}
				}).ToList()
			};
		}

       

		public async Task<Result<OrderDto>> CreateOrderFromCartAsync(string userId, CreateOrderDto orderDto)
		{
			_logger.LogInformation($"Creating order from cart for user: {userId}");

			using var transaction = await _unitOfWork.BeginTransactionAsync();
			try
			{
				var cartResult = await _cartServices.GetCartAsync(userId);
				if (!cartResult.Success || cartResult.Data == null)
				{
					await transaction.RollbackAsync();
					return Result<OrderDto>.Fail("Failed to retrieve cart", 400);
				}

				var cart = cartResult.Data;
				if (cart.IsEmpty)
				{
					await transaction.RollbackAsync();
					return Result<OrderDto>.Fail("Cart is empty", 400);
				}
                if(cart.CheckoutDate==null ||cart.CheckoutDate.Value.AddDays(7) < DateTime.UtcNow)
                {
                    await transaction.RollbackAsync();
                    _logger.LogWarning($"Cart for user {userId} has expired or not checked out properly.");
					return Result<OrderDto>.Fail("Please Make Checkout on Cart", 400);
				}

				var subtotal = cart.Items.Sum(i => i.Quantity * i.UnitPrice);
				var total = subtotal + orderDto.TaxAmount + orderDto.ShippingCost - orderDto.DiscountAmount;
				var orderNumber = await _orderRepository.GenerateOrderNumberAsync();

				var order = new E_Commerce.Models.Order
				{
					CustomerId = userId,
					OrderNumber = orderNumber,
					Status = OrderStatus.Pending,
					Subtotal = subtotal,
					TaxAmount = orderDto.TaxAmount,
					ShippingCost = orderDto.ShippingCost,
					DiscountAmount = orderDto.DiscountAmount,
					Total = total,
					Notes = orderDto.Notes,
					CreatedAt = DateTime.UtcNow,
				};

				var createdOrder = await _orderRepository.CreateAsync(order);
				if (createdOrder == null)
				{
					await transaction.RollbackAsync();
					return Result<OrderDto>.Fail("Failed to create order", 500);
				}

				var orderItems = cart.Items.Select(cartItem => new OrderItem
				{
					OrderId = createdOrder.Id,
					ProductId = cartItem.ProductId,
					ProductVariantId = cartItem.Product.productVariantForCartDto.Id,
					Quantity = cartItem.Quantity,
					UnitPrice = cartItem.UnitPrice,
					TotalPrice = cartItem.UnitPrice * cartItem.Quantity,
					OrderedAt = DateTime.UtcNow,
                    CreatedAt = DateTime.UtcNow,
                    
				}).ToList();

				await _unitOfWork.Repository<OrderItem>().CreateRangeAsync(orderItems.ToArray());
				await _cartServices.ClearCartAsync(userId);

				var adminLog = await _userOpreationServices.AddUserOpreationAsync(
					$"Created order {orderNumber} from cart",
					Opreations.AddOpreation,
					userId,
					createdOrder.Id
				);

				if (!adminLog.Success)
				{
					_logger.LogWarning($"Failed to log admin operation: {adminLog.Message}");
					await transaction.RollbackAsync();
					return Result<OrderDto>.Fail("An error occurred while creating the order", 500);
				}

				await _unitOfWork.CommitAsync();
				await transaction.CommitAsync();

				await _cacheManager.RemoveByTagAsync(CACHE_TAG_ORDER);
               
				var query =  _orderRepository.GetAll();
                query = query.Where(o=>o.Id==createdOrder.Id);
                var mappedOrderDto = await query.Select(GetOrderFilterExpression()).FirstOrDefaultAsync();
				

				return Result<OrderDto>.Ok(mappedOrderDto, "Order created successfully", 201);
			}
			catch (Exception ex)
			{
				await transaction.RollbackAsync();
				_logger.LogError($"Error creating order for user {userId}: {ex.Message}");
				NotifyAdminOfError($"Error creating order for user {userId}: {ex.Message}", ex.StackTrace);
				return Result<OrderDto>.Fail("An error occurred while creating the order", 500);
			}
		}

		public async Task<Result<OrderDto>> UpdateOrderStatusAsync(int orderId, UpdateOrderStatusDto statusDto, string userRole)
        {
            _logger.LogInformation($"Updating order {orderId} status to {statusDto.Status}");

            if (userRole != "Admin")
            {
                return Result<OrderDto>.Fail("Unauthorized access", 403);
            }

            using var transaction = await _unitOfWork.BeginTransactionAsync();
            try
            {
                var updateResult = await _orderRepository.UpdateOrderStatusAsync(orderId, statusDto.Status, statusDto.Notes);
                if (!updateResult)
                {
                    await transaction.RollbackAsync();
                    return Result<OrderDto>.Fail("Failed to update order status", 500);
                }

                // Log admin operation
                var adminLog = await _adminOperationServices.AddAdminOpreationAsync(
                    $"Updated order {orderId} status to {statusDto.Status}",
                    Opreations.UpdateOpreation,
                    "Admin",
                    orderId
                );

                if (!adminLog.Success)
                {
                    _logger.LogWarning($"Failed to log admin operation: {adminLog.Message}");
                }

                await _unitOfWork.CommitAsync();
                await transaction.CommitAsync();

                // Clear cache
                await _cacheManager.RemoveByTagAsync(CACHE_TAG_ORDER);

                // Get updated order
                var order = await _orderRepository.GetOrderByIdAsync(orderId);
                var orderDto = _mapper.Map<OrderDto>(order);

                return Result<OrderDto>.Ok(orderDto, "Order status updated successfully", 200);
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError($"Error updating order status for order {orderId}: {ex.Message}");
                NotifyAdminOfError($"Error updating order status for order {orderId}: {ex.Message}", ex.StackTrace);
                return Result<OrderDto>.Fail("An error occurred while updating order status", 500);
            }
        }

        public async Task<Result<string>> CancelOrderAsync(int orderId, CancelOrderDto cancelDto, string userId)
        {
            _logger.LogInformation($"Cancelling order {orderId} by user {userId}");

            using var transaction = await _unitOfWork.BeginTransactionAsync();
            try
            {
                var order = await _orderRepository.GetOrderByIdAsync(orderId);
                if (order == null)
                {
                    await transaction.RollbackAsync();
                    return Result<string>.Fail("Order not found", 404);
                }

                // Check if user has access to this order
                if (order.CustomerId != userId)
                {
                    await transaction.RollbackAsync();
                    return Result<string>.Fail("Access denied", 403);
                }

                var cancelResult = await _orderRepository.CancelOrderAsync(orderId, cancelDto.CancellationReason);
                if (!cancelResult)
                {
                    await transaction.RollbackAsync();
                    return Result<string>.Fail("Failed to cancel order", 500);
                }

                // Log admin operation
                var adminLog = await _adminOperationServices.AddAdminOpreationAsync(
                    $"Cancelled order {orderId}",
                    Opreations.UpdateOpreation,
                    userId,
                    orderId
                );

                if (!adminLog.Success)
                {
                    _logger.LogWarning($"Failed to log admin operation: {adminLog.Message}");
                }

                await _unitOfWork.CommitAsync();
                await transaction.CommitAsync();

                // Clear cache
                await _cacheManager.RemoveByTagAsync(CACHE_TAG_ORDER);

                return Result<string>.Ok(null, "Order cancelled successfully", 200);
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError($"Error cancelling order {orderId}: {ex.Message}");
                NotifyAdminOfError($"Error cancelling order {orderId}: {ex.Message}", ex.StackTrace);
                return Result<string>.Fail("An error occurred while cancelling order", 500);
            }
        }

        public async Task<Result<string>> ShipOrderAsync(int orderId, string userRole)
        {
            if (userRole != "Admin")
            {
                return Result<string>.Fail("Unauthorized access", 403);
            }

            try
            {
                var shipResult = await _orderRepository.ShipOrderAsync(orderId);
                if (!shipResult)
                {
                    return Result<string>.Fail("Failed to ship order", 500);
                }

                await _cacheManager.RemoveByTagAsync(CACHE_TAG_ORDER);
                return Result<string>.Ok(null, "Order shipped successfully", 200);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error shipping order {orderId}: {ex.Message}");
                return Result<string>.Fail("An error occurred while shipping order", 500);
            }
        }

        public async Task<Result<string>> DeliverOrderAsync(int orderId, string userRole)
        {
            if (userRole != "Admin")
            {
                return Result<string>.Fail("Unauthorized access", 403);
            }

            try
            {
                var deliverResult = await _orderRepository.DeliverOrderAsync(orderId);
                if (!deliverResult)
                {
                    return Result<string>.Fail("Failed to deliver order", 500);
                }

                await _cacheManager.RemoveByTagAsync(CACHE_TAG_ORDER);
                return Result<string>.Ok(null, "Order delivered successfully", 200);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error delivering order {orderId}: {ex.Message}");
                return Result<string>.Fail("An error occurred while delivering order", 500);
            }
        }

        public async Task<Result<int?>> GetOrderCountByCustomerAsync(string userId)
        {
            try
            {
                var count = await _orderRepository.GetOrderCountByCustomerAsync(userId);
                return Result<int?>.Ok(count, "Order count retrieved", 200);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error getting order count for user {userId}: {ex.Message}");
                return Result<int?>.Fail("An error occurred while getting order count", 500);
            }
        }

        public async Task<Result<decimal>> GetTotalRevenueByCustomerAsync(string userId)
        {
            try
            {
                var revenue = await _orderRepository.GetTotalRevenueByCustomerAsync(userId);
                return Result<decimal>.Ok(revenue, "Total revenue retrieved", 200);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error getting total revenue for user {userId}: {ex.Message}");
                return Result<decimal>.Fail("An error occurred while getting total revenue", 500);
            }
        }

        public async Task<Result<decimal>> GetTotalRevenueByDateRangeAsync(DateTime startDate, DateTime endDate, string userRole)
        {
            if (userRole != "Admin")
            {
                return Result<decimal>.Fail("Unauthorized access", 403);
            }

            try
            {
                var revenue = await _orderRepository.GetTotalRevenueByDateRangeAsync(startDate, endDate);
                return Result<decimal>.Ok(revenue, "Total revenue retrieved", 200);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error getting total revenue for date range: {ex.Message}");
                return Result<decimal>.Fail("An error occurred while getting total revenue", 500);
            }
        }

        public async Task<Result<List<OrderDto>>> GetOrdersWithPaginationAsync(int page, int pageSize, OrderStatus? status, string userRole)
        {
            if (userRole != "Admin")
            {
                return Result<List<OrderDto>>.Fail("Unauthorized access", 403);
            }

            try
            {
                var orders = await _orderRepository.GetOrdersWithPaginationAsync(page, pageSize, status);
                var orderDtos = _mapper.Map<List<OrderDto>>(orders);
                return Result<List<OrderDto>>.Ok(orderDtos, "Orders retrieved successfully", 200);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error getting orders with pagination: {ex.Message}");
                return Result<List<OrderDto>>.Fail("An error occurred while retrieving orders", 500);
            }
        }

        public async Task<Result<int?>> GetTotalOrderCountAsync(OrderStatus? status, string userRole)
        {
            if (userRole != "Admin")
            {
                return Result<int?>.Fail("Unauthorized access", 403);
            }

            try
            {
                var count = await _orderRepository.GetTotalOrderCountAsync(status);
                return Result<int?>.Ok(count, "Total order count retrieved", 200);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error getting total order count: {ex.Message}");
                return Result<int?>.Fail("An error occurred while getting total order count", 500);
            }
        }
    }
} 