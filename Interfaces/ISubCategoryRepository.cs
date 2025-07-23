using E_Commerce.Models;
using System.Linq;
using System.Threading.Tasks;

namespace E_Commerce.Interfaces
{
    public interface ISubCategoryRepository : IRepository<SubCategory>
    {

        public Task<SubCategory?> GetSubCategoryById(int id, bool? isActive = null, bool? isDeleted = null);
        public  Task<SubCategory?> GetSubCategoryWithImageById(int id, bool? isActive = null, bool? isDeleted = null);

		public bool IsExsistByName(string name);
        Task<bool> HasProductsAsync(int subCategoryId);
    }
} 