using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Serialization;

namespace PaymentAPI.Models
{ 
    public  class HashV1
    {
        /// <summary>
        /// Compute Hash v1 from a flat dictionary of field name → value pairs.
        /// Null/empty values are excluded per spec.
        /// </summary>
        public static string Compute(IDictionary<string, string> parameters, string secretKey)
        {
            // Step 1 & 2: Filter nulls/empties, then sort by key (ascending)
            var sorted = parameters
                .Where(kv => !string.IsNullOrEmpty(kv.Value))
                .OrderBy(kv => kv.Key, StringComparer.Ordinal)
                .ToList();

            // Step 3: Concatenate values only (no separator, no keys)
            var sb = new StringBuilder();
            foreach (var kv in sorted)
                sb.Append(kv.Value);

            string concatenated = sb.ToString();

            // Step 4 & 5: HMAC-SHA256 → lowercase hex
            return HmacSha256Hex(concatenated, secretKey);
        }

        /// <summary>
        /// Convenience overload: pass an object, reflect its JsonPropertyName attributes
        /// to build the parameter dictionary automatically (skips null/empty strings).
        /// </summary>
        public static string ComputeFromObject(object obj, string secretKey)
        {
            var dict = new Dictionary<string, string>();
            foreach (var prop in obj.GetType().GetProperties())
            {
                // Get the JSON field name (or fall back to property name)
                var attr = (JsonPropertyNameAttribute?)Attribute.GetCustomAttribute(
                    prop, typeof(JsonPropertyNameAttribute));
                string key = attr?.Name ?? prop.Name;

                object? val = prop.GetValue(obj);
                if (val == null) continue;

                string strVal = val.ToString() ?? string.Empty;
                if (!string.IsNullOrEmpty(strVal))
                    dict[key] = strVal;
            }
            return Compute(dict, secretKey);
        }

        private static string HmacSha256Hex(string data, string key)
        {
            byte[] keyBytes = Encoding.UTF8.GetBytes(key);
            byte[] msgBytes = Encoding.UTF8.GetBytes(data);
            using var hmac = new HMACSHA256(keyBytes);
            byte[] hash = hmac.ComputeHash(msgBytes);
            return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
        }
    }
}
