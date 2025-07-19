using E_Commers.DtoModels.CategoryDtos;
using E_Commers.DtoModels.CollectionDtos;
using E_Commers.DtoModels.DiscoutDtos;
using E_Commers.DtoModels.ImagesDtos;
using E_Commers.DtoModels.ProductDtos;
using E_Commers.DtoModels.Responses;
using E_Commers.Enums;
using E_Commers.ErrorHnadling;
using E_Commers.Interfaces;
using E_Commers.Models;
using E_Commers.Services.EmailServices;
using E_Commers.UOW;
using Microsoft.EntityFrameworkCore;
using E_Commers.Services.Cache;
using Hangfire;

namespace E_Commers.Services.ProductServices
{
	public interface IProductSearchService
	{
	
		Task<Result<List<ProductListItemDto>>> GetNewArrivalsAsync(int page, int pageSize, bool? isActive = null, bool? deletedOnly = null);
		Task<Result<List<ProductListItemDto>>> GetBestSellersAsync(int page, int pageSize, bool? isActive = null, bool? deletedOnly = null);
		Task<Result<List<ProductListItemDto>>> AdvancedSearchAsync(AdvancedSearchDto searchCriteria, int page, int pageSize, bool? isActive = null, bool? deletedOnly = null);
	}

	public class ProductSearchService : IProductSearchService
	{
		private readonly IBackgroundJobClient _backgroundJobClient;
		private readonly IUnitOfWork _unitOfWork;
		private readonly ILogger<ProductSearchService> _logger;
		private readonly IErrorNotificationService _errorNotificationService;
		private readonly ICacheManager _cacheManager;
		public const string CACHE_TAG_PRODUCT_SEARCH = "product_search";
		public const string CACHE_TAG_SUBCATEGORY = "subcategory";
		public static readonly string[] PRODUCT_CACHE_TAGS = new[] { CACHE_TAG_PRODUCT_SEARCH, CACHE_TAG_SUBCATEGORY };

		public ProductSearchService(
			IBackgroundJobClient backgroundJobClient,
			IUnitOfWork unitOfWork,
			ILogger<ProductSearchService> logger,
			IErrorNotificationService errorNotificationService,
			ICacheManager cacheManager)
		{
			_backgroundJobClient = backgroundJobClient;
			_unitOfWork = unitOfWork;
			_logger = logger;
			_errorNotificationService = errorNotificationService;
			_cacheManager = cacheManager;
		}

		private async Task SaveAndRemoveProductCacheAsync()
		{
			await _unitOfWork.CommitAsync();
			BackgroundJob.Enqueue(() => _cacheManager.RemoveByTagsAsync(PRODUCT_CACHE_TAGS));
		}
	
		

		
		private IQueryable<E_Commers.Models.Product> BasicFilter(IQueryable<E_Commers.Models. Product> query,bool? isActive,bool? DeletedOnly)
		{
			if (isActive.HasValue)
			{
				if (isActive.Value)
					query = query.Where(p => p.IsActive);
				else
					query = query.Where(p => !p.IsActive);
			}
			if (DeletedOnly.HasValue)
			{
				if (DeletedOnly.Value)
					query = query.Where(p => p.DeletedAt != null);
				else
					query = query.Where(p => p.DeletedAt == null);
			}
			return query;
		}

	

	
	
