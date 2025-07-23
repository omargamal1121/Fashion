using E_Commerce.Context;
using E_Commerce.Services;
using E_Commerce.Interfaces;
using E_Commerce.Models;
using E_Commerce.Enums;
using Microsoft.EntityFrameworkCore;
using StackExchange.Redis;
using System.Collections;

namespace E_Commerce.Repository
{
	public class ProductRepository : MainRepository<Product>, IProductRepository
	{
		private readonly DbSet<Product> _entity;
		private readonly ILogger<ProductRepository> _logger;

		public ProductRepository( AppDbContext context, ILogger<ProductRepository> logger) : base(context, logger)
		{
			_logger = logger;
			_entity = context.Products;
		}

		
		private IQueryable<Product> GetQueryableProducts(bool? isActive = null, bool? deletedOnly = null)
		{
			var query = _entity.AsQueryable();
			if (isActive.HasValue)
				query = query.Where(p => p.IsActive == isActive.Value);
			if (deletedOnly.HasValue)
			{
				if (deletedOnly.Value)
					query = query.Where(p => p.DeletedAt != null);
				else
					query = query.Where(p => p.DeletedAt == null);
			}
			return query;
		}
		public async Task<Product?> GetProductWithSimbleDataByIdAsync(int id, bool? isActive = null, bool? deletedOnly = null)
		{ 
			var query = GetQueryableProducts(isActive, deletedOnly);
			var product = await query.FirstOrDefaultAsync(p => p.Id == id);

			return product;

		}


		public async Task<Product?> GetProductByIdAsync(int id, bool? isActive = null, bool? deletedOnly = null)
		{
			var query = GetQueryableProducts(isActive, deletedOnly);

			var product= await query.Include(i => i.Images).Include(p => p.Discount).FirstOrDefaultAsync(p=>p.Id==id);
			return product;


		}

		public async Task< bool> IsExsistByNameAsync(string name)=> await _entity.AnyAsync(p => p.Name == name);
		
		public async Task<Product> GetProductWithVariants(int productId)
		{
			return await _entity
				.Include(p => p.ProductVariants)
				.FirstOrDefaultAsync(p => p.Id == productId && p.DeletedAt == null);
		}
	}
}
