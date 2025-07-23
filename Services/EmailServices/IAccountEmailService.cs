using E_Commerce.Models;

namespace E_Commerce.Services.EmailServices
{
    public interface IAccountEmailService
    {
        Task SendValidationEmailAsync(Customer user);
        Task SendPasswordResetEmailAsync(Customer user);
        Task SendPasswordResetSuccessEmailAsync(string email);
        Task SendAccountLockedEmailAsync(Customer user, string reason = "Multiple failed login attempts");
        Task SendWelcomeEmailAsync(Customer user);
    }
} 