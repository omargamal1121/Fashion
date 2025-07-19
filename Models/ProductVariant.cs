using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace E_Commers.Models
{
    public class ProductVariant : BaseEntity
    {
        [Required]
        public string Color { get; set; }
        
        public string? Size { get; set; }
        public int? Waist { get; set; }
        public int? Length { get; set; }
        public int? FitType { get; set; }

        [Required]
        public int Quantity { get; set; }

        [Required]
        public int ProductId { get; set; }
        public Product Product { get; set; }
		public ICollection<CartItem> CartItems { get; set; } = new List<CartItem>();
		public ICollection<OrderItem> OrderItems { get; set; } = new List<OrderItem>();
	}
} 