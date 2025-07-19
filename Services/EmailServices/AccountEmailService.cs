using E_Commers.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.WebUtilities;
using System.Text;

namespace E_Commers.Services.EmailServices
{
    public class AccountEmailService : IAccountEmailService
    {
        private readonly IEmailSender _emailSender;
        private readonly UserManager<Customer> _userManager;
        private readonly ILogger<AccountEmailService> _logger;

        public AccountEmailService(
            IEmailSender emailSender,
            UserManager<Customer> userManager,
            ILogger<AccountEmailService> logger)
        {
            _emailSender = emailSender;
            _userManager = userManager;
            _logger = logger;
        }

        public async Task SendValidationEmailAsync(Customer user)
        {
            try
            {
                var token = await _userManager.GenerateEmailConfirmationTokenAsync(user);
                var encodedToken = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(token));
                
                string subject = "Email Confirmation - Welcome to Our Service";
                string message = CreateEmailTemplate(
                    "Email Confirmation",
                    $@"
                        <h1 style='color: #2c3e50; margin-bottom: 20px;'>Welcome to Our Service!</h1>
                        <p style='font-size: 16px; line-height: 1.6; color: #34495e;'>
                            Thank you for registering with us. To complete your registration, please confirm your email address.
                        </p>
                        <div style='background: linear-gradient(135deg, #667eea 0%, #764ba2 100%); padding: 20px; border-radius: 10px; margin: 25px 0; text-align: center;'>
                            <h3 style='color: white; margin: 0 0 15px 0;'>Your Confirmation Token</h3>
                            <div style='background: rgba(255,255,255,0.9); padding: 15px; border-radius: 8px; margin: 10px 0;'>
                                <p style='font-size: 18px; font-weight: bold; color: #2c3e50; margin: 0; font-family: monospace;'>{encodedToken}</p>
                            </div>
                            <p style='color: white; margin: 10px 0 0 0; font-size: 14px;'>Please enter this token in the confirmation page</p>
                        </div>
                        <div style='background-color: #f8f9fa; padding: 15px; border-radius: 8px; margin: 20px 0; border-left: 4px solid #17a2b8;'>
                            <p style='margin: 0; color: #495057;'>
                                <strong>Important:</strong> This token will expire in 24 hours for security reasons.
                            </p>
                        </div>
                        <p style='font-size: 14px; color: #6c757d;'>
                            If you did not create an account with us, please ignore this email.
                        </p>"
                );
                
                await _emailSender.SendEmailAsync(user.Email, subject, message);
                _logger.LogInformation($"Validation email sent successfully to {user.Email}");
            }
            catch (Exception ex)
            {
                _logger.LogError($"Failed to send validation email to {user.Email}: {ex.Message}");
                throw;
            }
        }

        public async Task SendPasswordResetEmailAsync(Customer user)
        {
            try
            {
                var token = await _userManager.GeneratePasswordResetTokenAsync(user);
                var encodedToken = System.Net.WebUtility.UrlEncode(token);

                string subject = "Password Reset Request - Secure Your Account";
                string message = CreateEmailTemplate(
                    "Password Reset Request",
                    $@"
                        <h1 style='color: #dc3545; margin-bottom: 20px;'>Password Reset Request</h1>
                        <p style='font-size: 16px; line-height: 1.6; color: #34495e;'>
                            Hello <strong>{user.UserName}</strong>,<br>
                            We received a request to reset your password. If this was you, please use the token below to reset your password.
                        </p>
                        <div style='background: linear-gradient(135deg, #ff6b6b 0%, #ee5a24 100%); padding: 20px; border-radius: 10px; margin: 25px 0; text-align: center;'>
                            <h3 style='color: white; margin: 0 0 15px 0;'>Your Reset Token</h3>
                            <div style='background: rgba(255,255,255,0.9); padding: 15px; border-radius: 8px; margin: 10px 0;'>
                                <p style='font-size: 16px; font-weight: bold; color: #2c3e50; margin: 0; font-family: monospace; word-break: break-all;'>{encodedToken}</p>
                            </div>
                            <p style='color: white; margin: 10px 0 0 0; font-size: 14px;'>Copy this token and use it in the reset password form</p>
                        </div>
                        <div style='background-color: #fff3cd; padding: 15px; border-radius: 8px; margin: 20px 0; border-left: 4px solid #ffc107;'>
                            <p style='margin: 0; color: #856404;'>
                                <strong>Security Notice:</strong> This token is valid for 1 hour only. If you didn't request this reset, please ignore this email.
                            </p>
                        </div>
                        <p style='font-size: 14px; color: #6c757d;'>
                            For your security, if you didn't request this password reset, please contact our support team immediately.
                        </p>"
                );

                await _emailSender.SendEmailAsync(user.Email, subject, message);
                _logger.LogInformation($"Password reset email sent successfully to {user.Email}");
            }
            catch (Exception ex)
            {
                _logger.LogError($"Failed to send password reset email to {user.Email}: {ex.Message}");
                throw;
            }
        }

