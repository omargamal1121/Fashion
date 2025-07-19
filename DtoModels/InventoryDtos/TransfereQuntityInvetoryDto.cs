﻿using System.ComponentModel.DataAnnotations;

namespace E_Commers.DtoModels.InventoryDtos
{
	public class TransfereQuantityInvetoryDto
	{
		public int FromInventoryId { get; set; }		
		public int ToInventoryId { get; set; }

		[Range(0, int.MaxValue, ErrorMessage = $"Invalid Product Id ")]
		[Required(ErrorMessage = "Product Id Required")]

		public int ProductId { get; set; }
		[Required(ErrorMessage = "Quantity Required")]

		[Range(0, int.MaxValue, ErrorMessage = $"Invalid Quantity Id ")]
		public int Quantity { get; set; }

	}
}
