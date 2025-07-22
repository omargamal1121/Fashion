using E_Commers.Context;
using E_Commers.Interfaces;
using E_Commers.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Linq;
using System.Threading.Tasks;
using static Microsoft.EntityFrameworkCore.DbLoggerCategory;

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


	public bool IsExsistByName(string name)
	{

		_logger.LogInformation($"Executing {nameof(IsExsistByName)} for name: {name}");

		var exists = _subCategories.Any(c => c.Name == name);

		if (exists)
			_logger.LogInformation($"Category with name: {name} already exists");
		else
			_logger.LogInformation($"Category with name: {name} does not exist");

		return exists;

	}
	

    public async Task<bool> HasProductsAsync(int subCategoryId)
    {
        return await _subCategories.AnyAsync(sc => sc.Id == subCategoryId && sc.Products.Any());
    }
} 