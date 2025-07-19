using AutoMapper;
using E_Commers.DtoModels.CategoryDtos;
using E_Commers.DtoModels.DiscoutDtos;
using E_Commers.DtoModels.ImagesDtos;
using E_Commers.DtoModels.ProductDtos;
using E_Commers.DtoModels.Responses;
using E_Commers.Enums;
using E_Commers.ErrorHnadling;
using E_Commers.Interfaces;
using E_Commers.Models;
using E_Commers.Services.AdminOpreationServices;
using E_Commers.Services.EmailServices;
using E_Commers.UOW;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;
using System.Threading.Tasks;
using E_Commers.Services.ProductServices; // For IProductSearchService and AdvancedSearchDto

namespace E_Commers.Services.Product
{
	public interface IProductsServices
	{
		// Core product operations (delegated to ProductCatalogService)
		Task<Result<ProductDetailDto>> GetProductByIdAsync(int id, bool? isActive = null, bool? deletedOnly = null);
		Task<Result<ProductListItemDto>> CreateProductAsync(CreateProductDto dto, string userId);
		Task<Result<ProductListItemDto>> UpdateProductAsync(int id, UpdateProductDto dto, string userId);
		Task<Result<string>> DeleteProductAsync(int id, string userId);
		Task<Result<ProductListItemDto>> RestoreProductAsync(int id, string userId);
		Task<Result<List<ProductListItemDto>>> GetProductsBySubCategoryId(int subCategoryId);
		// Search operations (delegated to ProductSearchService)
		Task<Result<List<ProductListItemDto>>> GetNewArrivalsAsync(int page, int pageSize, bool? isActive = null, bool? deletedOnly = null);
		Task<Result<List<ProductListItemDto>>> GetBestSellersAsync(int page, int pageSize, bool? isActive = null, bool? deletedOnly = null);
		Task<Result<List<ProductListItemDto>>> AdvancedSearchAsync(AdvancedSearchDto searchCriteria, int page, int pageSize, bool? isActive = null, bool? deletedOnly = null);
		// Image operations (delegated to ProductImageService)
		Task<Result<List<ImageDto>>> GetProductImagesAsync(int productId);
		Task<Result<List<ImageDto>>> AddProductImagesAsync(int productId, List<IFormFile> images, string userId);
		Task<Result<bool>> RemoveProductImageAsync(int productId, int imageId, string userId);
		Task<Result<bool>> UploadAndSetMainImageAsync(int productId, IFormFile mainImage, string userId);
		// Variant operations (delegated to ProductVariantService)
		Task<Result<List<ProductVariantDto>>> GetProductVariantsAsync(int productId);
		Task<Result<ProductVariantDto>> AddVariantAsync(int productId, CreateProductVariantDto dto, string userId);
		Task<Result<ProductVariantDto>> UpdateVariantAsync(int variantId, UpdateProductVariantDto dto, string userId);
		Task<Result<string>> DeleteVariantAsync(int variantId, string userId);
		Task<Result<string>> UpdateVariantQuantityAsync(int variantId, int newQuantity, string userId);
		// Discount operations (delegated to ProductDiscountService)
		Task<Result<DiscountDto>> GetProductDiscountAsync(int productId);
		Task<Result<ProductDetailDto>> AddDiscountToProductAsync(int productId, int discountId, string userId);
		Task<Result<ProductDetailDto>> UpdateProductDiscountAsync(int productId, int discountId, string userId);
		Task<Result<ProductDetailDto>> RemoveDiscountFromProductAsync(int productId, string userId);
	}

	public class ProductsServices : IProductsServices
	{
		private readonly IProductCatalogService _productCatalogService;
		private readonly IProductSearchService _productSearchService;
		private readonly IProductImageService _productImageService;
		private readonly IProductVariantService _productVariantService;
		private readonly IProductDiscountService _productDiscountService;
		private readonly ILogger<ProductsServices> _logger;

		public ProductsServices(
			IProductCatalogService productCatalogService,
			IProductSearchService productSearchService,
			IProductImageService productImageService,
			IProductVariantService productVariantService,
			IProductDiscountService productDiscountService,
			ILogger<ProductsServices> logger)
		{
			_productCatalogService = productCatalogService;
			_productSearchService = productSearchService;
			_productImageService = productImageService;
			_productVariantService = productVariantService;
			_productDiscountService = productDiscountService;
			_logger = logger;
		}

