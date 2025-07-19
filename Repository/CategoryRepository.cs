﻿using E_Commers.Context;
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

	public async Task<bool> CategoryExistsAsync(int id)
	{
		_logger.LogInformation($"Executing {nameof(CategoryExistsAsync)} Id: {id}");
		var category = await GetByIdAsync(id);
		if (category == null)
		{
			_logger.LogWarning($"No Category With this id:{id}");
			return false;
		}
		_logger.LogInformation("Category found");
		return true;
	}

	public async Task<Category?> GetCategoryById(int id, bool isActiveFilter = false)
	{
		_logger.LogInformation($"Executing {nameof(GetCategoryById)} for id: {id}");
		var query = _categories
			.Include(c => c.Images)
			.Include(c => c.SubCategories
				.Where(s => s.DeletedAt == null && (!isActiveFilter || s.IsActive)))
			.ThenInclude(s => s.Images);

		var category = await query.FirstOrDefaultAsync(c => c.Id == id && c.DeletedAt == null);

		if (category == null)
		{
			_logger.LogWarning($"Category with id: {id} doesn't exist");
			return null;
		}

		_logger.LogInformation($"Category with id: {id} exists");
		return category;
	}

	public async Task<Category?> FindByNameAsync(string name)
	{
		return await _categories.FirstOrDefaultAsync(c => c.Name == name && c.DeletedAt == null);
	}

	public IQueryable<Category> FindByNameContains(string partialName, bool? activeOnly = null, bool? dletedOnly = null)
	{
		if (string.IsNullOrWhiteSpace(partialName))
			return Enumerable.Empty<Category>().AsQueryable();

		var query = _categories
			.Include(c => c.Images)
			.Include(c => c.SubCategories.Where(s => s.DeletedAt == null))
				.ThenInclude(s => s.Images)
			.Where(c => EF.Functions.Like(c.Name, $"%{partialName}%"));

		if (dletedOnly.HasValue)
		{
			if (dletedOnly.Value)
				query = query.Where(c => c.DeletedAt != null);
			else
				query = query.Where(c => c.DeletedAt == null);
		}
		else
		{
			query = query.Where(c => c.DeletedAt == null);
		}

		if (activeOnly.HasValue && activeOnly.Value)
		{
			query = query.Where(c => c.IsActive);
		}

		return query;
	}

	public (IQueryable<Category> Query, int TotalCount) FindByNameContainsPaged(string partialName, bool? activeOnly, bool? dletedOnly, int page, int pageSize)
	{
		var query = FindByNameContains(partialName, activeOnly, dletedOnly);
		int totalCount = query.Count();
		var paged = query.Skip((page - 1) * pageSize).Take(pageSize);
		return (paged, totalCount);
	}

	public async Task<bool> HasSubCategoriesAsync(int categoryId)
	{
		return await _categories.AnyAsync(c => c.Id == categoryId && c.SubCategories.Any(sc => sc.DeletedAt == null));
	}
}
