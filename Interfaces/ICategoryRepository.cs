using E_Commerce.Services;
using E_Commerce.Models;
using System.Linq;
using System.Threading.Tasks;

namespace E_Commerce.Interfaces
{
	public interface ICategoryRepository:IRepository<Category>
	{

		public  Task<List<Category>> GetCategoriesAsync(string keyword, bool? isActive = null, bool? isDeleted = null, int page = 1, int pageSize = 10);
		public Task<Category?> GetCategoryByIdAsync(int id, bool? isActiveFilter = null,bool?deleted=null);
		Task<bool> HasSubCategoriesAsync(int categoryId);
		public  Task<bool> HasSubCategoriesActiveAsync(int categoryId);
		public Task<Category?> GetCategoryByIdWithImagesAsync(int id, bool? isActive = null, bool? isDeleted = null);
		public bool IsExsistsByName(string name);
		public  Task<Category?> GetCategoryByIdWithSubCategoryAsync(int id, bool? isActive = null, bool? isDeleted = null);




	}
}
