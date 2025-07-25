using E_Commerce.Services;
using E_Commerce.Models;

namespace E_Commerce.Interfaces
{
	public interface IProductVariantRepository : IRepository<ProductVariant>
	{
		// Basic Operations
		public Task<bool> VariantExistsAsync(int id);
		public Task<ProductVariant?> GetVariantById(int id);
		public Task<List<ProductVariant>> GetVariantsByProductId(int productId);
		
		// Price Management
	//	public Task<bool> UpdateVariantPriceAsync(int variantId, decimal newPrice);
		public Task<bool> UpdateVariantQuantityAsync(int variantId, int newQuantity);
		
		// Search and Filter
		public Task<List<ProductVariant>> GetVariantsByColorAsync(string color);
	
		//public Task<List<ProductVariant>> GetVariantsByPriceRangeAsync(decimal minPrice, decimal maxPrice);
		public Task<List<ProductVariant>> GetVariantsInStockAsync();
		
		// Statistics
		//public Task<decimal> GetAverageVariantPriceAsync();
		//public Task<decimal> GetMinVariantPriceAsync();
		//public Task<decimal> GetMaxVariantPriceAsync();
	}
} 