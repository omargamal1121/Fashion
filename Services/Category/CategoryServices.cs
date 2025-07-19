using AutoMapper;
using E_Commers.DtoModels;
using E_Commers.DtoModels.CategoryDtos;
using E_Commers.DtoModels.ImagesDtos;
using E_Commers.DtoModels.ProductDtos;
using E_Commers.DtoModels.Responses;
using E_Commers.Enums;
using E_Commers.ErrorHnadling;
using E_Commers.Interfaces;
using E_Commers.Models;
using E_Commers.Services.AdminOpreationServices;
using E_Commers.Services.Cache;
using E_Commers.Services.EmailServices;
using E_Commers.UOW;
using Hangfire;
using Microsoft.EntityFrameworkCore;

namespace E_Commers.Services.Category
{
	public class CategoryServices : ICategoryServices
	{
		private readonly ILogger<CategoryServices> _logger;
		private readonly IMapper _mapping;
		private readonly IUnitOfWork _unitOfWork;
		private readonly IAdminOpreationServices _adminopreationservices;
		private readonly ICacheManager _cacheManager;
		private readonly IImagesServices _imagesServices;
		private const string CACHE_TAG_CATEGORY = "category";

		public CategoryServices(
			 IImagesServices imagesServices,
			IAdminOpreationServices adminopreationservices,
			ICacheManager cacheManager,
			IMapper mapping,
			IUnitOfWork unitOfWork,
			ILogger<CategoryServices> logger
		)
		{
			_imagesServices = imagesServices;
			_adminopreationservices = adminopreationservices;
			_cacheManager = cacheManager;
			_mapping = mapping;
			_logger = logger;
			_unitOfWork = unitOfWork;
		}

		private async Task SaveAndRemoveCategoryCacheAsync()
		{
			await _unitOfWork.CommitAsync();
			BackgroundJob.Enqueue(() => _cacheManager.RemoveByTagAsync(CACHE_TAG_CATEGORY));
		}

		public async Task<Result<List<CategoryDto>>> GetAllCategoriesAsync(bool? isActive = null, bool? isDeleted = null, int page = 1, int pageSize = 10)
		{
			_logger.LogInformation($"Executing {nameof(GetAllCategoriesAsync)} in CategoryService with isActive: {isActive}, isDeleted: {isDeleted}, page: {page}, pageSize: {pageSize}");
			var cacheKey = $"category_all_{isActive}_{isDeleted}_{page}_{pageSize}";
			var cached = await _cacheManager.GetAsync<List<CategoryDto>>(cacheKey);
			if (cached != null)
			{
				_logger.LogInformation($"Cache hit for GetAllCategoriesAsync with key: {cacheKey}");
				return Result<List<CategoryDto>>.Ok(cached, "Categories fetched from cache", 200);
			}
			(IQueryable<E_Commers.Models.Category> query, int totalCount) = _unitOfWork.Category.FindByNameContainsPaged("", isActive, isDeleted, page, pageSize);
			var result = await query.Select(c => new CategoryDto
			{
				Id = c.Id,
				Name = c.Name,
				Description = c.Description,
				IsActive = c.IsActive,
				CreatedAt = c.CreatedAt,
				UpdatedAt = null,
				Images = c.Images.Where(i => i.DeletedAt == null).Select(i => new ImageDto
				{
					Id = i.Id,
					Url = i.Url,
					IsMain = i.IsMain
				}).ToList()
			}).ToListAsync();
			BackgroundJob.Enqueue(() => _cacheManager.SetAsync(cacheKey, result, null, new[] { CACHE_TAG_CATEGORY }));
			return Result<List<CategoryDto>>.Ok(result, "Categories fetched", 200);
		}

