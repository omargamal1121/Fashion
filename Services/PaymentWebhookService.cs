using E_Commerce.DtoModels.PaymentDtos;
using E_Commerce.Enums;
using E_Commerce.Models;
using E_Commerce.UOW;
using E_Commerce.Context;
using Microsoft.EntityFrameworkCore;

namespace E_Commerce.Services
{
    public interface IPaymentWebhookService
    {
        Task HandlePaymobAsync(PaymobWebhookDto dto);
    }

    public class PaymentWebhookService : IPaymentWebhookService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly ILogger<PaymentWebhookService> _logger;

        public PaymentWebhookService(IUnitOfWork unitOfWork, ILogger<PaymentWebhookService> logger)
        {
            _unitOfWork = unitOfWork;
            _logger = logger;
        }

        public async Task HandlePaymobAsync(PaymobWebhookDto dto)
        {
            if (dto?.Obj == null)
                return;

            var obj = dto.Obj;

            var webhook = new PaymentWebhook
            {
                TransactionId = obj.Id,
                OrderId = 0, 
                PaymentMethod = obj.SourceData?.SubType ?? "Unknown",
                Success = obj.Success,
                Status = obj.Success ? "Approved" : "Declined",
                AmountCents = obj.AmountCents,
                Currency = obj.Currency ?? "EGP",
                SourceSubType = obj.SourceData?.SubType,
                SourceIssuer = dto.IssuerBank,
                CardLast4 = obj.SourceData?.PanLast4,
                PaymentProvider = "PayMob",
                ProviderOrderId = obj.Order?.Id.ToString(),
                RawData = System.Text.Json.JsonSerializer.Serialize(dto),
                HmacVerified = false, 
                AuthorizationCode = ExtractAuthorizationCode(obj),
                ReceiptNumber = ExtractReceiptNumber(obj),
                Is3DSecure = obj.SourceData?.SubType == "card" ? true : false,
                IsCapture = false, // Set based on PayMob response
                IsVoided = false,
                IsRefunded = false,
                IntegrationId = obj.PaymentKeyClaims?.OrderId.ToString(),
                ProfileId = null, // Extract from PayMob response if available
                ProcessedAt = DateTime.UtcNow,
                RetryCount = 0,
                WebhookUniqueKey = $"{obj.Id}_{obj.Order?.Id}_{obj.AmountCents}" // For idempotency
            };

            int localOrderId = 0;
            if (!string.IsNullOrWhiteSpace(obj.Order?.MerchantOrderId) && int.TryParse(obj.Order.MerchantOrderId, out var parsed))
            {
                localOrderId = parsed;
            }
            else if (obj.PaymentKeyClaims != null)
            {
                // fallback: find latest payment by amount and currency (best-effort)
                var amount = (decimal)obj.AmountCents / 100m;
                var payment = await _unitOfWork.Repository<Payment>()
                    .GetAll()
                    .Where(p => p.Amount == amount)
                    .OrderByDescending(p => p.Id)
                    .FirstOrDefaultAsync();
                if (payment != null)
                {
                    localOrderId = payment.OrderId;
                }
            }

            if (localOrderId > 0)
            {
                webhook.OrderId = localOrderId;
            }

            await _unitOfWork.Repository<PaymentWebhook>().CreateAsync(webhook);
            await _unitOfWork.CommitAsync();

            if (localOrderId <= 0)
            {
                _logger.LogWarning("Paymob webhook could not be linked to a local order. TxnId: {Txn}", obj.Id);
                return;
            }

            // Update payment and order
            var latestPayment = await _unitOfWork.Repository<Payment>()
                .GetAll()
                .Where(p => p.OrderId == localOrderId)
                .OrderByDescending(p => p.Id)
                .FirstOrDefaultAsync();

            if (latestPayment != null)
            {
               
                webhook.PaymentId = latestPayment.Id;
                
          
                var paymentStatus = obj.Success ? PaymentStatus.Completed : PaymentStatus.Failed;

                if (obj.Pending)
                {
                    paymentStatus = PaymentStatus.Pending;
                }
                else if (!obj.Success)
                {
                    paymentStatus = PaymentStatus.Failed;
                }
                else if (obj.Success)
                {
                    paymentStatus = PaymentStatus.Completed;
                }

                latestPayment.Status = paymentStatus;
                latestPayment.TransactionId = obj.Id.ToString();
                
                _logger.LogInformation("Updated payment {PaymentId} with status {Status} and transaction ID {TransactionId}", 
                    latestPayment.Id, paymentStatus, obj.Id);
            }

            var order = await _unitOfWork.Repository<Models.Order>().GetByIdAsync(localOrderId);
            if (order != null)
            {
                var orderStatus = obj.Success ? OrderStatus.Processing : OrderStatus.Cancelled;
                
                if (obj.Pending)
                {
                    orderStatus = OrderStatus.Pending;
                }
                else if (!obj.Success)
                {
                    orderStatus = OrderStatus.Cancelled;
                }
                else if (obj.Success)
                {
                    orderStatus = OrderStatus.Processing;
                }

                order.Status = orderStatus;
                _unitOfWork.Repository<Models.Order>().Update(order);
                
                _logger.LogInformation("Updated order {OrderId} with status {Status}", 
                    order.Id, orderStatus);
            }

            await _unitOfWork.CommitAsync();
        }

        private string? ExtractAuthorizationCode(PaymobTransactionObj obj)
        {
            // Try to extract from the data object if available
            if (obj.SourceData?.SubType == "card")
            {
                // In a real implementation, you'd parse the data object
                // For now, return a placeholder
                return "AUTH_" + obj.Id.ToString();
            }
            return null;
        }

        private string? ExtractReceiptNumber(PaymobTransactionObj obj)
        {
            // Try to extract from the data object if available
            if (obj.SourceData?.SubType == "card")
            {
                // In a real implementation, you'd parse the data object
                // For now, return a placeholder
                return "RECEIPT_" + obj.Id.ToString();
            }
            return null;
        }
    }
}


