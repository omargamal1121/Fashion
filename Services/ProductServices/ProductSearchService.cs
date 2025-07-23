using E_Commerce.DtoModels.CategoryDtos;
using E_Commerce.DtoModels.CollectionDtos;
using E_Commerce.DtoModels.DiscoutDtos;
using E_Commerce.DtoModels.ImagesDtos;
using E_Commerce.DtoModels.ProductDtos;
using E_Commerce.DtoModels.Responses;
using E_Commerce.Enums;
using E_Commerce.ErrorHnadling;
using E_Commerce.Interfaces;
using E_Commerce.Models;
using E_Commerce.Services.EmailServices;
using E_Commerce.UOW;
using Microsoft.EntityFrameworkCore;
using E_Commerce.Services.Cache;
using Hangfire;
using System.Linq.Expressions;

namespace E_Commerce.Services.ProductServices
{
	public interface IProductSearchService
	{
	
		Task<Result<List<ProductDto>>> GetNewArrivalsAsync(int page, int pageSize, bool? isActive = null, bool? deletedOnly = null);
		Task<Result<List<ProductDto>>> GetBestSellersAsync(int page, int pageSize, bool? isActive = null, bool? deletedOnly = null);
		Task<Result<List<ProductDto>>> AdvancedSearchAsync(AdvancedSearchDto searchCriteria, int page, int pageSize, bool? isActive = null, bool? deletedOnly = null);
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
	
		

		
		private IQueryable<E_Commerce.Models.Product> BasicFilter(IQueryable<E_Commerce.Models. Product> query,bool? isActive,bool? DeletedOnly)
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




		private Expression<Func<E_Commerce.Models.Product, ProductDto>> maptoproductdto = p =>
		new ProductDto
		{
			Id = p.Id,
			Name = p.Name,
			Description = p.Description,
			AvailableQuantity = p.Quantity,
			Gender = p.Gender,
			SubCategoryId = p.SubCategoryId,
			Price = p.Price,
			CreatedAt = p.CreatedAt,
			ModifiedAt = p.ModifiedAt,
			DeletedAt = p.DeletedAt,
			FinalPrice = (p.Discount != null && p.Discount.IsActive && (p.Discount.DeletedAt == null) && ( p.Discount.EndDate > DateTime.UtcNow)) ? Math.Round(p.Price - (p.Discount.DiscountPercent * p.Price)) : p.Price,

			fitType = p.fitType,
			images = p.Images.Where(i => i.DeletedAt == null).Select(i => new ImageDto { Id = i.Id, Url = i.Url }).ToList(),
			EndAt = (p.Discount != null && p.Discount.IsActive && p.Discount.EndDate > DateTime.UtcNow) && p.Discount.IsActive ? p.Discount.EndDate : null,
			DiscountName = (p.Discount != null && p.Discount.IsActive && p.Discount.EndDate > DateTime.UtcNow) ? p.Discount.Name : null,
			DiscountPrecentage = (p.Discount != null && p.Discount.IsActive && p.Discount.EndDate > DateTime.UtcNow) ? p.Discount.DiscountPercent : 0,
			IsActive = p.IsActive,
		};



		public async Task<Result<List<ProductDto>>> GetNewArrivalsAsync(int page, int pageSize, bool? isActive = null, bool? deletedOnly = null)
		{
			string cacheKey = $"newarrivals_{page}_{pageSize}_{isActive}_{deletedOnly}";
			var cached = await _cacheManager.GetAsync<Result<List<ProductDto>>>(cacheKey);
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
					.Select(maptoproductdto)
					.ToListAsync();

				Result<List<ProductDto>> result;
				if (!products.Any())
					result = Result<List<ProductDto>>.Fail("No new arrivals found", 404);
				else
					result = Result<List<ProductDto>>.Ok(products, $"Found {products.Count} new arrivals", 200);
				BackgroundJob.Enqueue(() => _cacheManager.SetAsync(cacheKey, result, TimeSpan.FromMinutes(2), new[] { CACHE_TAG_PRODUCT_SEARCH }));
				return result;
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error in GetNewArrivalsAsync");
				 	_backgroundJobClient.Enqueue(()=> _errorNotificationService.SendErrorNotificationAsync(ex.Message, ex.StackTrace));
				return Result<List<ProductDto>>.Fail("Error retrieving new arrivals", 500);
			}
		}
		private ProductListItemDto convertToProductListItemDto(E_Commerce.Models.Product p)
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

