using E_Commers.Services;
using E_Commers.Models;
using E_Commers.Enums;

namespace E_Commers.Interfaces
{
	public interface IProductRepository:IRepository<Product>
	{
		Task<Product> GetProductByIdAsync(int id, bool? isActive, bool? isDeleted);
		public  Task<bool> IsExsistByNameAsync(string name);
		Task<Product> GetProductWithVariants(int productId);
	}
}
