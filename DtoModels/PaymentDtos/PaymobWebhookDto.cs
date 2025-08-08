using System.Text.Json.Serialization;

namespace E_Commerce.DtoModels.PaymentDtos
{
    public class PaymobWebhookDto
    {
        [JsonPropertyName("type")] 
        public string? Type { get; set; }
        [JsonPropertyName("obj")] 
        public PaymobTransactionObj? Obj { get; set; }
        [JsonPropertyName("issuer_bank")] 
        public string? IssuerBank { get; set; }
        [JsonPropertyName("transaction_processed_callback_responses")] 
        public string? TransactionProcessedCallbackResponses { get; set; }
    }

    public class PaymobTransactionObj
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }
        [JsonPropertyName("pending")]
        public bool Pending { get; set; }
        [JsonPropertyName("amount_cents")]
        public long AmountCents { get; set; }
        [JsonPropertyName("success")] 
        public bool Success { get; set; }
        [JsonPropertyName("currency")]
        public string? Currency { get; set; }
        [JsonPropertyName("order")] 
        public PaymobOrder? Order { get; set; }
        [JsonPropertyName("payment_key_claims")] 
        public PaymobPaymentKeyClaims? PaymentKeyClaims { get; set; }
        [JsonPropertyName("source_data")] 
        public PaymobSourceData? SourceData { get; set; }
    }

    public class PaymobOrder
    {
        [JsonPropertyName("id")] 
        public long Id { get; set; }
        [JsonPropertyName("merchant_order_id")] 
        public string? MerchantOrderId { get; set; }
        [JsonPropertyName("amount_cents")]
        public long AmountCents { get; set; }
        [JsonPropertyName("currency")] 
        public string? Currency { get; set; }
    }

    public class PaymobPaymentKeyClaims
    {
        [JsonPropertyName("order_id")]
        public long OrderId { get; set; }
        [JsonPropertyName("amount_cents")] 
        public long AmountCents { get; set; }
        [JsonPropertyName("currency")] 
        public string? Currency { get; set; }
    }

    public class PaymobSourceData
    {
        [JsonPropertyName("type")] 
        public string? Type { get; set; }
        [JsonPropertyName("sub_type")]
        public string? SubType { get; set; }
        [JsonPropertyName("pan")]
        public string? PanLast4 { get; set; }
    }
}