		public async Task<Result<List<CategoryDto>>> SearchAsync(string keyword, bool? isActive = null, bool? isDeleted = null, int page = 1, int pageSize = 10)
		{
			_logger.LogInformation($"Executing {nameof(SearchAsync)} in CategoryService with keyword: {keyword}, isActive: {isActive}, isDeleted: {isDeleted}, page: {page}, pageSize: {pageSize}");
			var cacheKey = $"category_search_{keyword}_{isActive}_{isDeleted}_{page}_{pageSize}";
			var cached = await _cacheManager.GetAsync<List<CategoryDto>>(cacheKey);
			if (cached != null)
			{
				_logger.LogInformation($"Cache hit for SearchAsync with key: {cacheKey}");
				return Result<List<CategoryDto>>.Ok(cached, "Categories fetched from cache", 200);
			}
			if (string.IsNullOrWhiteSpace(keyword))
			{
				return await GetAllCategoriesAsync(isActive, isDeleted, page, pageSize);
			}
			(IQueryable<E_Commers.Models.Category> query, int totalCount) = _unitOfWork.Category.FindByNameContainsPaged(keyword, isActive, isDeleted, page, pageSize);
			var result = await query.Select(c => new CategoryDto
				{
					Id = c.Id,
					Name = c.Name,
					Description = c.Description,
					IsActive = c.IsActive,
					CreatedAt = c.CreatedAt,
					UpdatedAt = null,
					Images = c.Images.Select(i => new ImageDto
					{
						Id = i.Id,
						Url = i.Url,
						IsMain = i.IsMain
					}).ToList()
			}).ToListAsync();
			BackgroundJob.Enqueue(() => _cacheManager.SetAsync(cacheKey, result,null,  new[] { CACHE_TAG_CATEGORY }));
			return Result<List<CategoryDto>>.Ok(result, "Categories fetched", 200);
		}


		public async Task<Result<string>> IsExsistAsync(int id)
		{
			_logger.LogInformation($"Execute:{nameof(IsExsistAsync)} in Category Services");
			var exists = await _unitOfWork.Category.GetByIdAsync(id) != null;
			if (exists)
				return Result<string>.Ok(null, "category Exsist", 200);
			else
				return Result<string>.Fail($"No Categoty with this id:{id}", 404);
		}

		public async Task<Result<CategorywithdataDto>> GetCategoryByIdAsync(int id, bool? isActive = null, bool IsDeleted = false)
		{
			_logger.LogInformation($"Execute:{nameof(GetCategoryByIdAsync)} in services for id: {id}, isActive: {isActive}, includeDeleted: {IsDeleted}");
			var cacheKey = $"{CACHE_TAG_CATEGORY}id:{id}_active:{isActive}_deleted:{IsDeleted}";
			var cachedCategory = await _cacheManager.GetAsync<CategorywithdataDto>(cacheKey);
			if (cachedCategory != null)
			{
				_logger.LogInformation($"Cache hit for category {id} with filters");
				return Result<CategorywithdataDto>.Ok(cachedCategory, "Category found in cache", 200);
			}
			var query = _unitOfWork.Category.GetAll()
				.Where(c => c.Id == id && (IsDeleted || c.DeletedAt == null) && (isActive == null || c.IsActive == isActive))
				.Select(category => new CategorywithdataDto
				{
					Id = category.Id,
					Name = category.Name,
					Description = category.Description,
					DisplayOrder = category.DisplayOrder,
					IsActive = category.IsActive,
					CreatedAt = category.CreatedAt,
					UpdatedAt = null,
					
					Images = category.Images.Where(i=>i.DeletedAt==null).Select(i => new ImageDto
					{
						Id = i.Id,
						Url = i.Url,
						IsMain = i.IsMain
					}).ToList(),
					SubCategories = category.SubCategories.Where(sc => sc.DeletedAt == null&&sc.IsActive).Select(sc => new SubCategoryDto
					{
						Id = sc.Id,
						Name = sc.Name,
						CreatedAt = sc.CreatedAt,
						Description = sc.Description,
						IsActive = sc.IsActive,
						Images = sc.Images.Where(i=>i.DeletedAt==null).Select(i => new ImageDto
						{
							Id = i.Id,
							Url = i.Url,
							IsMain = i.IsMain
						}).ToList()
					}).ToList()
				});
			var categoryDto = await query.FirstOrDefaultAsync();
			if (categoryDto == null)
			{
				_logger.LogWarning($"Category with id: {id} not found");
				return Result<CategorywithdataDto>.Fail($"Category with id: {id} not found", 404);
			}
			BackgroundJob.Enqueue(() => _cacheManager.SetAsync(cacheKey, categoryDto,null,  new[] { CACHE_TAG_CATEGORY }));
			return Result<CategorywithdataDto>.Ok(categoryDto, "Category found", 200);
		}

