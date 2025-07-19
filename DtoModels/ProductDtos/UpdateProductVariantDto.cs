using System.ComponentModel.DataAnnotations;
using E_Commers.Enums;

namespace E_Commers.DtoModels.ProductDtos
{
	public class UpdateProductVariantDto
	{
		

		[StringLength(20, MinimumLength = 2, ErrorMessage = "Color must be between 2 and 20 characters.")]
		public string? Color { get; set; }
		
		public VariantSize? Size { get; set; }
		
		[StringLength(10, ErrorMessage = "Waist must not exceed 10 characters.")]
		public int? Waist { get; set; }
		
		[StringLength(10, ErrorMessage = "Length must not exceed 10 characters.")]
		public int? Length { get; set; }
		
	
	}
}