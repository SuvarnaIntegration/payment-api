using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json.Linq;
using PaymentAPI.Models;
using Paytm;
using System;
using System.Globalization;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using static System.Collections.Specialized.BitVector32;

namespace PaymentAPI.Controllers
{
    [Route("supay")]
    public class SuvarnaPaymentController : ControllerBase
    {
        private readonly IConfiguration _config;
        //private readonly HttpClient _httpClient;
        private readonly IHttpClientFactory _httpClient;
        private readonly LogService _logService;
        public SuvarnaPaymentController(IConfiguration config, IHttpClientFactory httpClientFactory, LogService logService)
        {
            _config = config;
            _logService = logService;
            _httpClient = httpClientFactory;
        }

        [HttpPost("pushtxn")]
        public async Task<IActionResult> PostPaymenttxn([FromBody] PaymentRequest request)
        {
            await _logService.WritePaymentLogAsync(request.Invoiceno, "common_request_" + request.Provider, request);
            if (string.IsNullOrEmpty(request.Provider))
                return BadRequest(new { code = 400, status = "failed", message = "Provider required" });
            if (string.IsNullOrEmpty(request.Amount))
                return BadRequest(new { code = 400, status = "failed", message = "amount required" });
            if (string.IsNullOrEmpty(request.Tid))
                return BadRequest(new { code = 400, status = "failed", message = "terminal Id required" });
            if (string.IsNullOrEmpty(request.Invoiceno))
                return BadRequest(new { code = 400, status = "failed", message = "transaction Id required" });
            //if (string.IsNullOrEmpty(request.Txntype))
            //    return BadRequest(new { code = 400, status = "failed", message = "transaction type required" });

            switch (request.Provider.ToLower())
            {
                case "razorpay": return await razorpaymethod(request, "initiate");
                case "icici": return await icicipaymethod(request, "initiate");
                case "hdfc": return await hdfcpaymethod(request, "initiate");
                case "paytm": return await paytmmethod(request, "initiate");
                case "worldline": return await worldlinemethod(request, "initiate"); // default initiate
                case "mswipe": return await MswipePaymentMethod(request, "initiate");
                case "pinelab": return await pinelabmethod(request, "initiate");

                default:
                    return StatusCode(401, new
                    {
                        code = 401,
                        status = "failed",
                        message = "Unknown Provider",
                        timestamp = DateTime.Now.ToString("s")
                    });
            }
        }

        [HttpPost("txnstatus")]
        public async Task<IActionResult> PostPaymentstatus([FromBody] PaymentRequest request)
        {
            await _logService.WritePaymentLogAsync(request.Invoiceno, "request", request);

            if (string.IsNullOrEmpty(request.Provider))
                return BadRequest(new { code = 400, status = "failed", message = "Provider required" });
            if (string.IsNullOrEmpty(request.Tid))
                return BadRequest(new { code = 400, status = "failed", message = "terminal Id required" });
            if (string.IsNullOrEmpty(request.Invoiceno))
                return BadRequest(new { code = 400, status = "failed", message = "transaction Id required" });
            if (string.IsNullOrEmpty(request.refernceId))
                return BadRequest(new { code = 400, status = "failed", message = "refernce Id required" });

            switch (request.Provider.ToLower())
            {
                case "razorpay": return await razorpaymethod(request, "status");
                case "icici": return await icicipaymethod(request, "status");
                case "hdfc": return await hdfcpaymethod(request, "status");
                case "paytm": return await paytmmethod(request, "status");
                case "worldline": return await worldlinemethod(request, "status");
                case "mswipe": return await MswipePaymentMethod(request, "status");
                case "pinelab": return await pinelabmethod(request, "status");

                default:
                    return StatusCode(401, new
                    {
                        code = 401,
                        status = "failed",
                        message = "Unknown Provider",
                        timestamp = DateTime.Now.ToString("s")
                    });
            }
        }


        [HttpPost("canceltxn")]
        public async Task<IActionResult> PostPaymentcancel([FromBody] PaymentRequest request)
        {
            await _logService.WritePaymentLogAsync(request.Invoiceno, "request", request);
            if (string.IsNullOrEmpty(request.Provider))
                return BadRequest(new { code = 400, status = "failed", message = "Provider required" });
            if (string.IsNullOrEmpty(request.Tid))
                return BadRequest(new { code = 400, status = "failed", message = "terminal Id required" });
            if (string.IsNullOrEmpty(request.Invoiceno))
                return BadRequest(new { code = 400, status = "failed", message = "transaction Id required" });
            if (string.IsNullOrEmpty(request.refernceId))
                return BadRequest(new { code = 400, status = "failed", message = "refernce Id required" });

            switch (request.Provider.ToLower())
            {
                case "icici": return await icicipaymethod(request, "cancel");
                case "razorpay": return await razorpaymethod(request, "cancel");
                case "hdfc": return await hdfcpaymethod(request, "cancel");
                case "worldline": return await worldlinemethod(request, "cancel"); // default initiate
                case "mswipe": return await MswipePaymentMethod(request, "cancel");
                case "pinelab": return await pinelabmethod(request, "cancel");
                case "paytm": return await paytmmethod(request, "cancel");

                default:
                    return StatusCode(401, new
                    {
                        code = 401,
                        status = "failed",
                        message = "Unknown Provider",
                        timestamp = DateTime.Now.ToString("s")
                    });
            }
        }