		private void NotifyAdminOfError(string message, string? stackTrace = null)
		{
			BackgroundJob.Enqueue<IErrorNotificationService>(_ => _.SendErrorNotificationAsync(message, stackTrace));
		}

		public async Task<Result<CategoryDto>> CreateAsync(CreateCategotyDto model, string userId)
		{
			_logger.LogInformation($"Execute {nameof(CreateAsync)}");
			if (string.IsNullOrWhiteSpace(model.Name))
			{
				return Result<CategoryDto>.Fail("Category name cannot be empty", 400);
			}
			var isexsist = await _unitOfWork.Category.FindByNameAsync(model.Name);
			if (isexsist != null)
			{
				return Result<CategoryDto>.Fail($"thier's category with this name:{model.Name}", 409);
			}

			using var transaction = await _unitOfWork.BeginTransactionAsync();
			try
			{
				var category = _mapping.Map<Models.Category>(model);
				var creationResult = await _unitOfWork.Category.CreateAsync(category);
				if (creationResult == null)
				{
					_logger.LogWarning("Failed to create category");
					NotifyAdminOfError($"Failed to create category '{model.Name}'");
					await transaction.RollbackAsync();
					return Result<CategoryDto>.Fail("Can't create category now... try again later", 500);
				}
				await _unitOfWork.CommitAsync();
				await transaction.CommitAsync();
				var adminLog = await _adminopreationservices.AddAdminOpreationAsync(
					"Add Category",
					Opreations.AddOpreation,
					userId,
					category.Id
				);
				if (!adminLog.Success)
				{
					_logger.LogError(adminLog.Message);
					NotifyAdminOfError($"Failed to log admin operation for category '{model.Name}' (ID: {category.Id})");
					return Result<CategoryDto>.Fail("Try Again later", 500);
				}
				await SaveAndRemoveCategoryCacheAsync();
				var categoryWithImages = await _unitOfWork.Category.GetCategoryById(category.Id);
				if (categoryWithImages == null)
				{
					_logger.LogError("Failed to retrieve created category");
					NotifyAdminOfError($"Failed to retrieve created category with ID {category.Id} after creation");
					return Result<CategoryDto>.Fail("Category created but failed to retrieve details", 500);
				}
					var categoryDto = _mapping.Map<CategoryDto>(categoryWithImages);
				return Result<CategoryDto>.Ok(categoryDto, "Created", 201);
			}
			catch (Exception ex)
			{
				await transaction.RollbackAsync();
				_logger.LogError($"Exception in CreateAsync: {ex.Message}");
				NotifyAdminOfError($"Exception in CreateAsync: {ex.Message}");
				return Result<CategoryDto>.Fail("Server error occurred while creating category", 500);
			}
		}

		public async Task<Result<List<ImageDto>>> AddImagesToCategoryAsync(int categoryId, List<IFormFile> images, string userId)
		{
			_logger.LogInformation($"Executing {nameof(AddImagesToCategoryAsync)} for categoryId: {categoryId}");
			if (images == null || !images.Any())
			{
				return Result<List<ImageDto>>.Fail("At least one image is required.", 400);
			}
			
			using var transaction = await _unitOfWork.BeginTransactionAsync();
			try
			{
				var category = await _unitOfWork.Category.GetCategoryById(categoryId);
				if (category == null)
				{
					await transaction.RollbackAsync();
					return Result<List<ImageDto>>.Fail($"Category with id {categoryId} not found", 404);
				}
				
				var imageResult = await _imagesServices.SaveCategoryImagesAsync(images, userId);
				if (!imageResult.Success || imageResult.Data == null)
				{
					await transaction.RollbackAsync();
					return Result<List<ImageDto>>.Fail($"Failed to save images: {imageResult.Message}", 400);
				}
				
				foreach (var img in imageResult.Data)
				{
					category.Images.Add(img);
				}
				
				var updateResult = _unitOfWork.Category.Update(category);
				if (!updateResult)
				{
					await transaction.RollbackAsync();
					return Result<List<ImageDto>>.Fail($"Failed to update category with new images", 500);
				}
				
				await _unitOfWork.CommitAsync();
				await transaction.CommitAsync();
				
				var mapped = _mapping.Map<List<ImageDto>>(category.Images);
				return Result<List<ImageDto>>.Ok(mapped, $"Added {imageResult.Data.Count} images to category", 200, warnings: imageResult.Warnings);
			}
			catch (Exception ex)
			{
				await transaction.RollbackAsync();
				_logger.LogError($"Exception in AddImagesToCategoryAsync: {ex.Message}");
				NotifyAdminOfError($"Exception in AddImagesToCategoryAsync: {ex.Message}", ex.StackTrace);
				return Result<List<ImageDto>>.Fail("An error occurred while adding images", 500);
			}
		}

