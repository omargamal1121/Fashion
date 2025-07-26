using E_Commerce.DtoModels.DiscoutDtos;
using E_Commerce.DtoModels.Shared;
using E_Commerce.Models;
using System.ComponentModel.DataAnnotations;
using E_Commerce.DtoModels.InventoryDtos;
using E_Commerce.DtoModels.ImagesDtos;
using E_Commerce.DtoModels.Responses;
using E_Commerce.DtoModels.ProductDtos;
using E_Commerce.Enums;
using E_Commerce.DtoModels.CollectionDtos;
using E_Commerce.DtoModels.SubCategorydto;

namespace E_Commerce.DtoModels.ProductDtos
{
	public class ProductDto:BaseDto
	{
		[RegularExpression(@"^[a-zA-Z0-9][a-zA-Z0-9\s\-,]*[a-zA-Z0-9]$", ErrorMessage = "Name must start and end with an alphanumeric character and can contain spaces, hyphens, and commas in between.")]
		public string Name { get; set; } = string.Empty;
		
		[RegularExpression(@"^[\w\s.,\-()'\""]{0,500}$", ErrorMessage = "Description can contain up to 500 characters: letters, numbers, spaces, and .,-()'\"")]
		public string Description { get; set; } = string.Empty;
		
		public int SubCategoryId { get; set; }
		public int AvailableQuantity { get; set; }
		public decimal Price { get; set; }
		public decimal? FinalPrice { get; set; }
		public Gender Gender { get; set; }
		public FitType  fitType { get; set; }
		public decimal? DiscountPrecentage { get; set; }
		public string? DiscountName { get; set; }

		public DateTime? EndAt { get; set; }


		public bool IsActive { get; set; }
		public IEnumerable<ImageDto> images { get; set; }



	}
	public class ProductForCartDto
	{
		public int Id { get; set; }

		public string Name { get; set; } = string.Empty;

		public decimal Price { get; set; }

		public decimal? FinalPrice { get; set; }

		public string? DiscountName { get; set; }

		public decimal? DiscountPrecentage { get; set; }

		public string? MainImageUrl { get; set; } 

		public bool IsActive { get; set; }
	}


	public class ProductVariantDto : BaseDto
	{
		public string Color { get; set; } = string.Empty;
		public VariantSize? Size { get; set; }
		public int? Waist { get; set; }
		public int? Length { get; set; }
		public int Quantity { get; set; }
		public int ProductId { get; set; }
		public bool IsActive { get; set; }

	}
	public class ProductVariantForCartDto : BaseDto
	{
		public string Color { get; set; } = string.Empty;
		public VariantSize? Size { get; set; }
		public int? Waist { get; set; }
		public int? Length { get; set; }
		public int Quantity { get; set; }

	}


	public class ReviewDto : BaseDto
	{
		public int Rating { get; set; }
		public string Comment { get; set; } = string.Empty;
		public string UserId { get; set; } = string.Empty;
		public string UserName { get; set; } = string.Empty;
		public int ProductId { get; set; }
	}

	public class WishlistItemDto : BaseDto
	{
		public int ProductId { get; set; }
		public string UserId { get; set; } = string.Empty;
		public ProductDto? Product { get; set; }
	}

	public class ReturnRequestProductDto : BaseDto
	{
		public int ReturnRequestId { get; set; }
		public int ProductId { get; set; }
		public int Quantity { get; set; }
		public string Reason { get; set; } = string.Empty;
		public ReturnStatus Status { get; set; }
	}

	public enum ReturnStatus
	{
		Pending = 1,
		Approved = 2,
		Rejected = 3,
		Completed = 4
	}

	public class ProductListItemDto : BaseDto
	{
		public string Name { get; set; } = string.Empty;
		public string Description { get; set; } = string.Empty;
		public int SubCategoryId { get; set; }
		public DiscountDto? Discount { get; set; }
		public int AvailableQuantity { get; set; }
		public decimal Price { get; set; }
		public Gender Gender { get; set; }
		public decimal? PriceAfterDiscount { get; set; }
		public IEnumerable<ImageDto>? Images { get; set; }

		public static ProductListItemDto FromDetail(ProductDetailDto dto)
		{
			return new ProductListItemDto
			{
				Id = dto.Id,
				Name = dto.Name,
				Description = dto.Description,
				SubCategoryId = dto.SubCategoryId,
				Discount = dto.Discount,
				AvailableQuantity = dto.AvailableQuantity,
				Price = dto.Price,
				Gender = dto.Gender,
				Images = dto.Images
			};
		}
	}

	public class ProductDetailDto : BaseDto
	{
		public string Name { get; set; } = string.Empty;
		public string Description { get; set; } = string.Empty;
		public int SubCategoryId { get; set; }
		public DiscountDto? Discount { get; set; }
		public int AvailableQuantity { get; set; }
		public decimal Price { get; set; }
		public Gender Gender { get; set; }
		public bool IsActive { get; set; }
		public  FitType fitType { get; set; }

		public IEnumerable<ImageDto>? Images { get; set; }
		public IEnumerable<ProductVariantDto>? Variants { get; set; }
		public decimal? FinalPrice { get; set; }
	}
}