        // ------------------ Razor Pay ------------------
        public async Task<IActionResult> razorpaymethod(PaymentRequest request, string action)
        {
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
            PaymentRequestResponse paymentRequestRes = new PaymentRequestResponse();
            var section = _config.GetSection("PaymentProviders:razorpay");
            string username = section["username"];
            string appKey = section["appKey"];

            string url = action switch
            {
                "status" => section["StatusUrl"],
                "cancel" => section["CancelUrl"],
                _ => section["InitiateUrl"] // default = pay
            };

            // decide mode based on Txntype
            string txnMode = request.Txntype switch
            {
                "00" => "CARD",
                "01" => "UPI",
                "02" => "CASH",
                _ => "CARD"
            };

            object payload;

            // ✅ Build payload based on action
            if (action == "initiate")
            {
                payload = new
                {
                    username = username,
                    appKey = appKey,
                    amount = request.Amount,
                    customerMobileNumber = request.Mobileno,
                    externalRefNumber = request.Invoiceno,
                    externalRefNumber2 = request.Patientid,
                    externalRefNumber3 = request.Locname,
                    externalRefNumber4 = request.Companyname,
                    externalRefNumber5 = request.Tid,
                    pushTo = new
                    {
                        deviceId = request.Tid + "|ezetap_android"
                    },
                    mode = txnMode
                };
            }
            else if (action == "status")
            {
                payload = new
                {
                    username = username,
                    appKey = appKey,
                    origP2pRequestId = request.refernceId
                };
            }
            else if (action == "cancel")
            {
                payload = new
                {
                    username = username,
                    appKey = appKey,
                    origP2pRequestId = request.refernceId,
                    pushTo = new
                    {
                        deviceId = request.Tid + "|ezetap_android"
                    }
                };
            }
            else
            {
                // fallback default payload
                payload = new { username, appKey };
            }

            var jsonPayload = JsonSerializer.Serialize(payload);
            await _logService.WritePaymentLogAsync(request.Invoiceno, "razorpaymethod_request_" + action, jsonPayload);
            var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");
            var client = _httpClient.CreateClient();
            var response = await client.PostAsync(url, content);
            var responseString = await response.Content.ReadAsStringAsync();

            // ✅ Deserialize JSON into a dynamic object
            var jsonObj = JsonSerializer.Deserialize<JsonElement>(responseString);

            // Map response safely
            paymentRequestRes.code = (int)response.StatusCode;
            paymentRequestRes.status = jsonObj.TryGetProperty("success", out var successProp)
                                        ? successProp.GetBoolean().ToString()
                                        : "false";
            paymentRequestRes.message = jsonObj.TryGetProperty("message", out var messageProp)
                                        ? messageProp.GetString()
                                        : "Unknown error";
            paymentRequestRes.transactionId = request.Invoiceno;

            // You can later extend this mapping based on actual API response
            if (paymentRequestRes.status != "False")
            {
                paymentRequestRes.refernceId = jsonObj.TryGetProperty("p2pRequestId", out var p2pRequestIdProp)
                                        ? p2pRequestIdProp.GetString()
                                        : "";
            }
            paymentRequestRes.cardNo = "";
            paymentRequestRes.upi = "";
            paymentRequestRes.paymentMode = request.Txntype;

            // 🔹 Keep the original JSON for debugging/logging
            paymentRequestRes.originalResponse = responseString;

            await _logService.WritePaymentLogAsync(request.Invoiceno, "razorpaymethod_response_" + action, paymentRequestRes);
            return StatusCode((int)response.StatusCode, paymentRequestRes);
        }

        // ------------------ icici Pay ------------------
        public async Task<IActionResult> icicipaymethod(PaymentRequest request, string action)
        {
            // Force TLS 1.2 (important for ICICI endpoint)
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;

            PaymentRequestResponse paymentRequestRes = new PaymentRequestResponse();
            var section = _config.GetSection("PaymentProviders:icici");
            string erp_client_id = section["erp_client_id"];
            string source_id = section["source_id"];
            string mid = section["mid"];

            string url = action switch
            {
                "status" => section["StatusUrl"],
                "cancel" => section["CancelUrl"],
                _ => section["InitiateUrl"] // default = initiate/pay
            };

            // decide mode based on Txntype
            string txnMode = request.Txntype switch
            {
                "00" => "1",  // CARD
                "01" => "16", // QRCODE
                "02" => "18", // REFUND
                _ => "16"
            };

            // Unique txn id (ICICI expects 16-digit like "2402300255304441")
            string erp_tran_id = DateTime.Now.ToString("yyMMddHHmmssffff");

            object payload;

            // ✅ Build payload based on action
            if (action == "initiate")
            {
                payload = new
                {
                    mid = mid,
                    tid = request.Tid,
                    tran_type = Convert.ToInt32(txnMode),
                    amount = request.Amount,
                    bill_no = request.Invoiceno,
                    tip = "0.00",
                    erp_tran_id = erp_tran_id,
                    erp_client_id = erp_client_id,
                    source_id = source_id
                };
            }
            else if (action == "status" || action == "cancel")
            {
                payload = new
                {
                    mid = mid,
                    tid = request.Tid,
                    tran_type = Convert.ToInt32(txnMode),
                    bill_no = request.Invoiceno,
                    erp_tran_id = request.refernceId,
                    erp_client_id = erp_client_id,
                    source_id = source_id
                };
            }
            else
            {
                payload = new { mid = mid, tid = request.Tid }; // fallback
            }

            // ✅ Ensure exact casing (snake_case preserved in payload)
            var jsonPayload = JsonSerializer.Serialize(payload, new JsonSerializerOptions
            {
                PropertyNamingPolicy = null
            });

            await _logService.WritePaymentLogAsync(request.Invoiceno, "icicipaymethod_request_" + action, jsonPayload);
            var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

            var client = _httpClient.CreateClient();


            // ✅ Add Postman-like headers
            client.DefaultRequestHeaders.Clear();
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("*/*"));
            client.DefaultRequestHeaders.UserAgent.ParseAdd("PostmanRuntime/7.39.0");
            client.DefaultRequestHeaders.Connection.Add("keep-alive");
            client.DefaultRequestHeaders.AcceptEncoding.Add(new StringWithQualityHeaderValue("gzip"));
            client.DefaultRequestHeaders.AcceptEncoding.Add(new StringWithQualityHeaderValue("deflate"));
            client.DefaultRequestHeaders.AcceptEncoding.Add(new StringWithQualityHeaderValue("br"));