		public async Task<Result<string>> DeleteAsync(int categoryId, string userid)
		{
			_logger.LogInformation($"Executing {nameof(DeleteAsync)} for categoryId: {categoryId}");
			
			using var transaction = await _unitOfWork.BeginTransactionAsync();
			try
			{
				var category = await _unitOfWork.Category.GetCategoryById(categoryId);
				if (category == null)
				{
					await transaction.RollbackAsync();
					return Result<string>.Fail($"Category with id {categoryId} not found", 404);
				}
				
				// Check if category has subcategories
				var hasSubCategories = await _unitOfWork.Category.HasSubCategoriesAsync(categoryId);
				if (hasSubCategories)
				{
					await transaction.RollbackAsync();
					_logger.LogWarning($"Category {categoryId} contains subcategories");
					return Result<string>.Fail("Can't delete category because it has subcategories", 400);
				}
				
				var deleteResult = await _unitOfWork.Category.SoftDeleteAsync(categoryId);
				if (!deleteResult)
				{
					await transaction.RollbackAsync();
					return Result<string>.Fail($"Failed to delete category", 500);
				}
				
				var adminLog = await _adminopreationservices.AddAdminOpreationAsync(
					$"Deleted Category {categoryId}",
					Opreations.DeleteOpreation,
					userid,
					categoryId
				);
				
				if (!adminLog.Success)
				{
					_logger.LogWarning($"Failed to log admin operation: {adminLog.Message}");
				}
				
				await _unitOfWork.CommitAsync();
				await transaction.CommitAsync();
				await SaveAndRemoveCategoryCacheAsync();
				
				return Result<string>.Ok(null, $"Category with ID {categoryId} deleted successfully", 200);
			}
			catch (Exception ex)
			{
				await transaction.RollbackAsync();
				_logger.LogError($"Exception in DeleteAsync: {ex.Message}");
				NotifyAdminOfError($"Exception in DeleteAsync: {ex.Message}", ex.StackTrace);
				return Result<string>.Fail("An error occurred while deleting category", 500);
			}
		}

		public async Task<Result<List<CategoryDto>>> FilterAsync(
	string? search,
	bool? isActive,
	bool ?includeDeleted,
	int page,
	int pageSize)
		{
			_logger.LogInformation($"Executing {nameof(FilterAsync)} with filters");
			var cacheKey = $"category_filter_{search}_{isActive}_{includeDeleted}_{page}_{pageSize}";
			var cached = await _cacheManager.GetAsync<List<CategoryDto>>(cacheKey);
			if (cached != null)
			{
				_logger.LogInformation($"Cache hit for FilterAsync with key: {cacheKey}");
				return Result<List<CategoryDto>>.Ok(cached, "Categories fetched from cache", 200);
			}


			var query = _unitOfWork.Category.GetAll();

			
			if (!string.IsNullOrWhiteSpace(search))
				query = query.Where(c => c.Name.Contains(search));

			if (isActive.HasValue)
				query = query.Where(c => c.IsActive == isActive.Value);

			if(includeDeleted.HasValue){
				if (!includeDeleted.Value)
					query = query.Where(c => c.DeletedAt == null);
				else
					query = query.Where(c => c.DeletedAt != null);
						}
			int totalCount = await query.CountAsync();

			var result = await query
				.OrderBy(c => c.DisplayOrder)
				.Skip((page - 1) * pageSize)
				.Take(pageSize)
				.Select(c => new CategoryDto
				{
					Id = c.Id,
					Name = c.Name,
					IsActive = c.IsActive,
					UpdatedAt=c.ModifiedAt,
					CreatedAt=c.CreatedAt,
					Images=c.Images.Select(i=>new ImageDto { Id=i.Id,IsMain=i.IsMain,Url=i.Url}).ToList(),
					Description=c.Description,
					DisplayOrder=c.DisplayOrder,
					DeletedAt=c.DeletedAt,
					
				})
				.ToListAsync();

			if (!result.Any())
				return Result<List<CategoryDto>>.Fail("No categories found", 404);
			BackgroundJob.Enqueue(() => _cacheManager.SetAsync(cacheKey, result, null, new[] { CACHE_TAG_CATEGORY }));
			return Result<List<CategoryDto>>.Ok(result, $"Filtered categories retrieved (Total: {totalCount})", 200);
		}


