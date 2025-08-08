﻿using E_Commerce.DtoModels.OrderDtos;
using E_Commerce.DtoModels.PaymentDtos;
using E_Commerce.Enums;
using E_Commerce.Models;
using E_Commerce.Services.EmailServices;
using E_Commerce.Services.PaymentProccessor;
using E_Commerce.UOW;
using Hangfire;
using Microsoft.AspNetCore.Authentication.OAuth;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using static E_Commerce.Services.PayMobServices.PayMobServices;

namespace E_Commerce.Services.PayMobServices
{
	public interface IPayMobServices
	{
		public Task<string?> GetTokenAsync();
		public Task<int> CreateOrderInPaymobAsync(CreateOrderRequest order);
		public Task<string?> GeneratePaymentKeyAsync(PaymentKeyContent content, PaymentMethodEnums paymentMethodEnums);
	}

	public class PayMobServices : IPaymentProcessor
	{
		private readonly IBackgroundJobClient _backgroundJobClient;
		private readonly ILogger<PayMobServices> _logger;
		private readonly IErrorNotificationService _errorNotificationService;
		public readonly IUnitOfWork _unitOfWork;
		public readonly UserManager<Customer> _usermanger;
		private string Token;
		private DateTime token_gentrate_at;

		public PayMobServices(UserManager<Customer> userManager, IUnitOfWork unitOfWork, ILogger<PayMobServices> logger, IErrorNotificationService errorNotificationService, IBackgroundJobClient backgroundJobClient)
		{
			_usermanger = userManager;
			_unitOfWork = unitOfWork;
			_logger = logger;
			_errorNotificationService = errorNotificationService;
			_backgroundJobClient = backgroundJobClient;
		}


		private async Task<bool> GetTokenAsync()
		{
			try
			{
				var apikey = await _unitOfWork.Repository<PaymentProvider>()
					.GetAll()
					.Where(p => p.Provider == PaymentProviderEnums.Paymob)
					.Select(p => p.PublicKey).FirstOrDefaultAsync();

				if (string.IsNullOrEmpty(apikey))
				{
					_logger.LogError("PayMob API key not found in database");
					_backgroundJobClient.Enqueue(() =>
						_errorNotificationService.SendErrorNotificationAsync("PayMob Configuration Error", "PayMob API key not found in database")
					);
					return false;
				}

				var body = new { api_key = apikey };
				_logger.LogInformation("Executing GetTokenAsync");

				var json = JsonSerializer.Serialize(body);
				var content = new StringContent(json, Encoding.UTF8, "application/json");

				using HttpClient client = new HttpClient();
				var response = await client.PostAsync("https://accept.paymob.com/api/auth/tokens", content);

				if (!response.IsSuccessStatusCode)
				{
					string error = await response.Content.ReadAsStringAsync();
					_backgroundJobClient.Enqueue(() =>
						_errorNotificationService.SendErrorNotificationAsync("PayMob - Failed to retrieve auth token", $"Status: {response.StatusCode}, Response: {error}")
					);
					_logger.LogError("PayMob - Failed to retrieve auth token. Status: {StatusCode}, Response: {Error}", response.StatusCode, error);
					return false;
				}

				var responseContent = await response.Content.ReadAsStringAsync();
				var result = JsonSerializer.Deserialize<TokenResponse>(
					responseContent,
					new JsonSerializerOptions { PropertyNameCaseInsensitive = true }
				);

				if (result == null || string.IsNullOrWhiteSpace(result.token))
				{
					_logger.LogError("Invalid token response from PayMob");
					_backgroundJobClient.Enqueue(() =>
						_errorNotificationService.SendErrorNotificationAsync("PayMob - Invalid token response", "Received null or empty token from PayMob")
					);
					return false;
				}

				_logger.LogInformation("Successfully retrieved PayMob token");
				Token = result.token;
				token_gentrate_at = DateTime.UtcNow;
				return true;
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Exception occurred while retrieving PayMob token");
				_backgroundJobClient.Enqueue(() =>
					_errorNotificationService.SendErrorNotificationAsync("PayMob - Token retrieval exception", ex.Message)
				);
				return false;
			}
		}