		public async Task<Result<List<ProductListItemDto>>> GetNewArrivalsAsync(int page, int pageSize, bool? isActive = null, bool? deletedOnly = null)
		{
			string cacheKey = $"newarrivals_{page}_{pageSize}_{isActive}_{deletedOnly}";
			var cached = await _cacheManager.GetAsync<Result<List<ProductListItemDto>>>(cacheKey);
			if (cached != null)
				return cached;
			try
			{
				var thirtyDaysAgo = DateTime.UtcNow.AddDays(-30);
				var query = _unitOfWork.Product.GetAll().Where(p => p.CreatedAt >= thirtyDaysAgo);

				query = BasicFilter(query, isActive, deletedOnly);
				var products = await query
					.OrderByDescending(p => p.CreatedAt)
					.Skip((page - 1) * pageSize)
					.Take(pageSize)
					.Select(p => new ProductListItemDto
					{
						Id = p.Id,
						Name = p.Name,
						Description = p.Description,
						AvailableQuantity = p.Quantity,
						Gender = p.Gender,
						SubCategoryId = p.SubCategoryId,
						Price = p.Price,
						PriceAfterDiscount = p.Discount != null && p.Discount.IsActive ? p.Price - (p.Price * (p.Discount.DiscountPercent / 100m)) : p.Price,
						Discount = p.Discount != null ? new DiscountDto
						{
							Id = p.Discount.Id,
							Name = p.Discount.Name,
							Description = p.Discount.Description,
							DiscountPercent = p.Discount.DiscountPercent,
							StartDate = p.Discount.StartDate,
							EndDate = p.Discount.EndDate,
							IsActive = p.Discount.IsActive,
							CreatedAt = p.Discount.CreatedAt,
							ModifiedAt = p.Discount.ModifiedAt,
							DeletedAt = p.Discount.DeletedAt,
							products = null
						} : null,
						Images = p.Images.Where(i => i.DeletedAt == null).Select(i => new ImageDto { Id = i.Id, Url = i.Url }).ToList()
					})
					.ToListAsync();

				Result<List<ProductListItemDto>> result;
				if (!products.Any())
					result = Result<List<ProductListItemDto>>.Fail("No new arrivals found", 404);
				else
					result = Result<List<ProductListItemDto>>.Ok(products, $"Found {products.Count} new arrivals", 200);
				BackgroundJob.Enqueue(() => _cacheManager.SetAsync(cacheKey, result, TimeSpan.FromMinutes(2), new[] { CACHE_TAG_PRODUCT_SEARCH }));
				return result;
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error in GetNewArrivalsAsync");
				 	_backgroundJobClient.Enqueue(()=> _errorNotificationService.SendErrorNotificationAsync(ex.Message, ex.StackTrace));
				return Result<List<ProductListItemDto>>.Fail("Error retrieving new arrivals", 500);
			}
		}
		private ProductListItemDto convertToProductListItemDto(E_Commers.Models.Product p)
		{
			return new ProductListItemDto
			{
				Id = p.Id,
				Name = p.Name,
				Description = p.Description,
				AvailableQuantity = p.Quantity,
				Gender = p.Gender,
				SubCategoryId = p.SubCategoryId,
				Price = p.Price,
				PriceAfterDiscount = p.Discount != null && p.Discount.IsActive ? p.Price - (p.Price * (p.Discount.DiscountPercent / 100m)) : p.Price,
				Discount = p.Discount != null ? new DiscountDto
				{
					Id = p.Discount.Id,
					Name = p.Discount.Name,
					Description = p.Discount.Description,
					DiscountPercent = p.Discount.DiscountPercent,
					StartDate = p.Discount.StartDate,
					EndDate = p.Discount.EndDate,
					IsActive = p.Discount.IsActive,
					CreatedAt = p.Discount.CreatedAt,
					ModifiedAt = p.Discount.ModifiedAt,
					DeletedAt = p.Discount.DeletedAt,
					products = null
				} : null,
				Images = p.Images.Where(i => i.DeletedAt == null).Select(i => new ImageDto { Id = i.Id, Url = i.Url }).ToList()
			};
		}

