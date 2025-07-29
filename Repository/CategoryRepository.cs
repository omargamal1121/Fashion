using E_Commerce.Context;
using E_Commerce.DtoModels.CategoryDtos;
using E_Commerce.DtoModels.SubCategorydto;
using E_Commerce.Interfaces;
using E_Commerce.Models;
using E_Commerce.Services;
using E_Commerce.UOW;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;
using System.Threading.Tasks;
using static Microsoft.Extensions.Logging.EventSource.LoggingEventSource;

public class CategoryRepository : MainRepository<Category>, ICategoryRepository
{
	private readonly DbSet<Category> _categories;
	private readonly ILogger<CategoryRepository> _logger;

	public CategoryRepository(AppDbContext context, ILogger<CategoryRepository> logger) : base(context, logger)
	{
		_categories = context.Categories;
		_logger = logger;
	}


	private IQueryable<E_Commerce.Models.Category> BasicFilter(IQueryable<E_Commerce.Models.Category> query, bool? isActive = null, bool? isDeleted = null)
	{
		if (isActive.HasValue)
			query = query.Where(c => c.IsActive == isActive.Value);
		if (isDeleted.HasValue)
		{
			if (isDeleted.Value)
				query = query.Where(c => c.DeletedAt != null);
			else
				query = query.Where(c => c.DeletedAt == null);
		}
		return query;
	}

	private static readonly Expression<Func<E_Commerce.Models.Category, Category>> CategorySelector = c => new  Category
	{
		Id = c.Id,
		Name = c.Name,
		Description = c.Description,
		IsActive = c.IsActive,
		CreatedAt = c.ModifiedAt,
		DeletedAt = c.DeletedAt,
		ModifiedAt = c.ModifiedAt,
		DisplayOrder = c.DisplayOrder,
		Images = c.Images.Select(i => new Image
		{
			Id = i.Id,
			Url = i.Url,
			IsMain = i.IsMain
		}).ToList()
	};
	private static readonly Expression<Func<E_Commerce.Models.Category, Category>> CategorySelectorWithData = c => new Category
	{
		Id = c.Id,
		Name = c.Name,
		Description = c.Description,
		IsActive = c.IsActive,
		CreatedAt = c.ModifiedAt,
		DeletedAt = c.DeletedAt,
		ModifiedAt = c.ModifiedAt,
		DisplayOrder = c.DisplayOrder,
		SubCategories = c.SubCategories.Select(sc => new SubCategory
		{
			Id = sc.Id,
			Name = sc.Name,
			Description = sc.Description,
			IsActive = sc.IsActive,
			CreatedAt = sc.CreatedAt,
			ModifiedAt = sc.ModifiedAt,
			DeletedAt = sc.DeletedAt,
			Images = sc.Images.Select(i => new Image
			{
				Id = i.Id,
				Url = i.Url,
				IsMain = i.IsMain
			}).ToList()
		}).ToList(),
		Images = c.Images.Select(i => new Image
		{
			Id = i.Id,
			Url = i.Url,
			IsMain = i.IsMain
		}).ToList()
	};
	public async Task<Category?> GetCategoryByIdAsync(int id, bool? isActive = null, bool? isDeleted = null)
	{
		_logger.LogInformation($"Executing {nameof(GetCategoryByIdAsync)} for id: {id}");

		var query = _categories.AsQueryable();

		query = query.Where(c => c.Id == id);

		query= BasicFilter(query, isActive, isDeleted);

		var category = await query.Select(CategorySelectorWithData)
			.FirstOrDefaultAsync();

		if (category == null)
		{
			_logger.LogWarning($"Category with id: {id} doesn't exist");
			return null;
		}

		_logger.LogInformation($"Category with id: {id} exists");
		return category;
	}
	public async Task<Category?> GetCategoryByIdWithImagesAsync(int id, bool? isActive = null, bool? isDeleted = null)
	{
		_logger.LogInformation($"Executing {nameof(GetCategoryByIdAsync)} for id: {id}");

		var query = _categories.AsQueryable();

		query = query.Where(c => c.Id == id);

		query= BasicFilter(query, isActive, isDeleted);

		var category = await query
			.Include(c => c.Images)
			.FirstOrDefaultAsync();

		if (category == null)
		{
			_logger.LogWarning($"Category with id: {id} doesn't exist");
			return null;
		}

		_logger.LogInformation($"Category with id: {id} exists");
		return category;
	}
	public async Task<Category?> GetCategoryByIdWithSubCategoryAsync(int id, bool? isActive = null, bool? isDeleted = null)
	{
		_logger.LogInformation($"Executing {nameof(GetCategoryByIdAsync)} for id: {id}");

		var query = _categories.AsQueryable();

		query = query.Where(c => c.Id == id);

		query= BasicFilter(query, isActive, isDeleted);

		var category = await query
			.Include(c => c.SubCategories.Where(sc=>sc.DeletedAt==null&&sc.IsActive==true))
			.FirstOrDefaultAsync();

		if (category == null)
		{
			_logger.LogWarning($"Category with id: {id} doesn't exist");
			return null;
		}

		_logger.LogInformation($"Category with id: {id} exists");
		return category;
	}


	public bool IsExsistsByName(string name)
	{
		_logger.LogInformation($"Executing {nameof(IsExsistsByName)} for name: {name}");

		var exists = _categories.Any(c => c.Name == name);

		if (exists)
			_logger.LogInformation($"Category with name: {name} already exists");
		else
			_logger.LogInformation($"Category with name: {name} does not exist");

		return exists;
	}

	public async Task<List<Category>> GetCategoriesAsync(string keyword, bool? isActive = null, bool? isDeleted = null, int page = 1, int pageSize = 10)
	{
		var query = GetAll().AsNoTracking();
		if (!string.IsNullOrWhiteSpace(keyword))
			query = query.Where(c => EF.Functions.Like(c.Name, $"%{keyword}%") ||
		EF.Functions.Like(c.Description, $"%{keyword}%"));
		query = BasicFilter(query, isActive, isDeleted);
		var result = await query
			.OrderBy(c => c.DisplayOrder)
			.Skip((page - 1) * pageSize)
			.Take(pageSize)
			.Select(CategorySelector)
			.ToListAsync();
		return result;
	}


	public async Task<bool> HasSubCategoriesAsync(int categoryId)
	{
		return await _categories.AnyAsync(c => c.Id == categoryId && c.SubCategories.Any());
	}
	public async Task<bool> HasSubCategoriesActiveAsync(int categoryId)
	{
		return await _categories.AnyAsync(c => c.Id == categoryId && c.SubCategories.Any(sc=>sc.IsActive&&sc.DeletedAt==null));
	}
}