		public async Task<Result<CategoryDto>> ReturnRemovedCategoryAsync(int id, string userid)
		{
			_logger.LogInformation($"Executing {nameof(ReturnRemovedCategoryAsync)} for id: {id}");
			
			using var transaction = await _unitOfWork.BeginTransactionAsync();
			try
			{
				var category = await _unitOfWork.Category.GetCategoryById(id);
				if (category == null || category.DeletedAt == null)
				{
					await transaction.RollbackAsync();
					_logger.LogWarning($"Can't Found Category with this id:{id}");
					return Result<CategoryDto>.Fail($"Can't Found Category with this id:{id}", 404);
				}
				
				var restoreResult = await _unitOfWork.Category.RestoreAsync(id);
				if (!restoreResult)
				{
					await transaction.RollbackAsync();
					return Result<CategoryDto>.Fail("Try Again later", 500);
				}
				
				var adminLog = await _adminopreationservices.AddAdminOpreationAsync(
					$"Restored Category {id}",
					Opreations.UpdateOpreation,
					userid,
					id
				);
				
				if (!adminLog.Success)
				{
					_logger.LogWarning($"Failed to log admin operation: {adminLog.Message}");
				}
				
				await _unitOfWork.CommitAsync();
				await transaction.CommitAsync();
				await SaveAndRemoveCategoryCacheAsync();
				
				var categorydto = _mapping.Map<CategoryDto>(category);
				return Result<CategoryDto>.Ok(categorydto, "Category restored successfully", 200);
			}
			catch (Exception ex)
			{
				await transaction.RollbackAsync();
				_logger.LogError($"Exception in ReturnRemovedCategoryAsync: {ex.Message}");
				NotifyAdminOfError($"Exception in ReturnRemovedCategoryAsync: {ex.Message}", ex.StackTrace);
				return Result<CategoryDto>.Fail("An error occurred while restoring category", 500);
			}
		}



	
		public async Task<Result<string>> ActivateCategoryAsync(int categoryId, string userId)
		{
			_logger.LogInformation($"Activating category {categoryId}");
			var category = await _unitOfWork.Category.GetCategoryById(categoryId);
			if (category == null)
				return Result<string>.Fail($"Category with id {categoryId} not found", 404);
			if (category.Images == null || !category.Images.Any(i => i.DeletedAt == null))
				return Result<string>.Fail("Cannot activate category without at least one image", 400);
			if (category.SubCategories == null || !category.SubCategories.Any(sc => sc.IsActive && sc.DeletedAt == null))
				return Result<string>.Fail("Cannot activate category without at least one active subcategory", 400);
			if (category.IsActive)
				return Result<string>.Ok(null, "Category is already active", 200);
			category.IsActive = true;
			var updateResult = _unitOfWork.Category.Update(category);
			if (!updateResult)
				return Result<string>.Fail("Failed to activate category", 500);
			await _unitOfWork.CommitAsync();
			await SaveAndRemoveCategoryCacheAsync();
			return Result<string>.Ok(null, "Category activated successfully", 200);
		}
		public async Task<Result<string>> DeactivateCategoryAsync(int categoryId, string userId)
		{
			_logger.LogInformation($"Deactivating category {categoryId}");
			var category = await _unitOfWork.Category.GetCategoryById(categoryId);
			if (category == null)
				return Result<string>.Fail($"Category with id {categoryId} not found", 404);
			if (!category.IsActive)
				return Result<string>.Ok(null, "Category is already inactive", 200);
			category.IsActive = false;
			var updateResult = _unitOfWork.Category.Update(category);
			if (!updateResult)
				return Result<string>.Fail("Failed to deactivate category", 500);
			await _unitOfWork.CommitAsync();
			await SaveAndRemoveCategoryCacheAsync();
			return Result<string>.Ok(null, "Category deactivated successfully", 200);
		}
		public async Task<Result<bool>> ChangeActiveStatus(int categoryId, string userId)
		{
			_logger.LogInformation($"Executing {nameof(ChangeActiveStatus)} for categoryId: {categoryId}");
			
			using var transaction = await _unitOfWork.BeginTransactionAsync();
			try
			{
				var category = await _unitOfWork.Category.GetCategoryById(categoryId);
				if (category == null)
				{
					await transaction.RollbackAsync();
					return Result<bool>.Fail($"Category with id {categoryId} not found", 404);
				}

				category.IsActive = !category.IsActive;

				var adminOpResult = await _adminopreationservices.AddAdminOpreationAsync(
					category.IsActive ? "Activate Category" : "Deactivate Category",
					 Opreations.UpdateOpreation,
					userId,
					category.Id
				);
				
				if (!adminOpResult.Success)
				{
					_logger.LogWarning($"Failed to log admin operation: {adminOpResult.Message}");
				}
				
				await _unitOfWork.CommitAsync();
				await transaction.CommitAsync();
				await SaveAndRemoveCategoryCacheAsync();

				return Result<bool>.Ok(category.IsActive, $"Category with ID {categoryId} has been {(category.IsActive ? "activated" : "deactivated")}", 200);
			}
			catch (Exception ex)
			{
				await transaction.RollbackAsync();
				_logger.LogError($"Exception in ChangeActiveStatus: {ex.Message}");
				NotifyAdminOfError($"Exception in ChangeActiveStatus: {ex.Message}", ex.StackTrace);
				return Result<bool>.Fail("An error occurred while changing category status", 500);
			}
		}
		public async Task<Result<CategoryDto>> UpdateAsync(int categoryId, UpdateCategoryDto category, string userid)
		{
			_logger.LogInformation($"Executing {nameof(UpdateAsync)} for categoryId: {categoryId}");

			var existingCategory = await _unitOfWork.Category.GetCategoryById(categoryId);
			if (existingCategory == null)
			{
				return Result<CategoryDto>.Fail($"Category with id {categoryId} not found", 404);
			}
		
				using var transaction = await _unitOfWork.BeginTransactionAsync();

			try
			{
				List<string> warings = new List<string>();
				
				if (!string.IsNullOrWhiteSpace(category.Name))
					existingCategory.Name = category.Name;

				if (!string.IsNullOrWhiteSpace(category.Description))
					existingCategory.Description = category.Description;

				if (category.DisplayOrder.HasValue)
					existingCategory.DisplayOrder = category.DisplayOrder.Value;


				var adminOpResult = await _adminopreationservices.AddAdminOpreationAsync(
					"Update Category",
					Opreations.UpdateOpreation,
					userid,
					existingCategory.Id
				);

				if (!adminOpResult.Success)
				{
					_logger.LogWarning($"Failed to log admin operation: {adminOpResult.Message}");
				}

				await _unitOfWork.CommitAsync();
				await transaction.CommitAsync();

				await SaveAndRemoveCategoryCacheAsync();

				var dto = _mapping.Map<CategoryDto>(existingCategory);
				return Result<CategoryDto>.Ok(dto, "Updated", 200, warnings: warings);
			}
				
			catch (Exception ex)
			{
				await transaction.RollbackAsync();
				_logger.LogError($"Exception in UpdateAsync: {ex.Message}");
				return Result<CategoryDto>.Fail("An error occurred during update", 500);
			}
		}



