using E_Commerce.Enums;
using E_Commerce.Models;
using E_Commerce.Services;
using static Dapper.SqlMapper;

namespace E_Commerce.Interfaces
{
	public interface IProductRepository:IRepository<Product>
	{
		Task<Product> GetProductByIdAsync(int id, bool? isActive, bool? isDeleted);
		public  Task<bool> IsExsistByNameAsync(string name);
		public  Task<bool> IsExsistAndActive(int id) ;
		Task<Product> GetProductWithVariants(int productId);
	}
}
