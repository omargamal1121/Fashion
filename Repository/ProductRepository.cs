using E_Commers.Context;
using E_Commers.Services;
using E_Commers.Interfaces;
using E_Commers.Models;
using E_Commers.Enums;
using Microsoft.EntityFrameworkCore;
using StackExchange.Redis;
using System.Collections;

namespace E_Commers.Repository
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
