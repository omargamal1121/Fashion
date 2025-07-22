using E_Commers.Services;
using E_Commers.Models;
using System.Linq;
using System.Threading.Tasks;

namespace E_Commers.Interfaces
{
	public interface ICategoryRepository:IRepository<Category>
	{
		public Task<Category?> GetCategoryByIdAsync(int id, bool? isActiveFilter = null,bool?deleted=null);
		Task<bool> HasSubCategoriesAsync(int categoryId);
		public bool IsExsistsByName(string name);




	}
}
