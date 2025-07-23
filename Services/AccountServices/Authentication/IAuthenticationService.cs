using E_Commerce.DtoModels.Responses;
using E_Commerce.DtoModels.TokenDtos;

namespace E_Commerce.Services.AccountServices.Authentication
{
    public interface IAuthenticationService
    {
        Task<Result<TokensDto>> LoginAsync(string email, string password);
        Task<Result<string>> LogoutAsync(string userid);
        Task<Result<string>> RefreshTokenAsync(string userid, string refreshtoken);
    }
} 