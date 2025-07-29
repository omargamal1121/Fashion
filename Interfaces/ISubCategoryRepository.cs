using E_Commerce.Models;
using System.Linq;
using System.Threading.Tasks;

namespace E_Commerce.Interfaces
{
    public interface ISubCategoryRepository : IRepository<SubCategory>
    {

        public Task<SubCategory?> GetSubCategoryById(int id, bool? isActive = null, bool? isDeleted = null);
        public  Task<bool> IsExsistAndActive(int id);
        public  Task<bool> IsExsistAndDeActive(int id);
        public Task<bool> ActiveSubCategoryAsync(int id);
        public Task<bool> DeActiveSubCategoryAsync(int id);

		public  Task<List<SubCategory>> FilterSubCategoryAsync(string search, bool? isActive = null, bool? isDeleted = null, int page = 1, int pagesize = 10);

		public  Task<SubCategory?> GetSubCategoryWithImageById(int id, bool? isActive = null, bool? isDeleted = null);
        public  Task<bool> IsHasActiveProduct(int subCategoryId);
        public  Task<bool> HasImagesAsync(int subCategoryId);
		public Task< bool> IsExsistByName(string name);
        Task<bool> HasProductsAsync(int subCategoryId);
    }
} 