        public async Task SendPasswordResetSuccessEmailAsync(string email)
        {
            try
            {
                string subject = "Password Reset Successful - Account Secured";

                string message = CreateEmailTemplate(
                    "Password Reset Successful",
                    $@"
                        <h1 style='color: #28a745; margin-bottom: 20px;'>Password Reset Successful</h1>
                        <div style='background-color: #d4edda; padding: 20px; border-radius: 10px; margin: 20px 0; border-left: 4px solid #28a745;'>
                            <h3 style='color: #155724; margin: 0 0 10px 0;'>✅ Your password has been successfully changed!</h3>
                            <p style='color: #155724; margin: 0; font-size: 16px;'>
                                Your account is now secured with your new password.
                            </p>
                        </div>
                        <p style='font-size: 16px; line-height: 1.6; color: #34495e;'>
                            If you made this change, no further action is needed. You can now log in with your new password.
                        </p>
                        <div style='background-color: #f8d7da; padding: 15px; border-radius: 8px; margin: 20px 0; border-left: 4px solid #dc3545;'>
                            <p style='margin: 0; color: #721c24; font-weight: bold;'>
                                ⚠️ If you did <strong>NOT</strong> perform this action, please:
                            </p>
                            <ul style='margin: 10px 0 0 20px; color: #721c24;'>
                                <li>Reset your password immediately</li>
                                <li>Contact our support team</li>
                                <li>Check your account for any suspicious activity</li>
                            </ul>
                        </div>
                        <p style='font-size: 14px; color: #6c757d;'>
                            Thank you for using our service. We're committed to keeping your account secure.
                        </p>"
                );

                await _emailSender.SendEmailAsync(email, subject, message);
                _logger.LogInformation($"Password reset success email sent successfully to {email}");
            }
            catch (Exception ex)
            {
                _logger.LogError($"Failed to send password reset success email to {email}: {ex.Message}");
                throw;
            }
        }

        public async Task SendAccountLockedEmailAsync(Customer user, string reason = "Multiple failed login attempts")
        {
            try
            {
                string subject = "Account Locked - Security Alert";

                string message = CreateEmailTemplate(
                    "Account Locked",
                    $@"
                        <h1 style='color: #dc3545; margin-bottom: 20px;'>Account Locked</h1>
                        <div style='background-color: #f8d7da; padding: 20px; border-radius: 10px; margin: 20px 0; border-left: 4px solid #dc3545;'>
                            <h3 style='color: #721c24; margin: 0 0 10px 0;'>🔒 Your account has been temporarily locked</h3>
                            <p style='color: #721c24; margin: 0; font-size: 16px;'>
                                Reason: {reason}
                            </p>
                        </div>
                        <p style='font-size: 16px; line-height: 1.6; color: #34495e;'>
                            Hello <strong>{user.UserName}</strong>,<br>
                            For your security, your account has been locked due to suspicious activity.
                        </p>
                        <div style='background-color: #e2e3e5; padding: 15px; border-radius: 8px; margin: 20px 0;'>
                            <h4 style='color: #495057; margin: 0 0 10px 0;'>What you need to do:</h4>
                            <ol style='color: #495057; margin: 0; padding-left: 20px;'>
                                <li>Wait for the lockout period to expire (usually 15 minutes)</li>
                                <li>Use the password reset feature if needed</li>
                                <li>Contact support if you continue to have issues</li>
                            </ol>
                        </div>
                        <p style='font-size: 14px; color: #6c757d;'>
                            If this was not you, please contact our support team immediately to secure your account.
                        </p>"
                );

                await _emailSender.SendEmailAsync(user.Email, subject, message);
                _logger.LogInformation($"Account locked email sent successfully to {user.Email}");
            }
            catch (Exception ex)
            {
                _logger.LogError($"Failed to send account locked email to {user.Email}: {ex.Message}");
                throw;
            }
        }

