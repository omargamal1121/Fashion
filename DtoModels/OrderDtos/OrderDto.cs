using E_Commerce.DtoModels.CartDtos;
using E_Commerce.DtoModels.ImagesDtos;
using E_Commerce.DtoModels.ProductDtos;
using E_Commerce.DtoModels.Shared;
using E_Commerce.Enums;
using E_Commerce.Services;

namespace E_Commerce.DtoModels.OrderDtos
{
    public class OrderDto : BaseDto
    {
        public string OrderNumber { get; set; } = string.Empty;
        public CustomerDto? Customer { get; set; }
        public OrderStatus Status { get; set; }
        public decimal Subtotal { get; set; }
        public decimal TaxAmount { get; set; }
        public decimal ShippingCost { get; set; }
        public decimal DiscountAmount { get; set; }
        public decimal Total { get; set; }
        public string? Notes { get; set; }
        public DateTime? ShippedAt { get; set; }
        public DateTime? DeliveredAt { get; set; }
        public DateTime? CancelledAt { get; set; }
        public List<OrderItemDto> Items { get; set; } = new List<OrderItemDto>();
        public PaymentDto? Payment { get; set; }
        
        public bool IsCancelled => Status == OrderStatus.Cancelled;
        public bool IsDelivered => Status == OrderStatus.Delivered;
        public bool IsShipped => Status == OrderStatus.Shipped;
        public bool CanBeCancelled => Status == OrderStatus.Pending || Status == OrderStatus.Confirmed;
        public bool CanBeReturned => Status == OrderStatus.Delivered;
        public string StatusDisplay => Status.ToString();
    }
	public class OrderWithPaymentDto
	{
		public OrderDto Order { get; set; }
		public string? PaymentUrl { get; set; } 
	}

	public class CustomerDto
	{

		public string Id { get; set; } = string.Empty;
		public string FullName { get; set; } = string.Empty;
		public string Email { get; set; } = string.Empty;
		public string PhoneNumber { get; set; } = string.Empty;
		public string Address { get; set; }= string.Empty;

	}

	public class OrderItemDto : BaseDto
    {

        public ProductForCartDto Product { get; set; }

      
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal TotalPrice { get; set; }
        public DateTime OrderedAt { get; set; }
    }

    public class PaymentDto : BaseDto
    {
        public string CustomerId { get; set; } = string.Empty;
        public int PaymentMethodId { get; set; }
        public PaymentMethodDto? PaymentMethod { get; set; }
        public int PaymentProviderId { get; set; }
        public PaymentProviderDto? PaymentProvider { get; set; }
        public decimal Amount { get; set; }
        public DateTime PaymentDate { get; set; }
        public int OrderId { get; set; }
        public OrderStatus Status { get; set; } 
    }

    
   





}