            // Make request
            var response = await client.PostAsync(url, content);

            var responseString = await response.Content.ReadAsStringAsync();


            // Always keep raw response
            paymentRequestRes.originalResponse = responseString;
            paymentRequestRes.code = (int)response.StatusCode;

            try
            {
                // Check content type → if not JSON, return raw response
                if (response.Content.Headers.ContentType?.MediaType != "application/json")
                {
                    paymentRequestRes.status = "Failed";
                    paymentRequestRes.message = "Non-JSON response from ICICI";
                    return StatusCode((int)response.StatusCode, paymentRequestRes);
                }

                // ✅ Parse JSON safely
                var jsonObj = JsonSerializer.Deserialize<JsonElement>(responseString);

                // Status mapping (default failed)
                paymentRequestRes.status = "False";

                if (jsonObj.TryGetProperty("ResponseCode", out var codeProp))
                {
                    paymentRequestRes.status = codeProp.GetString() == "00" ? "True" : "False";
                }

                // Message
                paymentRequestRes.message = jsonObj.TryGetProperty("ResponseDesc", out var descProp)
                    ? descProp.GetString()
                    : "Unknown error";

                // Transaction ID (map to ERP tran_id or bill_no)
                paymentRequestRes.transactionId = jsonObj.TryGetProperty("bill_no", out var billNoProp)
                    ? billNoProp.GetString()
                    : request.Invoiceno;

                // Reference ID
                paymentRequestRes.refernceId = jsonObj.TryGetProperty("Tran_Id", out var tranIdProp)
                    ? tranIdProp.GetString()
                    : erp_tran_id;

                // Optional mappings
                paymentRequestRes.cardNo = "";
                paymentRequestRes.upi = "";
                paymentRequestRes.paymentMode = request.Txntype;
            }
            catch (JsonException ex)
            {
                // JSON parse error
                paymentRequestRes.status = "Failed";
                paymentRequestRes.message = $"Invalid JSON response: {ex.Message}";
            }
            await _logService.WritePaymentLogAsync(request.Invoiceno, "icicipaymethod_response_" + action, paymentRequestRes);
            return StatusCode((int)response.StatusCode, paymentRequestRes);
        }

        // ------------------ hdfc Pay ------------------
        public async Task<IActionResult> hdfcpaymethod(PaymentRequest request, string action)
        {
            // Force TLS 1.2 (important for ICICI endpoint)
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;

            PaymentRequestResponse paymentRequestRes = new PaymentRequestResponse();
            var section = _config.GetSection("PaymentProviders:hdfc");
            string erp_client_id = section["erp_client_id"];
            string source_id = section["source_id"];
            string mid = section["mid"];

            string url = action switch
            {
                "status" => section["StatusUrl"],
                "cancel" => section["CancelUrl"],
                _ => section["InitiateUrl"] // default = initiate/pay
            };

            // decide mode based on Txntype
            string txnMode = request.Txntype switch
            {
                "00" => "1",  // CARD
                "01" => "16", // QRCODE
                "02" => "18", // REFUND
                _ => "16"
            };

            // Unique txn id (ICICI expects 16-digit like "2402300255304441")
            string erp_tran_id = DateTime.Now.ToString("yyMMddHHmmssffff");

            object payload;

            // ✅ Build payload based on action
            if (action == "initiate")
            {
                payload = new
                {
                    mid = mid,
                    tid = request.Tid,
                    tran_type = Convert.ToInt32(txnMode),
                    amount = request.Amount,
                    bill_no = request.Invoiceno,
                    tip = "0.00",
                    erp_tran_id = erp_tran_id,
                    erp_client_id = erp_client_id,
                    source_id = source_id
                };
            }
            else if (action == "status" || action == "cancel")
            {
                payload = new
                {
                    mid = mid,
                    tid = request.Tid,
                    tran_type = Convert.ToInt32(txnMode),
                    bill_no = request.Invoiceno,
                    erp_tran_id = request.refernceId,
                    erp_client_id = erp_client_id,
                    source_id = source_id
                };
            }
            else
            {
                payload = new { mid = mid, tid = request.Tid }; // fallback
            }

            // ✅ Ensure exact casing (snake_case preserved in payload)
            var jsonPayload = JsonSerializer.Serialize(payload, new JsonSerializerOptions
            {
                PropertyNamingPolicy = null
            });
            await _logService.WritePaymentLogAsync(request.Invoiceno, "hdfcpaymethod_request_" + action, jsonPayload);
            var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");
            var client = _httpClient.CreateClient();
            // ✅ Add Postman-like headers
            client.DefaultRequestHeaders.Clear();
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("*/*"));
            client.DefaultRequestHeaders.UserAgent.ParseAdd("PostmanRuntime/7.39.0");
            client.DefaultRequestHeaders.Connection.Add("keep-alive");
            client.DefaultRequestHeaders.AcceptEncoding.Add(new StringWithQualityHeaderValue("gzip"));
            client.DefaultRequestHeaders.AcceptEncoding.Add(new StringWithQualityHeaderValue("deflate"));
            client.DefaultRequestHeaders.AcceptEncoding.Add(new StringWithQualityHeaderValue("br"));

