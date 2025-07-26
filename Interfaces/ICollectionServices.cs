using E_Commerce.DtoModels.CollectionDtos;
using E_Commerce.DtoModels.ImagesDtos;
using E_Commerce.DtoModels.Responses;
using E_Commerce.Services;

namespace E_Commerce.Interfaces
{
    public interface ICollectionServices
    {
        Task<Result<CollectionDto>> GetCollectionByIdAsync(int collectionId, bool? IsActive = null, bool? IsDeleted = null);
        public Task<Result<bool>> RemoveImageFromCollectionAsync(int categoryId, int imageId, string userId);
        public  Task<Result<ImageDto>> AddMainImageToCollectionAsync(int collectionid, IFormFile mainImage, string userId);
        public  Task<Result<List<ImageDto>>> AddImagesToCollectionAsync(int collectionid, List<IFormFile> images, string userId);
        public  Task CheckAndDeactivateEmptyCollectionsAsync(int productId);


		Task<Result<CollectionSummaryDto>> CreateCollectionAsync(CreateCollectionDto collectionDto, string userid);
        Task<Result<CollectionSummaryDto>> UpdateCollectionAsync(int collectionId, UpdateCollectionDto collectionDto, string userid);
        Task<Result<bool>> DeleteCollectionAsync(int collectionId, string userid);
        Task<Result<bool>> AddProductsToCollectionAsync(int collectionId, AddProductsToCollectionDto productsDto, string userid);
        Task<Result<bool>> RemoveProductsFromCollectionAsync(int collectionId, RemoveProductsFromCollectionDto productsDto, string userid);
		public  Task<Result<bool>> ActivateCollectionAsync(int collectionId, string userId);
        public Task<Result<bool>> DeactivateCollectionAsync(int collectionId, string userId);

		Task<Result<bool>> UpdateCollectionDisplayOrderAsync(int collectionId, int displayOrder, string userid);
        public Task<Result<List<CollectionSummaryDto>>> SearchCollectionsAsync(string? searchTerm, bool? IsActive = null, bool? IsDeleted = null, int page=1,int pagesize=10);


	}
} 