        public async Task SendWelcomeEmailAsync(Customer user)
        {
            try
            {
                string subject = "Welcome to Our Service - Get Started!";

                string message = CreateEmailTemplate(
                    "Welcome",
                    $@"
                        <h1 style='color: #28a745; margin-bottom: 20px;'>Welcome to Our Service!</h1>
                        <div style='background: linear-gradient(135deg, #28a745 0%, #20c997 100%); padding: 20px; border-radius: 10px; margin: 20px 0; text-align: center;'>
                            <h3 style='color: white; margin: 0 0 15px 0;'>🎉 Your account is ready!</h3>
                            <p style='color: white; margin: 0; font-size: 16px;'>
                                Hello <strong>{user.UserName}</strong>, welcome to our community!
                            </p>
                        </div>
                        <p style='font-size: 16px; line-height: 1.6; color: #34495e;'>
                            Thank you for joining us! Your account has been successfully created and is ready to use.
                        </p>
                        <div style='background-color: #d1ecf1; padding: 15px; border-radius: 8px; margin: 20px 0; border-left: 4px solid #17a2b8;'>
                            <h4 style='color: #0c5460; margin: 0 0 10px 0;'>Next steps:</h4>
                            <ul style='color: #0c5460; margin: 0; padding-left: 20px;'>
                                <li>Complete your profile</li>
                                <li>Explore our features</li>
                                <li>Check out our getting started guide</li>
                            </ul>
                        </div>
                        <p style='font-size: 14px; color: #6c757d;'>
                            If you have any questions, our support team is here to help!
                        </p>"
                );

                await _emailSender.SendEmailAsync(user.Email, subject, message);
                _logger.LogInformation($"Welcome email sent successfully to {user.Email}");
            }
            catch (Exception ex)
            {
                _logger.LogError($"Failed to send welcome email to {user.Email}: {ex.Message}");
                throw;
            }
        }

        private string CreateEmailTemplate(string title, string content)
        {
            return $@"
                <!DOCTYPE html>
                <html lang='en'>
                <head>
                    <meta charset='UTF-8'>
                    <meta name='viewport' content='width=device-width, initial-scale=1.0'>
                    <title>{title}</title>
                </head>
                <body style='margin: 0; padding: 0; font-family: Arial, sans-serif; background-color: #f8f9fa;'>
                    <div style='max-width: 600px; margin: 0 auto; background-color: white; padding: 40px 20px;'>
                        <div style='text-align: center; margin-bottom: 30px;'>
                            <h2 style='color: #2c3e50; margin: 0; font-size: 24px;'>{title}</h2>
                        </div>
                        <div style='line-height: 1.6;'>
                            {content}
                        </div>
                        <hr style='border: none; border-top: 1px solid #e9ecef; margin: 30px 0;'>
                        <div style='text-align: center; color: #6c757d; font-size: 12px;'>
                            <p style='margin: 0;'>This is an automated message from our service.</p>
                            <p style='margin: 5px 0 0 0;'>Please do not reply to this email.</p>
                        </div>
                    </div>
                </body>
                </html>";
        }
    }
} 