using E_Commerce.Enums;

namespace E_Commerce.DtoModels.OrderDtos
{
	public class OrderListDto
	{
		public int Id { get; set; }
		public string OrderNumber { get; set; } = string.Empty;
		public string CustomerName { get; set; } = string.Empty;
		public OrderStatus Status { get; set; }
		public decimal Total { get; set; }
		public DateTime CreatedAt { get; set; }
	}







}