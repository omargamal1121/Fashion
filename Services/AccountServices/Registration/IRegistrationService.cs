using E_Commerce.DtoModels.AccountDtos;
using E_Commerce.DtoModels.Responses;

namespace E_Commerce.Services.AccountServices.Registration
{
    public interface IRegistrationService
    {
        Task<Result<RegisterResponse>> RegisterAsync(RegisterDto model);
        Task<Result<string>> ConfirmEmailAsync(string userId, string token);
        Task<Result<string>> ResendConfirmationEmailAsync(string email);
    }
} 