		public async Task<int> CreateOrderInPaymobAsync(CreateOrderRequest order)
		{
			// Ensure we have a valid token
			if (string.IsNullOrEmpty(Token) || token_gentrate_at.AddHours(1) < DateTime.UtcNow)
			{
				var tokenResult = await GetTokenAsync();
				if (!tokenResult)
				{
					_logger.LogError("Failed to get token for PayMob order creation");
					return 0;
				}
			}

			if (order == null)
			{
				_logger.LogError("CreateOrderInPaymobAsync received null order");
				return 0;
			}

			try
			{
				var json = JsonSerializer.Serialize(order);
				var content = new StringContent(json, Encoding.UTF8, "application/json");

				using HttpClient client = new HttpClient();
				var response = await client.PostAsync("https://accept.paymob.com/api/ecommerce/orders", content);

				if (response.StatusCode == HttpStatusCode.Unauthorized)
				{
					await GetTokenAsync();
					response = await client.PostAsync("https://accept.paymob.com/api/ecommerce/orders", content);
				}

				if (!response.IsSuccessStatusCode)
				{
					string error = await response.Content.ReadAsStringAsync();
					_backgroundJobClient.Enqueue(() => _errorNotificationService.SendErrorNotificationAsync("Error While creating order in paymob", error));
					_logger.LogError("Failed to create order in PayMob. Status: {StatusCode}, Error: {Error}", response.StatusCode, error);
					return 0;
				}

				var responseJson = await response.Content.ReadAsStringAsync();
				if (string.IsNullOrEmpty(responseJson))
				{
					_logger.LogError("Empty response from PayMob order creation");
					return 0;
				}

				var responseContent = JsonSerializer.Deserialize<CreateOrderResponse>(responseJson);
				if (responseContent == null)
				{
					_logger.LogError("Failed to deserialize PayMob order response");
					return 0;
				}

				return responseContent.id;
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Exception occurred while creating order in PayMob");
				return 0;
			}
		}

