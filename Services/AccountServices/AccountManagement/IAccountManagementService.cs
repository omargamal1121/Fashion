using E_Commerce.DtoModels.Responses;

namespace E_Commerce.Services.AccountServices.AccountManagement
{
    public interface IAccountManagementService
    {
        Task<Result<string>> DeleteAsync(string id);
    }
} 