		//public async Task<ApiResponse<List<SubCategoryDto>>> GetSubCategoriesAsync(int categoryId)
		//      {
		//          var subcategoriesQuery = _unitOfWork.Category.GetSubCategories(categoryId);
		//          var dtos = await subcategoriesQuery.Select(sc => new SubCategoryDto
		//          {
		//              Id = sc.Id,
		//              Name = sc.Name,
		//              Description = sc.Description,
		//          }).ToListAsync();
		//          if (!dtos.Any())
		//              return ApiResponse<List<SubCategoryDto>>.CreateErrorResponse(
		//                  new ErrorResponse("SubCategory", $"No subcategories found for category {categoryId}"),
		//                  404
		//              );
		//          return ApiResponse<List<SubCategoryDto>>.CreateSuccessResponse("Subcategories found", dtos);
		//      }


		public async Task<Result<ImageDto>> AddMainImageToCategoryAsync(int categoryId, IFormFile mainImage, string userId)
		{
			_logger.LogInformation($"Executing {nameof(AddMainImageToCategoryAsync)} for categoryId: {categoryId}");
			if (mainImage == null || mainImage.Length == 0)
			{
				return Result<ImageDto>.Fail("Main image is required.", 400);
			}
			
			using var transaction = await _unitOfWork.BeginTransactionAsync();
			try
			{
				var category = await _unitOfWork.Category.GetCategoryById(categoryId);
				if (category == null)
				{
					await transaction.RollbackAsync();
					return Result<ImageDto>.Fail($"Category with id {categoryId} not found", 404);
				}
				
				// Remove existing main image if exists
				var existingMainImage = category.Images.FirstOrDefault(i => i.IsMain && i.DeletedAt == null);
				if (existingMainImage != null)
				{
					_logger.LogInformation($"Removing existing main image with ID {existingMainImage.Id} from category {categoryId}");
					var deleteResult = await _imagesServices.DeleteImageAsync(existingMainImage);
					if (!deleteResult.Success)
					{
						_logger.LogError($"Failed to delete existing main image: {deleteResult.Message}");
						await transaction.RollbackAsync();
						return Result<ImageDto>.Fail(deleteResult.Message, deleteResult.StatusCode, deleteResult.Warnings);
					}
					category.Images.Remove(existingMainImage);
				}
				
				var mainImageResult = await _imagesServices.SaveMainCategoryImageAsync(mainImage, userId);
				if (!mainImageResult.Success || mainImageResult.Data == null)
				{
					await transaction.RollbackAsync();
					return Result<ImageDto>.Fail($"Failed to save main image: {mainImageResult.Message}", 500);
				}
				
				category.Images.Add(mainImageResult.Data);
				var updateResult = _unitOfWork.Category.Update(category);
				if (!updateResult)
				{
					await transaction.RollbackAsync();
					return Result<ImageDto>.Fail($"Failed to update category with main image", 500);
				}
				
				await _unitOfWork.CommitAsync();
				await transaction.CommitAsync();
				
				var mapped = _mapping.Map<ImageDto>(mainImageResult.Data);
				return Result<ImageDto>.Ok(mapped, "Main image added to category", 200);
			}
			catch (Exception ex)
			{
				await transaction.RollbackAsync();
				_logger.LogError($"Exception in AddMainImageToCategoryAsync: {ex.Message}");
				NotifyAdminOfError($"Exception in AddMainImageToCategoryAsync: {ex.Message}", ex.StackTrace);
				return Result<ImageDto>.Fail("An error occurred while adding main image", 500);
			}
		}

