using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using System.Xml;
using Formatting = Newtonsoft.Json.Formatting;

namespace Paytm
{
    public static class PaytmChecksum
    {
        // Generate signature from parameters (Dictionary)
        public static string generateSignature(Dictionary<string, string> parameters, string merchantKey)
        {
            string json = JsonConvert.SerializeObject(parameters, Formatting.None);
            return generateSignature(json, merchantKey);
        }

        // Generate signature from JSON body
        public static string generateSignature(string body, string merchantKey)
        {
            byte[] keyBytes = Encoding.UTF8.GetBytes(merchantKey);
            using (var hmac = new HMACSHA256(keyBytes))
            {
                byte[] hashMessage = hmac.ComputeHash(Encoding.UTF8.GetBytes(body));
                return Convert.ToBase64String(hashMessage);
            }
        }

        // Verify signature
        public static bool verifySignature(string body, string merchantKey, string checksum)
        {
            string newChecksum = generateSignature(body, merchantKey);
            return newChecksum.Equals(checksum);
        }

        public static bool verifySignature(Dictionary<string, string> parameters, string merchantKey, string checksum)
        {
            string json = JsonConvert.SerializeObject(parameters, Formatting.None);
            return verifySignature(json, merchantKey, checksum);
        }
    }
}
