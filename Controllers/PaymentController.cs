using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;
using Paytm; // ensure you have Paytm checksum lib

namespace PaymentAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class PaymentController : ControllerBase
    {
        private readonly IHttpClientFactory _httpClientFactory;

        public PaymentController(IHttpClientFactory httpClientFactory)
        {
            _httpClientFactory = httpClientFactory;
        }

        [HttpPost("paytm")]
        public async Task<IActionResult> PaytmPayment()
        {
            string dates = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

            // Build request map exactly as in WebForms code
            Dictionary<string, string> Map = new Dictionary<string, string>
            {
                { "paytmMid", "pfAmBe24305278340665" }, // MID
                { "paytmTid", "70001957" }, // TID
                { "transactionDateTime", dates },
                { "merchantTransactionId", "87789433221326908" + new Random().Next(10000) },
                { "transactionAmount", "100" },
                { "paymentMode", "CARD" }
            };

            // Generate checksum (sign Map only)
            string paytmChecksum = Checksum.generateSignature(Map, "pFL3Pvbsg&MKJKyN"); // merchant key

            // Convert Map to JObject
            JObject Obj = JObject.FromObject(Map);

            // Add merchantExtendedInfo node
            JObject merchantExtendedInfo = new JObject
            {
                { "paymentMode", "CARD" }
            };
            Obj.Add("merchantExtendedInfo", merchantExtendedInfo);

            // Convert to string for request body
            string post_data = Obj.ToString();

            // Build final JSON payload
            JObject requestRoot = new JObject
            {
                ["head"] = new JObject
                {
                    { "version", "3.1" },
                    { "requestTimeStamp", dates },
                    { "channelId", "EDC" },
                    { "checksum", paytmChecksum }
                },
                ["body"] = Obj
            };

            string body = JsonConvert.SerializeObject(requestRoot, Formatting.None);

            // Log/Debug
            Console.WriteLine("Request:\n" + body);

            // Send to Paytm
            string url = "https://securegw-stage.paytm.in/ecr/payment/request";
            var client = _httpClientFactory.CreateClient();
            var content = new StringContent(body, Encoding.UTF8, "application/json");

            HttpResponseMessage response;
            try
            {
                response = await client.PostAsync(url, content);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    error = "Failed to send request",
                    details = ex.Message
                });
            }

            string responseText = await response.Content.ReadAsStringAsync();
            Console.WriteLine("Response:\n" + responseText);

            // Return raw Paytm response
            return Content(responseText, "application/json");
        }
    }
}
