using E_Commerce.Services;

namespace E_Commerce.Interfaces
{
	public interface IRefreshTokenService 
	{
	
		public Task<Result<bool>> ValidateRefreshTokenAsync(string userid, string refreshtoken);
		public Task<Result<bool>> RemoveRefreshTokenAsync(string userid);
		public Task<Result<string>> GenerateRefreshTokenAsync(string userid);
		public Task<Result<string>> RefreshTokenAsync(string userid, string refreshtoken);

	}
}
