using System.Text.Json.Serialization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace PaymentAPI.Models
{
    public class HashV2
    {
        private static readonly JsonSerializerOptions _minifyOptions = new()
        {
            WriteIndented = false,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        /// <summary>
        /// Compute Hash v2 from any serialisable C# object.
        /// Ensure secureHash property is set to null before calling.
        /// </summary>
        public static string Compute(object payload, string secretKey)
        {
            // Step 1: minified JSON
            string json = JsonSerializer.Serialize(payload, _minifyOptions);

            Console.WriteLine($"[HashV2 Step 1] Minified JSON:\n  {json}");

            // Steps 2-3: HMAC-SHA256 → lowercase hex
            string result = HmacSha256Hex(json, secretKey);

            Console.WriteLine($"[HashV2 Step 4] Lowercase hex:\n  {result}");
            return result;
        }

        /// <summary>
        /// Compute Hash v2 from a raw JSON string (already minified).
        /// </summary>
        public static string ComputeFromString(string minifiedJson, string secretKey)
            => HmacSha256Hex(minifiedJson, secretKey);

        private static string HmacSha256Hex(string data, string key)
        {
            byte[] keyBytes = Encoding.UTF8.GetBytes(key);
            byte[] msgBytes = Encoding.UTF8.GetBytes(data);
            using var hmac = new HMACSHA256(keyBytes);
            byte[] hash = hmac.ComputeHash(msgBytes);
            return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
        }
    }
    public class InvoiceCreateRequest
    {
        [JsonPropertyName("addlParam1")] public string? addlParam1 { get; set; }
        [JsonPropertyName("addlParam2")] public string? addlParam2 { get; set; }
        [JsonPropertyName("aggregatorId")] public string? aggregatorId { get; set; }
        [JsonPropertyName("chargeAmount")] public string? chargeAmount { get; set; }
        [JsonPropertyName("chargeHead1")] public string? chargeHead1 { get; set; }
        [JsonPropertyName("chargeHead2")] public string? chargeHead2 { get; set; }
        [JsonPropertyName("chargeHead3")] public string? chargeHead3 { get; set; }
        [JsonPropertyName("currencyCode")] public string? currencyCode { get; set; }
        [JsonPropertyName("desc")] public string? desc { get; set; }
        [JsonPropertyName("dueDate")] public string? dueDate { get; set; }
        [JsonPropertyName("emailID")] public string? emailID { get; set; }
        [JsonPropertyName("invoiceNo")] public string? invoiceNo { get; set; }
        [JsonPropertyName("merchantId")] public string? merchantId { get; set; }
        [JsonPropertyName("mobileNo")] public string? mobileNo { get; set; }
        [JsonPropertyName("paymentReturnURL")] public string? paymentReturnURL { get; set; }
        [JsonPropertyName("secureHash")] public string? secureHash { get; set; } // null when hashing
        [JsonPropertyName("userName")] public string? userName { get; set; }
    }

    /// <summary>Invoice Status API request (Hash v1)</summary>
    public class InvoiceStatusRequest
    {
        [JsonPropertyName("merchantId")] public string? MerchantId { get; set; }
        [JsonPropertyName("invoiceNo")] public string? InvoiceNo { get; set; }
        [JsonPropertyName("reqType")] public string? ReqType { get; set; }
        [JsonPropertyName("secureHash")] public string? SecureHash { get; set; }
    }

    /// <summary>Invoice Refund API request (Hash v1 + v2 for JSON)</summary>
    public class InvoiceRefundRequest
    {
        [JsonPropertyName("merchantId")] public string? MerchantId { get; set; }
        [JsonPropertyName("aggregatorId")] public string? AggregatorId { get; set; }
        [JsonPropertyName("invoiceNo")] public string? InvoiceNo { get; set; }
        [JsonPropertyName("amount")] public string? Amount { get; set; }
        [JsonPropertyName("secureHash")] public string? SecureHash { get; set; }
    }

}
