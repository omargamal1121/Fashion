using E_Commers.DtoModels.CategoryDtos;
using E_Commers.DtoModels.ImagesDtos;
using E_Commers.Services;
using Microsoft.AspNetCore.Http;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace E_Commers.Interfaces
{
	public interface ICategoryServices
	{
		Task<Result<string>> IsExsistAsync(int id);
		Task<Result<List<CategoryDto>>> GetAllCategoriesAsync(bool? isActive = null, bool? isDeleted = null, int page = 1, int pageSize = 10);
		Task<Result<List<CategoryDto>>> SearchAsync(string key, bool? isActive = null, bool? isDeleted = null, int page = 1, int pageSize = 10);
		Task<Result<CategoryDto>> CreateAsync(CreateCategotyDto categoty, string userid);
		Task<Result<CategorywithdataDto>> GetCategoryByIdAsync(int id, bool? isActive = null, bool? includeDeleted = null);
		Task<Result<string>> DeleteAsync(int id, string userid);
		public Task<Result<bool>> ChangeActiveStatus(int categoryId, string userId);
		Task<Result<CategoryDto>> UpdateAsync(int categoryid, UpdateCategoryDto category, string userid);
		public  Task<Result<List<CategoryDto>>> FilterAsync(string? search,bool? isActive,bool?includeDeleted,int page,int pageSize);
		Task<Result<CategoryDto>> ReturnRemovedCategoryAsync(int id, string userid);
		//public Task<ApiResponse<List<SubCategoryDto>>> GetSubCategoriesAsync(int categoryId);
		Task<Result<List<ImageDto>>> AddImagesToCategoryAsync(int categoryId, List<IFormFile> images, string userId);
		Task<Result<ImageDto>> AddMainImageToCategoryAsync(int categoryId, IFormFile mainImage, string userId);
		Task<Result<CategoryDto>> RemoveImageFromCategoryAsync(int categoryId, int imageId, string userId);
		Task<Result<string>> ActivateCategoryAsync(int categoryId, string userId);
		Task<Result<string>> DeactivateCategoryAsync(int categoryId, string userId);
	}
}
