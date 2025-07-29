using E_Commerce.Context;
using E_Commerce.DtoModels.SubCategorydto;
using E_Commerce.Interfaces;
using E_Commerce.Models;
using E_Commerce.UOW;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using static Microsoft.EntityFrameworkCore.DbLoggerCategory;
using static Microsoft.Extensions.Logging.EventSource.LoggingEventSource;

public class SubCategoryRepository : MainRepository<SubCategory>, ISubCategoryRepository
{
    private readonly DbSet<SubCategory> _subCategories;
    private readonly ILogger<SubCategoryRepository> _logger;

    public SubCategoryRepository(AppDbContext context, ILogger<SubCategoryRepository> logger) : base(context, logger)
    {
        _subCategories = context.Set<SubCategory>();
        _logger = logger;
    }

	private IQueryable<SubCategory> BasicFilter(IQueryable<SubCategory> query, bool? isActive, bool? DeletedOnly)
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
	public async Task<SubCategory?> GetSubCategoryWithImageById(int id, bool? isActive = null, bool? isDeleted = null)
	{
		var query = _subCategories.AsQueryable();

		query = query.Where(c => c.Id == id);
		var Subcategory = await query
			.Include(sc => sc.Images)
			.FirstOrDefaultAsync();
		return Subcategory;

	}
	public async Task<bool> IsExsistAndActive(int id)
	{
		return await _subCategories.AnyAsync(sc => sc.Id == id && sc.IsActive && sc.DeletedAt == null);
	}
	public async Task<bool> IsExsistAndDeActive(int id)
	{
		return await _subCategories.AnyAsync(sc => sc.Id == id && !sc.IsActive && sc.DeletedAt != null);
	}

	public async Task<bool> ActiveSubCategoryAsync(int id)
	{
		var subcategory=await GetByIdAsync(id);
		if(subcategory==null||subcategory.IsActive) return false;
		subcategory.IsActive = true;
		return true;
	}
	public async Task<bool> DeActiveSubCategoryAsync(int id)
	{
		var subcategory=await GetByIdAsync(id);
		if(subcategory==null||subcategory.IsActive) return false;
		subcategory.IsActive = false;
		return true;
	}

	public static Expression<Func<SubCategory, SubCategory>> MapToDtoWithDataExpression =>
	subCategory => new SubCategory
	{
		Id = subCategory.Id,
		Name = subCategory.Name,
		IsActive = subCategory.IsActive,
		CreatedAt = subCategory.CreatedAt,
		ModifiedAt = subCategory.ModifiedAt,
		DeletedAt = subCategory.DeletedAt,
		Description = subCategory.Description,

		Images = subCategory.Images
			.Where(img => img.DeletedAt == null)
			.Select(img => new Image
			{
				Id = img.Id,
				IsMain = img.IsMain,
				Url = img.Url
			}).ToList(),

		Products = subCategory.Products.Select(p => new Product
		{
			
			CreatedAt = p.CreatedAt,
			DeletedAt = p.DeletedAt,
			Description = p.Description,
			Discount= new Discount { 
				Name=p.Discount.Name,
				StartDate= p.Discount.StartDate,
				EndDate= p.Discount.EndDate,
				IsActive =p.IsActive,
				DeletedAt=p.Discount.DeletedAt,
				DiscountPercent=p.Discount.DiscountPercent,
				Id= p.Discount.Id
			},
			
			Id = p.Id,
			Name = p.Name,
			IsActive = p.IsActive,
			ModifiedAt = p.ModifiedAt,
			fitType = p.fitType,
			Gender = p.Gender,
			Price = p.Price,
			SubCategoryId = p.SubCategoryId,

			Images = p.Images
				.Where(img => img.DeletedAt == null)
				.Select(img => new Image
				{
					Id = img.Id,
					IsMain = img.IsMain,
					Url = img.Url
				}).ToList()
		}).ToList()
	};

	public static Expression<Func<SubCategory, SubCategory>> MapToDtoExpression =>
subCategory => new SubCategory
{
Id = subCategory.Id,
Name = subCategory.Name,
IsActive = subCategory.IsActive,
CreatedAt = subCategory.CreatedAt,
ModifiedAt = subCategory.ModifiedAt,
DeletedAt = subCategory.DeletedAt,
Description = subCategory.Description,
Images = subCategory.Images
	.Where(img => img.DeletedAt == null)
	.Select(img => new Image
	{
		Id = img.Id,
		Url = img.Url
	}).ToList(),

};

	public async Task<List<SubCategory>> FilterSubCategoryAsync(string search, bool? isActive = null, bool? isDeleted = null, int page = 1, int pagesize = 10)
	{
		var query = GetAll().AsNoTracking();
		if (!string.IsNullOrWhiteSpace(search))
			query = query.Where(sc => EF.Functions.Like(sc.Name, $"%{search}%") ||
		EF.Functions.Like(sc.Description, $"%{search}%"));
		query = BasicFilter(query, isActive, isDeleted);

		var subCategories = await query
			   .OrderBy(sc => sc.Id)
			   .Skip((page - 1) * pagesize)
			   .Take(pagesize)
			   .Select(MapToDtoExpression)
			   .ToListAsync();
		return subCategories;
	}
	public async Task<bool> IsHasActiveProduct(int subCategoryId)
	{
		return await GetAll()
			.AsNoTracking()
			.AnyAsync(sc => sc.Id == subCategoryId && sc.Products.Any(p => p.IsActive && p.DeletedAt == null));
	}


	public async Task<SubCategory?> GetSubCategoryById(int id, bool? isActive = null,bool ? isDeleted = null)
    {
		var query = _subCategories.AsQueryable();

		query = query.Where(c => c.Id == id);

		query = BasicFilter(query, isActive, isDeleted);
		var Subcategory = await query
			.Include(sc => sc.Images)
			.Include(sc =>sc.Products.Where(sc => sc.DeletedAt == null && sc.IsActive)).
			ThenInclude(p=>p.Discount).Include(sc => sc.Products)
			.ThenInclude(sc => sc.Images)
			.FirstOrDefaultAsync();

		if (Subcategory == null)
		{
			_logger.LogWarning($"Category with id: {id} doesn't exist");
			return null;
		}

		_logger.LogInformation($"Category with id: {id} exists");
		return Subcategory;
	}


	public async Task<bool> IsExsistByName(string name)
	{

		_logger.LogInformation($"Executing {nameof(IsExsistByName)} for name: {name}");

		var exists = await _subCategories.AnyAsync(c => c.Name == name);

		if (exists)
			_logger.LogInformation($"Category with name: {name} already exists");
		else
			_logger.LogInformation($"Category with name: {name} does not exist");

		return exists;

	}

	public async Task<bool> HasImagesAsync(int subCategoryId)
	{
		return await _subCategories.AnyAsync(sc => sc.Id == subCategoryId && sc.Images.Any(i=>i.DeletedAt==null));
	}
	public async Task<bool> HasProductsAsync(int subCategoryId)
    {
        return await _subCategories.AnyAsync(sc => sc.Id == subCategoryId && sc.Products.Any());
    }
} 