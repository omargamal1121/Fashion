using E_Commerce.Services;
using E_Commerce.Models;
using E_Commerce.Enums;

namespace E_Commerce.Interfaces
{
	public interface IProductRepository:IRepository<Product>
	{
		Task<Product> GetProductByIdAsync(int id, bool? isActive, bool? isDeleted);
		public  Task<bool> IsExsistByNameAsync(string name);
		Task<Product> GetProductWithVariants(int productId);
	}
}