		public async Task<Result<List<ProductDto>>> GetBestSellersAsync(int page, int pageSize, bool? isActive = null, bool? deletedOnly = null)
		{
			string cacheKey = $"bestsellers_{page}_{pageSize}_{isActive}_{deletedOnly}";
			var cached = await _cacheManager.GetAsync<Result<List<ProductDto>>>(cacheKey);
			if (cached != null)
				return cached;
			try
			{
				var query = _unitOfWork.Product.GetAll();
				query = BasicFilter(query, isActive, deletedOnly);

				var bestSellers = await query
	.Select(p => new {
		Product = p,
		TotalSold = p.OrderItems
			.Where(oi => oi.Order.Status == E_Commerce.Enums.OrderStatus.Delivered)
			.Sum(oi => (int?)oi.Quantity) ?? 0
	})
	.OrderByDescending(x => x.TotalSold)
	.Skip((page - 1) * pageSize)
	.Take(pageSize)
	.Select(x => new ProductDto
	{
		Id = x.Product.Id,
		Name = x.Product.Name,
		Description = x.Product.Description,
		AvailableQuantity = x.Product.Quantity,
		Gender = x.Product.Gender,
		SubCategoryId = x.Product.SubCategoryId,
		Price = x.Product.Price,
		CreatedAt = x.Product.CreatedAt,
		ModifiedAt = x.Product.ModifiedAt,
		DeletedAt = x.Product.DeletedAt,
		FinalPrice = (x.Product.Discount != null && x.Product.Discount.IsActive && (x.Product.Discount.DeletedAt == null) && (x.Product.Discount.EndDate == null || x.Product.Discount.EndDate > DateTime.UtcNow)) ? Math.Round(x.Product.Price - (x.Product.Discount.DiscountPercent * x.Product.Price)) : x.Product .Price,

		fitType = x.Product.fitType,
		images = x.Product.Images
			.Where(i => i.DeletedAt == null)
			.Select(i => new ImageDto { Id = i.Id, Url = i.Url })
			.ToList(),
		EndAt = (x.Product.Discount != null && x.Product.Discount.IsActive && x.Product.Discount.EndDate > DateTime.UtcNow)
			? x.Product.Discount.EndDate
			: null,
		DiscountName = (x.Product.Discount != null && x.Product.Discount.IsActive && x.Product.Discount.EndDate > DateTime.UtcNow)
			? x.Product.Discount.Name
			: null,
		DiscountPrecentage = (x.Product.Discount != null && x.Product.Discount.IsActive && x.Product.Discount.EndDate > DateTime.UtcNow)
			? x.Product.Discount.DiscountPercent
			: 0,
		IsActive = x.Product.IsActive
	})
	.ToListAsync();


				Result<List<ProductDto>> result;
				if (!bestSellers.Any())
					result = Result<List<ProductDto>>.Fail("No best sellers found", 404);
				else
					result = Result<List<ProductDto>>.Ok(bestSellers, $"Found {bestSellers.Count} best sellers", 200);
				BackgroundJob.Enqueue(() => _cacheManager.SetAsync(cacheKey, result, TimeSpan.FromMinutes(2), new[] { CACHE_TAG_PRODUCT_SEARCH }));
				return result;
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error in GetBestSellersAsync");
				 	_backgroundJobClient.Enqueue(()=> _errorNotificationService.SendErrorNotificationAsync(ex.Message, ex.StackTrace));
				return Result<List<ProductDto>>.Fail("Error retrieving best sellers", 500);
			}
		}

		public async Task<Result<List<ProductDto>>> AdvancedSearchAsync(AdvancedSearchDto searchCriteria, int page, int pageSize, bool? isActive = null, bool? deletedOnly = null)
		{
			string cacheKey = $"advsearch_{searchCriteria?.SearchTerm}_{searchCriteria?.Subcategoryid}_{searchCriteria?.Gender}_{searchCriteria?.FitType}_{searchCriteria?.MinPrice}_{searchCriteria?.MaxPrice}_{searchCriteria?.InStock}_{searchCriteria?.OnSale}_{searchCriteria?.SortBy}_{searchCriteria?.SortDescending}_{page}_{pageSize}_{isActive}_{deletedOnly}";
			var cached = await _cacheManager.GetAsync<Result<List<ProductDto>>>(cacheKey);
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
					.Select(maptoproductdto)
					.ToListAsync();
				if (searchCriteria.MinPrice.HasValue || searchCriteria.MaxPrice.HasValue)
				{
					if (searchCriteria.MinPrice.HasValue && searchCriteria.MaxPrice.HasValue)
					{
						products = products.Where(p =>
							(p.Price >= searchCriteria.MinPrice.Value && p.Price <= searchCriteria.MaxPrice.Value)
							||
							(p.FinalPrice.HasValue && p.FinalPrice.Value >= searchCriteria.MinPrice.Value && p.FinalPrice.Value <= searchCriteria.MaxPrice.Value)
						).ToList();
					}
					else if (searchCriteria.MinPrice.HasValue)
					{
						products = products.Where(p =>
							p.Price >= searchCriteria.MinPrice.Value
							||
							(p.FinalPrice.HasValue && p.FinalPrice.Value >= searchCriteria.MinPrice.Value)
						).ToList();
					}
					else if (searchCriteria.MaxPrice.HasValue)
					{
						products = products.Where(p =>
							p.Price <= searchCriteria.MaxPrice.Value
							||
							(p.FinalPrice.HasValue && p.FinalPrice.Value <= searchCriteria.MaxPrice.Value)
						).ToList();
					}
				}

				Result<List<ProductDto>> result;
				if (!products.Any())
					result = Result<List<ProductDto>>.Fail("No products found matching the search criteria", 404);
				else
					result = Result<List<ProductDto>>.Ok(products, $"Found {products.Count} products matching search criteria", 200);
				BackgroundJob.Enqueue(() => _cacheManager.SetAsync(cacheKey, result, TimeSpan.FromMinutes(2), new[] { CACHE_TAG_PRODUCT_SEARCH }));
				return result;
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error in AdvancedSearchAsync");
				 	_backgroundJobClient.Enqueue(()=> _errorNotificationService.SendErrorNotificationAsync(ex.Message, ex.StackTrace));
				return Result<List<ProductDto>>.Fail("Error performing advanced search", 500);
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