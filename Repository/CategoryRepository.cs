using E_Commers.Context;
using E_Commers.Interfaces;
using E_Commers.Models;
using E_Commers.Services;
using Microsoft.EntityFrameworkCore;
using System.Threading.Tasks;

public class CategoryRepository : MainRepository<Category>, ICategoryRepository
{
	private readonly DbSet<Category> _categories;
	private readonly ILogger<CategoryRepository> _logger;

	public CategoryRepository(AppDbContext context, ILogger<CategoryRepository> logger) : base(context, logger)
	{
		_categories = context.Categories;
		_logger = logger;
	}



	public async Task<Category?> GetCategoryByIdAsync(int id, bool? isActive = null, bool? isDeleted = null)
	{
		_logger.LogInformation($"Executing {nameof(GetCategoryByIdAsync)} for id: {id}");

		var query = _categories.AsQueryable();

		query = query.Where(c => c.Id == id);

		if (isActive.HasValue)
		{
			query = query.Where(c => c.IsActive == isActive.Value);
		}

		if (isDeleted.HasValue)
		{
			if (isDeleted.Value)
				query = query.Where(c => c.DeletedAt != null);
			else
				query = query.Where(c => c.DeletedAt == null);
		}

		var category = await query
			.Include(c => c.Images)
			.Include(c => c.SubCategories.Where(sc => sc.DeletedAt == null && sc.IsActive))
				.ThenInclude(sc => sc.Images)
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


	public async Task<bool> HasSubCategoriesAsync(int categoryId)
	{
		return await _categories.AnyAsync(c => c.Id == categoryId && c.SubCategories.Any());
	}
}