		public async Task<Result<CategoryDto>> RemoveImageFromCategoryAsync(int categoryId, int imageId, string userId)
		{
			_logger.LogInformation($"Removing image {imageId} from category: {categoryId}");
			using var transaction = await _unitOfWork.BeginTransactionAsync();
			try
			{
				var category = await _unitOfWork.Category.GetCategoryById(categoryId);
				if (category == null)
				{
					await transaction.RollbackAsync();
					return Result<CategoryDto>.Fail($"Category with id {categoryId} not found", 404);
				}
				var image = category.Images.FirstOrDefault(i => i.Id == imageId);
				if (image == null)
				{
					await transaction.RollbackAsync();
					return Result<CategoryDto>.Fail("Image not found", 404);
				}
				category.Images.Remove(image);
				// Optionally: delete file from disk
				var filePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", image.Url.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
				if (File.Exists(filePath))
				{
					File.Delete(filePath);
				}
				// Deactivate if no images left
				if (!category.Images.Any(i => i.DeletedAt == null))
				{
					if (category.IsActive)
					{
						category.IsActive = false;
						_unitOfWork.Category.Update(category);
						_logger.LogInformation($"Category {categoryId} deactivated because it has no images left.");
					}
				}
				var updateResult = _unitOfWork.Category.Update(category);
				if (!updateResult)
				{
					await transaction.RollbackAsync();
					return Result<CategoryDto>.Fail("Failed to remove image", 400);
				}
				// Log admin operation
				var adminLog = await _adminopreationservices.AddAdminOpreationAsync(
					$"Remove Image {imageId} from Category {categoryId}",
					Opreations.UpdateOpreation,
					userId,
					categoryId
				);
				if (!adminLog.Success)
				{
					_logger.LogWarning($"Failed to log admin operation: {adminLog.Message}");
					await transaction.RollbackAsync();
					return Result<CategoryDto>.Fail($"Failed to log admin operation: {adminLog.Message}", 500);
				}
				await _unitOfWork.CommitAsync();
				await transaction.CommitAsync();
				var categoryDto = _mapping.Map<CategoryDto>(category);
				var warnings = new List<string>();
				if (!category.Images.Any(i => i.DeletedAt == null))
				{
					warnings.Add("Category was deactivated because it has no images left.");
				}
				return Result<CategoryDto>.Ok(categoryDto, "Image removed successfully", 200, warnings: warnings);
			}
			catch (Exception ex)
			{
				await transaction.RollbackAsync();
				_logger.LogError(ex, $"Unexpected error in RemoveImageFromCategoryAsync for category {categoryId}");
				NotifyAdminOfError(ex.Message, ex.StackTrace);
				return Result<CategoryDto>.Fail("Unexpected error occurred while removing image", 500);
			}
		}

