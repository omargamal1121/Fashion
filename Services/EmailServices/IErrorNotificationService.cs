namespace E_Commerce.Services.EmailServices
{
	public interface IErrorNotificationService
    {
        Task SendErrorNotificationAsync(string errorMessage, string? stackTrace = null);
	}
} 