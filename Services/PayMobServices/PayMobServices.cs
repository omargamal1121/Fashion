using E_Commerce.Services.EmailServices;
using Hangfire;
using Microsoft.AspNetCore.Authentication.OAuth;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using static E_Commerce.Services.PayMobServices.PayMobServices;

namespace E_Commerce.Services.PayMobServices
{
	public interface IPayMobServices
	{
		public Task<string?> GetTokenAsync();
		public  Task<int> CreateOrderInPaymobAsync(CreateOrderRequest order);
		public  Task<string?> GeneratePaymentKeyAsync(PaymentKeyContent content);
	}
	public class PayMobServices: IPayMobServices
	{
		private readonly IBackgroundJobClient _backgroundJobClient;
		private readonly ILogger<PayMobServices> _logger;
		private readonly IErrorNotificationService _errorNotificationService;
		public PayMobServices(ILogger<PayMobServices> logger ,IErrorNotificationService errorNotificationService, IBackgroundJobClient backgroundJobClient)
		{
			_logger = logger;
			_errorNotificationService = errorNotificationService;
			_backgroundJobClient = backgroundJobClient;

		}
		private const string API_KEY = "ZXlKaGJHY2lPaUpJVXpVeE1pSXNJblI1Y0NJNklrcFhWQ0o5LmV5SmpiR0Z6Y3lJNklrMWxjbU5vWVc1MElpd2ljSEp2Wm1sc1pWOXdheUk2TVRBMk5EZ3pPU3dpYm1GdFpTSTZJbWx1YVhScFlXd2lmUS5RRVVrRWpjZXVUMDloeUhsQlNQY0JNUUdTZncwQzVndFhvR3BOZTVOdGRZX3k2VjJ2Smd6RER1WG0wN2Fzckh4NUlPTWlBQnB3bDNPRWJXREdWMWVTdw==";
		public async Task<string?> GetTokenAsync()
		{
			var body = new { api_key = API_KEY };
			_logger.LogInformation($"Execute {GetTokenAsync}");

			var json = JsonSerializer.Serialize(body);
			var content = new StringContent(json, Encoding.UTF8, "application/json");

			using HttpClient client = new HttpClient();
			var response = await client.PostAsync("https://accept.paymob.com/api/auth/tokens", content);

			if (!response.IsSuccessStatusCode)
			{
				string error = await response.Content.ReadAsStringAsync();
				_backgroundJobClient.Enqueue(() =>
					_errorNotificationService.SendErrorNotificationAsync("PayMob - Failed to retrieve auth token. Check API Key or network connectivity.",error)
				);
				_logger.LogError("PayMob - Failed to retrieve auth token. Check API Key or network connectivity.");
				return null;
			}

			var responseContent = await response.Content.ReadAsStringAsync();

			var result = JsonSerializer.Deserialize<TokenResponse>(
				responseContent,
				new JsonSerializerOptions { PropertyNameCaseInsensitive = true }
			);
			_logger.LogInformation("Return token");

			return result?.token;
		}
		public async Task<int> CreateOrderInPaymobAsync(CreateOrderRequest order)
		{
			var json= JsonSerializer.Serialize(order);
			var content=new StringContent(json,Encoding.UTF8,"application/json");
			using HttpClient client = new HttpClient();
			var response= await client.PostAsync($"https://accept.paymob.com/api/ecommerce/orders", content);
			if(!response.IsSuccessStatusCode)
			{
				string error = await response.Content.ReadAsStringAsync();
				_backgroundJobClient.Enqueue(() => _errorNotificationService.SendErrorNotificationAsync("Error While creating order in paymob", error));
				return 0;
			}
			var responsejson= await response.Content.ReadAsStringAsync();
			if(responsejson == null)
			{
				return 0;
			}
			var responsecontent = JsonSerializer.Deserialize<CreateOrderResponse>(responsejson);
			if (responsecontent == null)
				return 0;

			return responsecontent.id;

		}

		public async Task<string?> GeneratePaymentKeyAsync(PaymentKeyContent content)
		{
			try
			{
				_logger.LogInformation("Starting GeneratePaymentKeyAsync for Order ID: {OrderId}, Amount: {AmountCents}", content.order_id, content.amount_cents);

				var json = JsonSerializer.Serialize(content);
				var requestBody = new StringContent(json, Encoding.UTF8, "application/json");

				using HttpClient client = new HttpClient();
				var response = await client.PostAsync("https://accept.paymob.com/api/acceptance/payment_keys", requestBody);

				_logger.LogInformation("Request sent to Paymob payment_keys endpoint");

				if (!response.IsSuccessStatusCode)
				{
					string error = await response.Content.ReadAsStringAsync();
					_logger.LogError("Failed to get payment key from Paymob. Status: {StatusCode}, Response: {Error}", response.StatusCode, error);

					_backgroundJobClient.Enqueue(() =>
						_errorNotificationService.SendErrorNotificationAsync("Error While getting payment key", error)
					);

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

				_backgroundJobClient.Enqueue(() =>
					_errorNotificationService.SendErrorNotificationAsync("Exception while getting payment key", ex.Message)
				);

				return null;
			}
		}

		public class CreateOrderRequest
		{
			public string auth_token { get; set; }
			public bool delivery_needed { get; set; }
			public decimal amount_cents { get; set; }
			public string currency { get; set; }


		}


		public class PaymentKeyContent
		{
			public string currency { get; set; } = "EGP";
			public string auth_token { get; set; }

			public decimal amount_cents { get; set; }
			public int expiration { get; set; } = 1000;
			public int order_id { get; set; }
			public int integration_id { get; set; }
			public billing_data billing_data { get; set; }
		
		}
		public class billing_data 
		{
			public string apartment { get; set; } = "NA";
			public string phone_number { get; set; } = "NA";
			public string email { get; set; }
			public string floor { get; set; } = "NA";
			public string first_name { get; set; }
			public string street { get; set; } = "NA";
			public string building { get; set; } = "NA";
			public string shipping_method { get; set; }
			public string postal_code { get; set; } = "NA";
			public string city { get; set; } = "NA";
			public string country { get; set; } = "NA";
			public string last_name { get; set; }
			public string state { get; set; } = "NA";
		}
		public class CreateOrderResponse 
		{
			public int id { get; set; }
			public DateTime created_at { get; set; }
			public decimal amount_cents { get; set; }
			public string currency { get; set; }
		} 

			
		public class TokenResponse {
		
			public string token { get; set; } = string.Empty;
		}
	}
}