		public async Task<Result<bool>> RemoveImageAsync(int categoryId, int imageId, string userId)
		{
			_logger.LogInformation($"Executing {nameof(RemoveImageAsync)} for categoryId: {categoryId}, imageId: {imageId}, userId: {userId}");
			using var transaction = await _unitOfWork.BeginTransactionAsync();
			try
			{
				var category = await _unitOfWork.Category.GetAll()
					.Include(c => c.Images)
					.FirstOrDefaultAsync(c => c.Id == categoryId && c.DeletedAt == null);
				if (category == null)
				{
					await transaction.RollbackAsync();
					_logger.LogWarning($"Category with id: {categoryId} not found");
					return Result<bool>.Fail($"Category with id: {categoryId} not found", 404);
				}
				var image = category.Images.FirstOrDefault(i => i.Id == imageId);
				if (image == null)
				{
					await transaction.RollbackAsync();
					_logger.LogWarning($"Image with id: {imageId} not found in category {categoryId}");
					return Result<bool>.Fail($"Image with id: {imageId} not found in category", 404);
				}
				category.Images.Remove(image);
				var updateResult = _unitOfWork.Category.Update(category);
				if (!updateResult)
				{
					await transaction.RollbackAsync();
					return Result<bool>.Fail("Failed to remove image", 400);
				}
				// Log admin operation
				var adminLog = await _adminopreationservices.AddAdminOpreationAsync(
					$"Remove Image {imageId} from Category {categoryId}",
					Opreations.UpdateOpreation,
					userId,
					categoryId
				);
				if (!adminLog.Success)
				{
					_logger.LogWarning($"Failed to log admin operation: {adminLog.Message}");
					await transaction.RollbackAsync();
					return Result<bool>.Fail($"Failed to log admin operation: {adminLog.Message}", 500);
				}
				await _unitOfWork.CommitAsync();
				await transaction.CommitAsync();
				_logger.LogInformation($"Image with id: {imageId} removed from category {categoryId}");
				return Result<bool>.Ok(true,$"Image removed from category", 200);
			}
			catch (Exception ex)
			{
				await transaction.RollbackAsync();
				_logger.LogError(ex, $"Unexpected error in RemoveImageAsync for category {categoryId}");
				NotifyAdminOfError(ex.Message, ex.StackTrace);
				return Result<bool>.Fail("Unexpected error occurred while removing image", 500);
			}
		}

	}
}