            // Make request
            var response = await client.PostAsync(url, content);

            var responseString = await response.Content.ReadAsStringAsync();


            // Always keep raw response
            paymentRequestRes.originalResponse = responseString;
            paymentRequestRes.code = (int)response.StatusCode;

            try
            {
                // Check content type → if not JSON, return raw response
                if (response.Content.Headers.ContentType?.MediaType != "application/json")
                {
                    paymentRequestRes.status = "Failed";
                    paymentRequestRes.message = "Non-JSON response from ICICI";
                    return StatusCode((int)response.StatusCode, paymentRequestRes);
                }

                // ✅ Parse JSON safely
                var jsonObj = JsonSerializer.Deserialize<JsonElement>(responseString);

                // Status mapping (default failed)
                paymentRequestRes.status = "False";

                if (jsonObj.TryGetProperty("ResponseCode", out var codeProp))
                {
                    paymentRequestRes.status = codeProp.GetString() == "00" ? "True" : "False";
                }

                // Message
                paymentRequestRes.message = jsonObj.TryGetProperty("ResponseDesc", out var descProp)
                    ? descProp.GetString()
                    : "Unknown error";

                // Transaction ID (map to ERP tran_id or bill_no)
                paymentRequestRes.transactionId = jsonObj.TryGetProperty("bill_no", out var billNoProp)
                    ? billNoProp.GetString()
                    : request.Invoiceno;

                // Reference ID
                paymentRequestRes.refernceId = jsonObj.TryGetProperty("Tran_Id", out var tranIdProp)
                    ? tranIdProp.GetString()
                    : erp_tran_id;

                // Optional mappings
                paymentRequestRes.cardNo = "";
                paymentRequestRes.upi = "";
                paymentRequestRes.paymentMode = request.Txntype;
            }
            catch (JsonException ex)
            {
                // JSON parse error
                paymentRequestRes.status = "Failed";
                paymentRequestRes.message = $"Invalid JSON response: {ex.Message}";
            }
            await _logService.WritePaymentLogAsync(request.Invoiceno, "hdfcpaymethod_response_" + action, paymentRequestRes);
            return StatusCode((int)response.StatusCode, paymentRequestRes);
        }

        // ------------------ mswipe Pay ------------------
        public async Task<IActionResult> MswipePaymentMethod([FromBody] PaymentRequest request, string action)
        {
            try
            {
                ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;

                PaymentRequestResponse paymentRequestRes = new PaymentRequestResponse();
                var section = _config.GetSection("PaymentProviders:mswipe");

                string saltKey = section["SALT_KEY"];
                string username = section["username"];
                string clientKey = section["clientKey"];
                string clientcode = section["clientcode"];
                string userId = section["username"];      // new for status API
                string password = section["password"];  // new for status API

                // 🔹 Select API URL
                string url = action switch
                {
                    "status" => section["StatusUrl"],
                    "cancel" => section["CancelUrl"],
                    _ => section["InitiateUrl"]   // default initiate
                };

                object payload;
                HttpRequestMessage requestMsg;

                // decide mode based on Txntype
                string txnMode = request.Txntype switch
                {
                    "00" => "00",  // CARD
                    "01" => "03",  // QRCODE
                    "02" => "00",  // CARD
                    _ => "00"
                };

                if (action == "initiate")
                {
                    string mac = GenerateMac(request.Amount, txnMode, clientcode, saltKey);

                    payload = new
                    {
                        amount = request.Amount,
                        clientcode = clientcode,
                        Mac = mac,
                        notes = request.Remarks,
                        txntype = txnMode,
                        storeid = "",
                        tid = request.Tid,
                        invoiceno = request.Invoiceno,
                        extranotes1 = request.Locname,
                        extranotes2 = request.Companyname,
                        extranotes3 = request.Locid,
                        extranotes4 = request.refernceId,
                        extranotes5 = request.Remarks,
                        extranotes6 = "",
                        extranotes7 = "",
                        extranotes8 = "",
                        extranotes9 = ""
                    };

                    requestMsg = new HttpRequestMessage(HttpMethod.Post, url);
                    requestMsg.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
                }
                else if (action == "cancel")
                {
                    payload = new
                    {
                        tokenid = request.refernceId,
                        invoiceno = request.Invoiceno,
                        clientcode = clientcode
                    };

                    requestMsg = new HttpRequestMessage(HttpMethod.Post, url);
                    requestMsg.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
                }
                else if (action == "status")
                {
                    payload = new
                    {
                        client_code = clientcode,
                        mer_invoiceno = request.Invoiceno
                    };

                    requestMsg = new HttpRequestMessage(HttpMethod.Post, url);
                    requestMsg.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

                    // 🔹 Add required headers
                    requestMsg.Headers.Add("userId", userId);
                    requestMsg.Headers.Add("password", password);
                }
                else
                {
                    return BadRequest(new { error = "Invalid action. Use initiate, cancel or status." });
                }
                await _logService.WritePaymentLogAsync(request.Invoiceno, "mswipepaymethod_request_" + action, requestMsg);
                // 🔹 Send Request
                var client = _httpClient.CreateClient();
                var response = await client.SendAsync(requestMsg);
                var responseString = await response.Content.ReadAsStringAsync();

                var jsonObj = JsonSerializer.Deserialize<JsonElement>(responseString);

                // 🔹 Map Response
                paymentRequestRes.code = 200;

                if (action == "initiate")
                {
                    paymentRequestRes.status = jsonObj.TryGetProperty("F039", out var f039Prop)
                                               ? (f039Prop.GetString() == "00" ? "True" : "False")
                                               : "False";

                    paymentRequestRes.message = jsonObj.TryGetProperty("Desc", out var descProp)
                                                ? descProp.GetString()
                                                : "Unknown error";

                    paymentRequestRes.refernceId = jsonObj.TryGetProperty("token", out var tokenProp)
                                                   ? tokenProp.GetString()
                                                   : "";
                }
                else if (action == "cancel")
                {
                    paymentRequestRes.status = jsonObj.TryGetProperty("ResponseCode", out var respCode)
                                               ? (respCode.GetString() == "00" ? "True" : "False")
                                               : "False";

                    paymentRequestRes.message = jsonObj.TryGetProperty("ResponseMessage", out var msgProp)
                                                ? msgProp.GetString()
                                                : "Unknown error";

                    paymentRequestRes.refernceId = request.refernceId; // comes from request
                }
                else if (action == "status")
                {
                    paymentRequestRes.status = jsonObj.TryGetProperty("status", out var vData)
                                               ? (vData.GetString() != "False" ? "True" : "False")
                                               : "False";

                    paymentRequestRes.message = jsonObj.TryGetProperty("ErrMsg", out var errProp)
                                                ? errProp.GetString()
                                                : "Unknown error";

                    paymentRequestRes.refernceId = request.Invoiceno; // map invoice back
                }

                paymentRequestRes.transactionId = request.Invoiceno;
                paymentRequestRes.originalResponse = responseString;
                await _logService.WritePaymentLogAsync(request.Invoiceno, "mswipepaymethod_response_" + action, paymentRequestRes);
                return StatusCode((int)200, paymentRequestRes);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        // ------------------ PineLab Pay ------------------
        public async Task<IActionResult> pinelabmethod(PaymentRequest request, string action)
        {
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
            PaymentRequestResponse paymentRequestRes = new PaymentRequestResponse();
            var section = _config.GetSection("PaymentProviders:pinelab");
            string UserID = section["UserID"];
            string MerchantID = section["MerchantID"];
            string MerchantStorePosCode = section["MerchantStorePosCode"];
            string SecurityToken = section["SecurityToken"];

            string url = action switch
            {
                "status" => section["StatusUrl"],
                "cancel" => section["CancelUrl"],
                _ => section["InitiateUrl"] // default = pay
            };

            // decide mode based on Txntype
            string txnMode = request.Txntype switch
            {
                "00" => "3",
                "01" => "UPI",
                "02" => "CASH",
                _ => ""
            };

            object payload;


            // ✅ Build payload based on action
            if (action == "initiate")
            {
                decimal originalAmount = decimal.Parse(request.Amount);  // convert string → decimal
                decimal apiAmount = originalAmount * 100;

                string Amount = apiAmount.ToString();
                if (Amount.EndsWith(".00"))
                {
                    Amount = Amount.Substring(0, Amount.Length - 3);
                }

                payload = new
                {
                    TransactionNumber = request.Invoiceno,
                    SequenceNumber = "1",
                    AllowedPaymentMode = "1",
                    MerchantStorePosCode = MerchantStorePosCode,
                    Amount = Amount,
                    UserID = UserID,
                    MerchantID = MerchantID,
                    SecurityToken = SecurityToken,
                    IMEI = request.Tid,
                    AutoCancelDurationInMinutes = "5",
                    TxnType = txnMode,
                    PlutusTransactionReferenceID = request.refernceId
                };

            }
            else if (action == "status")
            {
                payload = new
                {
                    MerchantID = MerchantID,
                    SecurityToken = SecurityToken,
                    IMEI = request.Tid,
                    MerchantStorePosCode = MerchantStorePosCode,
                    PlutusTransactionReferenceID = request.refernceId
                };
            }
            else if (action == "cancel")
            {

                decimal originalAmount = decimal.Parse(request.Amount);  // convert string → decimal
                decimal apiAmount = originalAmount * 100;

                string Amount = apiAmount.ToString();
                if (Amount.EndsWith(".00"))
                {
                    Amount = Amount.Substring(0, Amount.Length - 3);
                }

                payload = new
                {
                    UserID = UserID,
                    MerchantID = MerchantID,
                    MerchantStorePosCode = MerchantStorePosCode,
                    SecurityToken = SecurityToken,
                    Amount = Amount,
                    IMEI = request.Tid,
                    SequenceNumber = "1",
                    TransactionNumber = request.Invoiceno,
                    AllowedPaymentMode = "1",
                    AutoCancelDurationInMinutes = "3",
                    PlutusTransactionReferenceID = request.refernceId,
                    TxnType = txnMode
                };
            }
            else
            {
                // fallback default payload
                payload = new { UserID, MerchantID };
            }

            var jsonPayload = JsonSerializer.Serialize(payload);
            var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");
            await _logService.WritePaymentLogAsync(request.Invoiceno, "pinelabpaymethod_request_" + action, jsonPayload);
            await _logService.WritePaymentLogAsync(request.Invoiceno, "pinelabpaymethod_response_" + action, url);
            var client = _httpClient.CreateClient();
            var response = await client.PostAsync(url, content);
            var responseString = await response.Content.ReadAsStringAsync();

            await _logService.WritePaymentLogAsync(request.Invoiceno, "pinelabpaymethod_response_" + action, responseString);
            // ✅ Deserialize JSON into a dynamic object
            var jsonObj = JsonSerializer.Deserialize<JsonElement>(responseString);

            // Map response safely
            paymentRequestRes.code = (int)response.StatusCode;
            if (action == "initiate")
            {

                paymentRequestRes.status = jsonObj.TryGetProperty("ResponseCode", out var respCode)
       ? (respCode.GetInt32() == 0 ? "True" : "False")
       : "False";

                paymentRequestRes.message = jsonObj.TryGetProperty("ResponseMessage", out var messageProp)
                                            ? messageProp.GetString()
                                            : "Unknown error";
                paymentRequestRes.transactionId = request.Invoiceno;


                paymentRequestRes.refernceId = jsonObj.TryGetProperty("PlutusTransactionReferenceID", out var p2pRequestIdProp)
                                        ? p2pRequestIdProp.GetInt32().ToString()
                                        : "";
            }
            else if (action == "status")
            {
                // ✅ Status
                paymentRequestRes.status = jsonObj.TryGetProperty("ResponseCode", out var respCode)
                    ? (respCode.GetInt32() == 0 ? "True" : "False")
                    : "False";

                // ✅ Message
                paymentRequestRes.message = jsonObj.TryGetProperty("ResponseMessage", out var messageProp)
                    ? messageProp.GetString()
                    : "Unknown error";

                // ✅ TransactionId from request
                paymentRequestRes.transactionId = request.Invoiceno;

                // ✅ ReferenceId → Extract from TransactionData array ("Tag" == "RRN")
                paymentRequestRes.refernceId = "";
                if (jsonObj.TryGetProperty("TransactionData", out var txnDataProp) &&
                    txnDataProp.ValueKind == JsonValueKind.Array)
                {
                    foreach (var item in txnDataProp.EnumerateArray())
                    {
                        if (item.TryGetProperty("Tag", out var tagProp) &&
                            tagProp.GetString() == "TransactionLogId" &&
                            item.TryGetProperty("Value", out var valueProp))
                        {
                            paymentRequestRes.refernceId = valueProp.GetString();
                            break;
                        }
                    }
                }
            }

            else if (action == "cancel")
            {
                // ✅ Status
                paymentRequestRes.status = jsonObj.TryGetProperty("ResponseCode", out var respCode)
                    ? (respCode.GetInt32() == 0 ? "True" : "False")
                    : "False";

                // ✅ Message
                paymentRequestRes.message = jsonObj.TryGetProperty("ResponseMessage", out var messageProp)
                    ? messageProp.GetString()
                    : "Unknown error";

                // ✅ TransactionId from request
                paymentRequestRes.transactionId = request.Invoiceno;

                // ✅ ReferenceId → Extract from TransactionData array ("Tag" == "RRN")
                paymentRequestRes.refernceId = request.refernceId;

            }
            paymentRequestRes.cardNo = "";
            paymentRequestRes.upi = "";
            paymentRequestRes.paymentMode = request.Txntype;

            // 🔹 Keep the original JSON for debugging/logging
            paymentRequestRes.originalResponse = responseString;

            return StatusCode((int)response.StatusCode, paymentRequestRes);
        }

        // ------------------ WORLDLINE ------------------
        public async Task<IActionResult> worldlinemethod(PaymentRequest request, string action)
        {
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
            PaymentRequestResponse paymentRequestRes = new PaymentRequestResponse();
            var section = _config.GetSection("PaymentProviders:worldline");
            string encKey = section["EncKey"];
            string encIv = section["EncIV"];

            string url = action switch
            {
                "status" => section["StatusUrl"],
                "cancel" => section["CancelUrl"],
                _ => section["InitiateUrl"]
            };

            // Decide mode based on Txntype
            string txnMode = request.Txntype switch
            {
                "00" => "SALE",
                "01" => "SALE-BQR",
                "02" => "CASH",
                _ => "SALE"
            };

            string actionId = request.Txntype switch
            {
                "00" => "1",
                "01" => "132",
                _ => "1"
            };
            object payload;

            // ✅ Build payload based on action
            if (action == "initiate")
            {


                payload = new
                {
                    tid = request.Tid,
                    amount = request.Amount,
                    organization_code = "Retail",
                    additional_attribute1 = request.Patientname,
                    additional_attribute2 = request.Patientid,
                    additional_attribute3 = request.Mobileno,
                    invoiceNumber = request.Invoiceno,
                    rrn = string.Empty,
                    type = txnMode,
                    cb_amt = string.Empty,
                    app_code = string.Empty,
                    tokenisedValue = request.refernceId,
                    actionId,
                    request_urn = string.Empty
                };
            }
            else if (action == "status")
            {
                payload = new
                {
                    urn = request.refernceId,
                    tid = request.Tid,
                    request_urn = string.Empty
                };
            }
            else if (action == "cancel")
            {


                payload = new
                {

                    actionId = actionId,
                    additional_attribute1 = "",
                    additional_attribute2 = "",
                    additional_attribute3 = "",
                    amount = "",
                    app_code = "",
                    cb_amt = "",
                    invoiceNumber = request.Invoiceno,
                    organization_code = "Retail",
                    request_urn = "",
                    rrn = "",
                    tid = request.Tid,
                    tokenisedValue = "",
                    type = txnMode,
                    urn = request.refernceId


                };
            }
            else
            {
                // Safe fallback payload
                payload = new
                {
                    tid = request.Tid,
                    amount = request.Amount,
                    type = txnMode,
                    referenceId = request.refernceId
                };
            }

            // Serialize + Encrypt payload
            string jsonPayload = JsonSerializer.Serialize(payload);
            await _logService.WritePaymentLogAsync(request.Invoiceno, "worldlinepaymethod_request_" + action, jsonPayload);
            string encryptedPayload;
            try
            {
                encryptedPayload = AES_256_CBC.Encrypt(jsonPayload, encKey, encIv);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = "Encryption failed", details = ex.Message });
            }



            // Build final request body (pretty JSON for readability)
            var finalBody = new { data = encryptedPayload };
            string requestBody = JsonSerializer.Serialize(
                finalBody,
                new JsonSerializerOptions { WriteIndented = true }
            );



            var content = new StringContent(encryptedPayload, Encoding.UTF8, "text/plain");
            var client = _httpClient.CreateClient();
            HttpResponseMessage response;
            try
            {
                response = await client.PostAsync(url, content);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = "Request to Worldline failed", details = ex.Message });
            }

            string responseString = await response.Content.ReadAsStringAsync();

            // Determine final JSON string to parse
            string finalJson = responseString;

            try
            {
                using var doc = JsonDocument.Parse(responseString);
                if (doc.RootElement.TryGetProperty("data", out var dataElement))
                {
                    string decrypted = AES_256_CBC.Decrypt(dataElement.GetString(), encKey, encIv);
                    finalJson = decrypted; // overwrite with decrypted payload
                }
            }
            catch
            {
                // ignore → means response is already plain JSON
            }

            // Deserialize safely into JsonElement
            var jsonObj = JsonSerializer.Deserialize<JsonElement>(finalJson);
            await _logService.WritePaymentLogAsync(request.Invoiceno, "worldlinepaymethod_response_" + action, finalJson);
            // Map common fields
            paymentRequestRes.code = (int)response.StatusCode;

            if (action == "initiate" || action == "status")
            {
                // ✅ Status
                paymentRequestRes.status = jsonObj.TryGetProperty("response_code", out var respCode)
                    ? (respCode.GetInt32() == 0 ? "True" : "False")
                    : "False";

                // ✅ Message
                paymentRequestRes.message = jsonObj.TryGetProperty("response_message", out var messageProp)
                    ? messageProp.GetString()
                    : "Unknown error";

                // ✅ TransactionId
                paymentRequestRes.transactionId = request.Invoiceno;
                // ✅ ReferenceId (map urn → refernceId)
                if (action == "status")
                {
                    paymentRequestRes.refernceId = jsonObj.TryGetProperty("invoicenumber", out var invNo)
                                 ? invNo.GetString()
                                 : "";

                }
                else
                {
                    paymentRequestRes.refernceId = jsonObj.TryGetProperty("urn", out var urnProp)
                          ? urnProp.GetString()
                          : "";
                }
            }
            else if (action == "cancel")
            {
                // ✅ Status
                paymentRequestRes.status = jsonObj.TryGetProperty("response_code", out var respCode)
                    ? (respCode.ToString() == "0" ? "True" : "False")
                    : "False";

                // ✅ Message
                paymentRequestRes.message = jsonObj.TryGetProperty("response_message", out var messageProp)
                    ? messageProp.GetString()
                    : "Unknown error";

                // ✅ TransactionId
                paymentRequestRes.transactionId = request.Invoiceno;
            }
            else
            {
                paymentRequestRes.status = "False";
                paymentRequestRes.message = "Unsupported action mapping";
                paymentRequestRes.transactionId = request.Invoiceno;
                paymentRequestRes.refernceId = request.refernceId;
            }

            // Always populate these for consistency
            paymentRequestRes.cardNo = "";
            paymentRequestRes.upi = "";
            paymentRequestRes.paymentMode = request.Txntype;
            paymentRequestRes.originalResponse = finalJson; // store decrypted if available

            return StatusCode((int)response.StatusCode, paymentRequestRes);


        }

        // ------------------ Paytm Pay ------------------
        public async Task<IActionResult> paytmmethod(PaymentRequest request, string action)
        {

            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
            PaymentRequestResponse paymentRequestRes = new PaymentRequestResponse();
            var section = _config.GetSection("PaymentProviders:paytm");

            string MID = section["MID"];
            string MerchantKey = section["MerchantKey"];

            string url = action switch
            {
                "status" => section["StatusUrl"],
                "cancel" => section["CancelUrl"],
                _ => section["InitiateUrl"]
            };

            string txnMode = request.Txntype switch
            {
                "00" => "CARD",
                "01" => "QR",
                "02" => "CASH",
                _ => "UPI"
            };

            string dates = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

            // ✅ Declare map before condition
            Dictionary<string, string> Map = new Dictionary<string, string>();

            if (action == "status")
            {
                // ✅ Status requires only these 4
                Map = new Dictionary<string, string>
        {
            { "paytmMid", MID },
            { "paytmTid", request.Tid },
            { "transactionDateTime", dates },
            { "merchantTransactionId", request.Invoiceno }
        };
            }
            else if (action == "initiate")
            {
                // ✅ Initiate & Cancel require these
                Map = new Dictionary<string, string>
        {
            { "paytmMid", MID },
            { "paytmTid", request.Tid },
            { "transactionDateTime", dates },
            { "merchantTransactionId", request.Invoiceno },
            { "transactionAmount", request.Amount },
            { "paymentMode", txnMode }
        };
            }
            else if (action == "cancel")
            {
                // ✅ Status requires only these 4
                Map = new Dictionary<string, string>
        {
            { "paytmMid", MID },
            { "paytmTid", request.Tid },
            { "transactionDateTime", dates },
            { "merchantTransactionId", request.Invoiceno }
        };
            }

            // ✅ Generate checksum
            string paytmChecksum = Checksum.generateSignature(Map, MerchantKey);

            // ✅ Convert Map to JObject
            JObject Obj = JObject.FromObject(Map);

            // Add merchantExtendedInfo only for initiate/cancel
            if (action != "status")
            {
                JObject merchantExtendedInfo = new JObject
        {
            { "paymentMode", txnMode }
        };
                Obj.Add("merchantExtendedInfo", merchantExtendedInfo);
            }

            // ✅ Final JSON payload
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

            string body = Newtonsoft.Json.JsonConvert.SerializeObject(requestRoot, Newtonsoft.Json.Formatting.None);
            // Console.WriteLine("Request:\n" + body);

            var client = _httpClient.CreateClient();
            var content = new StringContent(body, Encoding.UTF8, "application/json");

            HttpResponseMessage response;
            try
            {
                response = await client.PostAsync(url, content);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = "Failed to send request", details = ex.Message });
            }

            string responseText = await response.Content.ReadAsStringAsync();
            await _logService.WritePaymentLogAsync(request.Invoiceno, "paytmmethod_response_" + action, responseText);



            var jsonObj = JsonSerializer.Deserialize<JsonElement>(responseText);
            // await _logService.WritePaymentLogAsync(request.Invoiceno, "paytmmethod_response_" + action, responseText);

            // Map common fields
            paymentRequestRes.code = (int)response.StatusCode;

            if (action == "initiate" || action == "status")
            {
                // ✅ Status from Paytm → resultStatus (SUCCESS/FAIL/PENDING)
                if (jsonObj.TryGetProperty("body", out var bodyProp) &&
                    bodyProp.TryGetProperty("resultInfo", out var resultInfo))
                {
                    string resultCode = resultInfo.GetProperty("resultCode").GetString() ?? "";
                    if (resultCode == "P" || resultCode == "F")
                    {
                        paymentRequestRes.status = "False";
                    }
                    else
                    {
                        paymentRequestRes.status = "True";
                    }


                        paymentRequestRes.message = resultInfo.TryGetProperty("resultMsg", out var msgProp)
                            ? msgProp.GetString()
                            : "Unknown error";
                }
                else
                {
                    paymentRequestRes.status = "False";
                    paymentRequestRes.message = "Invalid Paytm response structure";
                }

                // ✅ TransactionId
                paymentRequestRes.transactionId = request.Invoiceno;

                // ✅ ReferenceId → use merchantTransactionId from body
                if (action == "status")
                {
                    paymentRequestRes.refernceId = bodyProp.TryGetProperty("acquirementId", out var txnIdProp)
                        ? txnIdProp.GetString()
                        : "";

                    if (request.Txntype == "00")
                    {
                        paymentRequestRes.cardNo = bodyProp.TryGetProperty("lastFourDigitsCard", out var cardNoProp)
                             ? cardNoProp.GetString()
                             : "";
                    }
                    else
                    {
                        paymentRequestRes.cardNo = "";
                    }
                }
                else
                {
                    paymentRequestRes.refernceId = request.Invoiceno;
                }
            }
            else if (action == "cancel")
            {
                // Paytm cancel also comes with resultInfo
                if (jsonObj.TryGetProperty("body", out var bodyProp) &&
                    bodyProp.TryGetProperty("resultInfo", out var resultInfo))
                {
                    string resultStatus = resultInfo.GetProperty("resultStatus").GetString() ?? "FAIL";
                    paymentRequestRes.status = resultStatus.Equals("SUCCESS", StringComparison.OrdinalIgnoreCase) ? "True" : "False";

                    paymentRequestRes.message = resultInfo.TryGetProperty("resultMsg", out var msgProp)
                        ? msgProp.GetString()
                        : "Unknown error";
                }
                else
                {
                    paymentRequestRes.status = "False";
                    paymentRequestRes.message = "Invalid Paytm cancel response";
                }

                paymentRequestRes.transactionId = request.Invoiceno;
            }
            else
            {
                paymentRequestRes.status = "False";
                paymentRequestRes.message = "Unsupported action mapping";
                paymentRequestRes.transactionId = request.Invoiceno;
                paymentRequestRes.refernceId = request.refernceId;
            }

            // Always populate these for consistency
            // paymentRequestRes.cardNo = "";
            paymentRequestRes.upi = "";
            paymentRequestRes.paymentMode = request.Txntype;
            paymentRequestRes.originalResponse = responseText;

            return StatusCode((int)response.StatusCode, paymentRequestRes);
        }
        // ------------------ GenerateMac ------------------
        private string GenerateMac(string amount, string txntype, string clientcode, string saltKey)
        {
            string input = amount + txntype + clientcode + saltKey;

            using (var sha256 = SHA256.Create())
            {
                var hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(input));
                return BitConverter.ToString(hash).Replace("-", "").ToUpper();
            }
        }

    }
}