		public async Task<string?> GeneratePaymentKeyAsync(PaymentKeyContent content, PaymentMethodEnums paymentMethodEnums)
		{
			if (content == null)
			{
				_logger.LogError("GeneratePaymentKeyAsync received null content");
				return null;
			}

			// Ensure we have a valid token
			if (string.IsNullOrEmpty(Token) || token_gentrate_at.AddHours(1) < DateTime.UtcNow)
			{
				var tokenResult = await GetTokenAsync();
				if (!tokenResult)
				{
					_logger.LogError("Failed to get token for payment key generation");
					return null;
				}
			}

			try
			{
				_logger.LogInformation("Starting GeneratePaymentKeyAsync for Order ID: {OrderId}, Amount: {AmountCents}", content.order_id, content.amount_cents);

			
				var json = JsonSerializer.Serialize(content);
				var requestBody = new StringContent(json, Encoding.UTF8, "application/json");

				using HttpClient client = new HttpClient();
				var response = await client.PostAsync("https://accept.paymob.com/api/acceptance/payment_keys", requestBody);

				_logger.LogInformation("Request sent to Paymob payment_keys endpoint");

				if (response.StatusCode == HttpStatusCode.Unauthorized)
				{
					await GetTokenAsync();
					response = await client.PostAsync("https://accept.paymob.com/api/acceptance/payment_keys", requestBody);
				}

				if (!response.IsSuccessStatusCode)
				{
					string error = await response.Content.ReadAsStringAsync();
					_logger.LogError("Failed to get payment key from Paymob. Status: {StatusCode}, Response: {Error}", response.StatusCode, error);
					_backgroundJobClient.Enqueue(() => _errorNotificationService.SendErrorNotificationAsync("Error While getting payment key", error));
					return null;
				}

				var responseContent = await response.Content.ReadAsStringAsync();
				_logger.LogInformation("Received response from Paymob payment_keys endpoint");

				var result = JsonSerializer.Deserialize<TokenResponse>(responseContent,
					new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

				if (string.IsNullOrWhiteSpace(result?.token))
				{
					_logger.LogWarning("Payment key is null or empty in response");
					return null;
				}

				_logger.LogInformation("Payment key generated successfully for Order ID: {OrderId}", content.order_id);
				return result.token;
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Exception occurred while generating payment key for Order ID: {OrderId}", content.order_id);
				_backgroundJobClient.Enqueue(() => _errorNotificationService.SendErrorNotificationAsync("Exception while getting payment key", ex.Message));
				return null;
			}
		}

		public async Task<Result<string>> GetPaymentLinkAsync(CreatePayment dto)
		{
			if (dto == null)
			{
				return Result<string>.Fail("Invalid payment request", 400);
			}

			try
			{
				// Ensure we have a valid token
				if (string.IsNullOrEmpty(Token) || token_gentrate_at.AddHours(1) < DateTime.UtcNow)
				{
					var tokenResult = await GetTokenAsync();
					if (!tokenResult)
					{
						_logger.LogError("Failed to get token for payment link generation");
						return Result<string>.Fail("Authentication failed", 401);
					}
				}

				var user = await _usermanger.FindByIdAsync(dto.CustomerId);
				if (user == null)
				{
					_logger.LogWarning("User not found for payment: {CustomerId}", dto.CustomerId);
					return Result<string>.Fail("User not found", 404);
				}

				var address = await _unitOfWork.CustomerAddress.GetAddressByIdAsync(dto.AddressId);
				if (address == null)
				{
					_logger.LogWarning("Address not found for payment: {AddressId}", dto.AddressId);
					return Result<string>.Fail("Address not found", 404);
				}

                                var paymobOrderRequest = new CreateOrderRequest
                {
                    auth_token = Token,
                    amount_cents = (int)(dto.Amount * 100),
                    currency = "EGP",
                    delivery_needed = true,
                    merchant_order_id = dto.OrderId.ToString()
                };

				var paymobOrderId = await CreateOrderInPaymobAsync(paymobOrderRequest);
				if (paymobOrderId == 0)
				{
					_logger.LogError("Failed to create order in PayMob for OrderId: {OrderId}", dto.OrderId);
					return Result<string>.Fail("Failed to create payment order", 500);
				}

				_logger.LogInformation("Successfully created PayMob order: {PayMobOrderId} for local order: {OrderId}", paymobOrderId, dto.OrderId);

                var amountInCents = (int)(dto.Amount * 100);
				var integrationId = await _unitOfWork.Repository<PaymentMethod>()
					.GetAll()
					.Where(p => p.Method == dto.PaymentMethod && p.IsActive && p.PaymentProviders.Provider == PaymentProviderEnums.Paymob)
					.Select(p => p.IntegrationId)
					.FirstOrDefaultAsync();

				if (string.IsNullOrEmpty(integrationId))
				{
					_logger.LogError("Integration ID not found for payment method: {PaymentMethod}. Please configure the integration ID in the database.", dto.PaymentMethod);
					return Result<string>.Fail("Payment method not configured", 400);
				}

				_logger.LogInformation("Using integration ID: {IntegrationId} for payment method: {PaymentMethod}", integrationId, dto.PaymentMethod);

				var paymentKeyRequest = new PaymentKeyContent
				{
					amount_cents = amountInCents,
					auth_token = Token,
					order_id = paymobOrderId,
					integration_id = integrationId,
					billing_data = new billing_data
					{
						city = address.City ?? "NA",
						country = address.Country ?? "EG",
						state = address.State ?? "NA",
						postal_code = address.PostalCode ?? "NA",
						street = address.StreetAddress ?? "NA",
						first_name = user.Name?.Split(" ").FirstOrDefault() ?? "NA",
						last_name = user.Name?.Split(" ").Skip(1).FirstOrDefault() ?? "NA",
						email = user.Email ?? "noemail@email.com",
						phone_number = user.PhoneNumber ?? "0000000000"
					}
				};

				var paymentKey = await GeneratePaymentKeyAsync(paymentKeyRequest, dto.PaymentMethod);
				if (string.IsNullOrEmpty(paymentKey))
				{
					_logger.LogError("Failed to generate payment key from PayMob for order: {OrderId}", dto.OrderId);
					return Result<string>.Fail("Failed to generate payment key", 500);
				}

				_logger.LogInformation("Successfully generated payment key for order: {OrderId}", dto.OrderId);

				_logger.LogInformation("Payment Key generated successfully for Paymob order: {PaymobOrderId}", paymobOrderId);
                // Get iframe id from PaymentProvider configuration
                var iframeId = await _unitOfWork.Repository<PaymentProvider>()
                    .GetAll()
                    .Where(p => p.Provider == PaymentProviderEnums.Paymob)
                    .Select(p => p.IframeId)
                    .FirstOrDefaultAsync();
                if (string.IsNullOrWhiteSpace(iframeId))
                {
                    _logger.LogWarning("Paymob IframeId not configured. Falling back to default.");
                    iframeId = "0";
                }
                var paymentUrl =iframeId+$"?payment_token={paymentKey}";

				return Result<string>.Ok(paymentUrl);
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Exception occurred while generating payment link");
				return Result<string>.Fail("Failed to initiate payment", 500);
			}
		}

		public class WebhookEvent
		{
			public int Id { get; set; }
			public string EventType { get; set; } = string.Empty;
			public string PaymobTransactionId { get; set; } = string.Empty;
			public string RawContent { get; set; } = string.Empty;
			public bool IsProcessed { get; set; }
			public DateTime? ProcessedAt { get; set; }
			public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
			public int RetryCount { get; set; } = 0;
		}

        public class CreateOrderRequest
		{
			public bool delivery_needed { get; set; }
			public decimal amount_cents { get; set; }
			public string currency { get; set; } = "EGP";
			public string auth_token { get; set; } = string.Empty;
            public string? merchant_order_id { get; set; }
		}

		public class PaymentKeyContent
		{
			public string currency { get; set; } = "EGP";
			public string auth_token { get; set; } = string.Empty;
			public decimal amount_cents { get; set; }
			public int expiration { get; set; } = 1000;
			public int order_id { get; set; }
			public string integration_id { get; set; } = string.Empty;
			public billing_data billing_data { get; set; } = new billing_data();
		}

		public class billing_data
		{
			public string apartment { get; set; } = "NA";
			public string phone_number { get; set; } = "NA";
			public string email { get; set; } = string.Empty;
			public string floor { get; set; } = "NA";
			public string first_name { get; set; } = string.Empty;
			public string street { get; set; } = "NA";
			public string building { get; set; } = "NA";
			public string shipping_method { get; set; } = "NA";
			public string postal_code { get; set; } = "NA";
			public string city { get; set; } = "NA";
			public string country { get; set; } = "EG";
			public string last_name { get; set; } = string.Empty;
			public string state { get; set; } = "NA";
		}

		public class CreateOrderResponse
		{
			public int id { get; set; }
			public DateTime created_at { get; set; }
			public decimal amount_cents { get; set; }
			public string currency { get; set; } = "EGP";
		}

		public class TokenResponse
		{
			public string token { get; set; } = string.Empty;
		}
	}
}