		public async Task<Result<List<ProductListItemDto>>> GetBestSellersAsync(int page, int pageSize, bool? isActive = null, bool? deletedOnly = null)
		{
			string cacheKey = $"bestsellers_{page}_{pageSize}_{isActive}_{deletedOnly}";
			var cached = await _cacheManager.GetAsync<Result<List<ProductListItemDto>>>(cacheKey);
			if (cached != null)
				return cached;
			try
			{
				var query = _unitOfWork.Product.GetAll();
				query = BasicFilter(query, isActive, deletedOnly);

				var bestSellers = await query
					.Select(p => new {
						Product = p,
						TotalSold = p.OrderItems.Where(oi => oi.Order.Status == E_Commers.Enums.OrderStatus.Delivered).Sum(oi => (int?)oi.Quantity) ?? 0
					})
					.OrderByDescending(x => x.TotalSold)
					.Skip((page - 1) * pageSize)
					.Take(pageSize)
					.Select(x => new ProductListItemDto
					{
						Id = x.Product.Id,
						Name = x.Product.Name,
						Description = x.Product.Description,
						AvailableQuantity = x.Product.Quantity,
						Gender = x.Product.Gender,
						SubCategoryId = x.Product.SubCategoryId,
						Price = x.Product.Price,
						PriceAfterDiscount = x.Product.Discount != null && x.Product.Discount.IsActive ? x.Product.Price - (x.Product.Price * (x.Product.Discount.DiscountPercent / 100m)) : x.Product.Price,
						Discount = x.Product.Discount != null ? new DiscountDto
						{
							Id = x.Product.Discount.Id,
							Name = x.Product.Discount.Name,
							Description = x.Product.Discount.Description,
							DiscountPercent = x.Product.Discount.DiscountPercent,
							StartDate = x.Product.Discount.StartDate,
							EndDate = x.Product.Discount.EndDate,
							IsActive = x.Product.Discount.IsActive,
							CreatedAt = x.Product.Discount.CreatedAt,
							ModifiedAt = x.Product.Discount.ModifiedAt,
							DeletedAt = x.Product.Discount.DeletedAt,
							products = null
						} : null,
						Images = x.Product.Images.Where(i => i.DeletedAt == null).Select(i => new ImageDto { Id = i.Id, Url = i.Url }).ToList()
					})
					.ToListAsync();

				Result<List<ProductListItemDto>> result;
				if (!bestSellers.Any())
					result = Result<List<ProductListItemDto>>.Fail("No best sellers found", 404);
				else
					result = Result<List<ProductListItemDto>>.Ok(bestSellers, $"Found {bestSellers.Count} best sellers", 200);
				BackgroundJob.Enqueue(() => _cacheManager.SetAsync(cacheKey, result, TimeSpan.FromMinutes(2), new[] { CACHE_TAG_PRODUCT_SEARCH }));
				return result;
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error in GetBestSellersAsync");
				 	_backgroundJobClient.Enqueue(()=> _errorNotificationService.SendErrorNotificationAsync(ex.Message, ex.StackTrace));
				return Result<List<ProductListItemDto>>.Fail("Error retrieving best sellers", 500);
			}
		}