		// Core Product Operations
		public async Task<Result<ProductDetailDto>> GetProductByIdAsync(int id, bool? isActive = null, bool? deletedOnly = null)
		{
			return await _productCatalogService.GetProductByIdAsync(id, isActive, deletedOnly);
		}
		public async Task<Result<ProductListItemDto>> CreateProductAsync(CreateProductDto dto, string userId)
		{
			return await _productCatalogService.CreateProductAsync(dto, userId);
		}
		public async Task<Result<ProductListItemDto>> UpdateProductAsync(int id, UpdateProductDto dto, string userId)
		{
			return await _productCatalogService.UpdateProductAsync(id, dto, userId);
		}
		public async Task<Result<string>> DeleteProductAsync(int id, string userId)
		{
			return await _productCatalogService.DeleteProductAsync(id, userId);
		}
		public async Task<Result<ProductListItemDto>> RestoreProductAsync(int id, string userId)
		{
			return await _productCatalogService.RestoreProductAsync(id, userId);
		}
		public async Task<Result<List<ProductListItemDto>>> GetProductsBySubCategoryId(int subCategoryId)
		{
			return await _productCatalogService.GetProductsBySubCategoryId(subCategoryId);
		}
		// Search Operations
		public async Task<Result<List<ProductListItemDto>>> GetNewArrivalsAsync(int page, int pageSize, bool? isActive = null, bool? deletedOnly = null)
		{
			return await _productSearchService.GetNewArrivalsAsync(page, pageSize, isActive, deletedOnly);
		}
		public async Task<Result<List<ProductListItemDto>>> GetBestSellersAsync(int page, int pageSize, bool? isActive = null, bool? deletedOnly = null)
		{
			return await _productSearchService.GetBestSellersAsync(page, pageSize, isActive, deletedOnly);
		}
		public async Task<Result<List<ProductListItemDto>>> AdvancedSearchAsync(AdvancedSearchDto searchCriteria, int page, int pageSize, bool? isActive = null, bool? deletedOnly = null)
		{
			return await _productSearchService.AdvancedSearchAsync(searchCriteria, page, pageSize, isActive, deletedOnly);
		}
		// Image Operations
		public async Task<Result<List<ImageDto>>> GetProductImagesAsync(int productId)
		{
			return await _productImageService.GetProductImagesAsync(productId);
		}
		public async Task<Result<List<ImageDto>>> AddProductImagesAsync(int productId, List<IFormFile> images, string userId)
		{
			return await _productImageService.AddProductImagesAsync(productId, images, userId);
		}
		public async Task<Result<bool>> RemoveProductImageAsync(int productId, int imageId, string userId)
		{
			return await _productImageService.RemoveProductImageAsync(productId, imageId, userId);
		}
		public async Task<Result<bool>> UploadAndSetMainImageAsync(int productId, IFormFile mainImage, string userId)
		{
			return await _productImageService.UploadAndSetMainImageAsync(productId, mainImage, userId);
		}
		// Variant Operations
		public async Task<Result<List<ProductVariantDto>>> GetProductVariantsAsync(int productId)
		{
			return await _productVariantService.GetProductVariantsAsync(productId);
		}
		public async Task<Result<ProductVariantDto>> AddVariantAsync(int productId, CreateProductVariantDto dto, string userId)
		{
			return await _productVariantService.AddVariantAsync(productId, dto, userId);
		}
		public async Task<Result<ProductVariantDto>> UpdateVariantAsync(int variantId, UpdateProductVariantDto dto, string userId)
		{
			return await _productVariantService.UpdateVariantAsync(variantId, dto, userId);
		}
		public async Task<Result<string>> DeleteVariantAsync(int variantId, string userId)
		{
			return await _productVariantService.DeleteVariantAsync(variantId, userId);
		}
	
		public async Task<Result<string>> UpdateVariantQuantityAsync(int variantId, int newQuantity, string userId)
		{
			return await _productVariantService.UpdateVariantQuantityAsync(variantId, newQuantity, userId);
		}
		// Discount Operations
		public async Task<Result<DiscountDto>> GetProductDiscountAsync(int productId)
		{
			return await _productDiscountService.GetProductDiscountAsync(productId);
		}
		public async Task<Result<ProductDetailDto>> AddDiscountToProductAsync(int productId, int discountId, string userId)
		{
			return await _productDiscountService.AddDiscountToProductAsync(productId, discountId, userId);
		}
		public async Task<Result<ProductDetailDto>> UpdateProductDiscountAsync(int productId, int discountId, string userId)
		{
			return await _productDiscountService.UpdateProductDiscountAsync(productId, discountId, userId);
		}
		public async Task<Result<ProductDetailDto>> RemoveDiscountFromProductAsync(int productId, string userId)
		{
			return await _productDiscountService.RemoveDiscountFromProductAsync(productId, userId);
		}
	}
}
