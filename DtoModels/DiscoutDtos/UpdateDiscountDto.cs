using System.ComponentModel.DataAnnotations;

namespace E_Commers.DtoModels.DiscoutDtos
{
	public class UpdateDiscountDto
	{
		[StringLength(100, ErrorMessage = "Discount name cannot exceed 100 characters")]
		public string? Name { get; set; }

		[Range(1, 100, ErrorMessage = "Discount percentage must be between 1 and 100")]
		public decimal? DiscountPercent { get; set; }

		public bool? IsActive { get; set; }

		public DateTime? StartDate { get; set; }

		public DateTime? EndDate { get; set; }

		public string? Description { get; set; }
	}
} 