		public async Task<Result<List<ProductListItemDto>>> AdvancedSearchAsync(AdvancedSearchDto searchCriteria, int page, int pageSize, bool? isActive = null, bool? deletedOnly = null)
		{
			string cacheKey = $"advsearch_{searchCriteria?.SearchTerm}_{searchCriteria?.Subcategoryid}_{searchCriteria?.Gender}_{searchCriteria?.FitType}_{searchCriteria?.MinPrice}_{searchCriteria?.MaxPrice}_{searchCriteria?.InStock}_{searchCriteria?.OnSale}_{searchCriteria?.SortBy}_{searchCriteria?.SortDescending}_{page}_{pageSize}_{isActive}_{deletedOnly}";
			var cached = await _cacheManager.GetAsync<Result<List<ProductListItemDto>>>(cacheKey);
			if (cached != null)
				return cached;
			try
			{
				var query = _unitOfWork.Product.GetAll();
				query = BasicFilter(query, isActive, deletedOnly);

				if (!string.IsNullOrWhiteSpace(searchCriteria.SearchTerm))
				{
					query = query.Where(p => p.Name.Contains(searchCriteria.SearchTerm) || 
						p.Description.Contains(searchCriteria.SearchTerm));
				}

				if (searchCriteria.Subcategoryid.HasValue)
				{
					query = query.Where(p => p.SubCategoryId == searchCriteria.Subcategoryid.Value);
				}

				if (searchCriteria.Gender.HasValue)
				{
					query = query.Where(p => p.Gender == searchCriteria.Gender.Value);
				}

				if (searchCriteria.FitType.HasValue)
				{
					query = query.Where(p => p.fitType == (FitType)searchCriteria.FitType.Value);
				}

				if (searchCriteria.MinPrice.HasValue || searchCriteria.MaxPrice.HasValue)
				{
					if (searchCriteria.MinPrice.HasValue && searchCriteria.MaxPrice.HasValue)
					{
						query = query.Where(p =>
							(p.Price >= searchCriteria.MinPrice.Value && p.Price <= searchCriteria.MaxPrice.Value)
							||
							(p.FinalPrice.HasValue && p.FinalPrice.Value >= searchCriteria.MinPrice.Value && p.FinalPrice.Value <= searchCriteria.MaxPrice.Value)
						);
					}
					else if (searchCriteria.MinPrice.HasValue)
					{
						query = query.Where(p =>
							p.Price >= searchCriteria.MinPrice.Value
							||
							(p.FinalPrice.HasValue && p.FinalPrice.Value >= searchCriteria.MinPrice.Value)
						);
					}
					else if (searchCriteria.MaxPrice.HasValue)
					{
						query = query.Where(p =>
							p.Price <= searchCriteria.MaxPrice.Value
							||
							(p.FinalPrice.HasValue && p.FinalPrice.Value <= searchCriteria.MaxPrice.Value)
						);
					}
				}

				if (searchCriteria.InStock.HasValue)
				{
					if (searchCriteria.InStock.Value)
					{
						query = query.Where(p => p.Quantity > 0);
					}
					else
					{
						query = query.Where(p => p.Quantity == 0);
					}
				}

				if (searchCriteria.OnSale.HasValue && searchCriteria.OnSale.Value)
				{
					query = query.Where(p => p.Discount != null && 
						p.Discount.IsActive && 
						p.Discount.DeletedAt == null &&
						p.Discount.StartDate <= DateTime.UtcNow &&
						p.Discount.EndDate >= DateTime.UtcNow);
				}
				switch (searchCriteria.SortBy?.ToLower())
				{
					case "name":
						query = searchCriteria.SortDescending ? query.OrderByDescending(p => p.Name) : query.OrderBy(p => p.Name);
						break;
					case "price":
						query = searchCriteria.SortDescending ? 
							query.OrderByDescending(p => p.Price) : 
							query.OrderBy(p => p.Price);
						break;
					case "newest":
						query = searchCriteria.SortDescending ? query.OrderByDescending(p => p.CreatedAt) : query.OrderBy(p => p.CreatedAt);
						break;
					default:
						query = query.OrderBy(p => p.Name);
						break;
				}

				var products = await query
					.Skip((page - 1) * pageSize)
					.Take(pageSize)
					.Select(p => new ProductListItemDto
					{
						Id = p.Id,
						Name = p.Name,
						Description = p.Description,
						AvailableQuantity = p.Quantity,
						Gender = p.Gender,
						SubCategoryId = p.SubCategoryId,
						Price = p.Price,
						PriceAfterDiscount = p.Discount != null && p.Discount.IsActive ? p.Price - (p.Price * (p.Discount.DiscountPercent / 100m)) : p.Price,
						Discount = p.Discount != null ? new DiscountDto 
						{ 
							Id = p.Discount.Id, 
							DiscountPercent = p.Discount.DiscountPercent, 
							IsActive = p.Discount.IsActive,
							StartDate = p.Discount.StartDate,
							EndDate = p.Discount.EndDate,
							Name = p.Discount.Name,
							Description = p.Discount.Description
						} : null,
						Images = p.Images.Where(i => i.DeletedAt == null).Select(i => new ImageDto { Id = i.Id, Url = i.Url }).ToList()
					})
					.ToListAsync();

				Result<List<ProductListItemDto>> result;
				if (!products.Any())
					result = Result<List<ProductListItemDto>>.Fail("No products found matching the search criteria", 404);
				else
					result = Result<List<ProductListItemDto>>.Ok(products, $"Found {products.Count} products matching search criteria", 200);
				BackgroundJob.Enqueue(() => _cacheManager.SetAsync(cacheKey, result, TimeSpan.FromMinutes(2), new[] { CACHE_TAG_PRODUCT_SEARCH }));
				return result;
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error in AdvancedSearchAsync");
				 	_backgroundJobClient.Enqueue(()=> _errorNotificationService.SendErrorNotificationAsync(ex.Message, ex.StackTrace));
				return Result<List<ProductListItemDto>>.Fail("Error performing advanced search", 500);
			}
		}
	}

	public class AdvancedSearchDto
	{
		public string? SearchTerm { get; set; }
		public int? Subcategoryid { get; set; }
		public Gender? Gender { get; set; }
		public  FitType? FitType { get; set; }
		public decimal? MinPrice { get; set; }
		public decimal? MaxPrice { get; set; }
		public bool? InStock { get; set; }
		public bool? OnSale { get; set; }
		public string? SortBy { get; set; }
		public bool SortDescending { get; set; } = false;
		public string? Color { get; set; } // Filter by variant color
		public decimal? MinSize { get; set; } // Minimum variant size
		public decimal? MaxSize { get; set; } // Maximum variant size
	}
} 