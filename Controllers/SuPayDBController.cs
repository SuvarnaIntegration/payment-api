using Azure.Core;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Npgsql.Internal.Postgres;
using Npgsql.Replication.PgOutput;
using PaymentAPI.Models;
using PaymentAPI.Services;
using Paytm;
using System;
using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.NetworkInformation;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using static System.Net.WebRequestMethods;

namespace PaymentAPI.Controllers
{
    [Route("paylink")]
    [ApiController]
    public class SuPayDBController : ControllerBase
    {
        private readonly LogService _logService;
        private readonly IConfiguration _config;
        private readonly IHttpClientFactory _httpClient;
        private readonly string _databaseProvider;
        private readonly PostgresDatabaseService _pgdb;
        private readonly JwtTokenService _jwtService;

        public SuPayDBController(
            LogService logService,
            IConfiguration config,
            IHttpClientFactory httpClientFactory,
            PostgresDatabaseService pgdb,
            JwtTokenService jwtService)
        {
            _logService = logService;
            _config = config;
            _jwtService = jwtService;
            _httpClient = httpClientFactory;
            _pgdb = pgdb;
            _databaseProvider = config["DatabaseProvider"];
        }

        [HttpPost("authtoken")]
        [AllowAnonymous]
        public IActionResult authtoken([FromBody] authtokenprop model)
        {
            var section = _config.GetSection("Jwt");
            string UserName = section["UserName"];
            string Password = section["Password"];

            if (model is null)
                return StatusCode(200, new { code = 200, status = "false", message = "Request body required." });

            // Replace with real user validation
            if (model.UserName == UserName && model.Password == Password)
            {
                var (token, expiry) = _jwtService.GenerateToken(model.UserName);

                return Ok(new
                {
                    code = 200,
                    status = "true",
                    message = "Login successful",
                    token = "Bearer " + token,
                    expiryTime = "15 Mins"
                });
            }

            return StatusCode(200, new
            {
                code = 200,
                status = "False",
                message = "Please check User Name and Password..."
            });
        }

        [HttpPost("pushtrans")]
        [Authorize]
        public async Task<IActionResult> PostPaymenttxn([FromBody] PosPaymentRequest posrequest)
        {
            try
            {
                if (posrequest is null)
                    return Ok(ApiResponseHelper.Error("Request body required."));

                await _logService.WritePaymentLogAsync(
                    $"Push Transaction API: Provider: {posrequest.provider}",
                    $"EDC No: {posrequest.edcsNo}, Transaction No: {posrequest.transactionNo}, PatientId: {posrequest.patientId}",
                    $"Amount: {posrequest.amount}, TxnType: {posrequest.txntype}"
                );

                var missingField =
                    string.IsNullOrWhiteSpace(posrequest.provider) ? "Provider" :
                    string.IsNullOrWhiteSpace(posrequest.amount) ? "Amount" :
                    string.IsNullOrWhiteSpace(posrequest.edcsNo) ? "EDC Machine Serial No" :
                    string.IsNullOrWhiteSpace(posrequest.transactionNo) ? "Transaction No" :
                    string.IsNullOrWhiteSpace(posrequest.txntype) ? "Transaction Type" : null;

                if (missingField != null)
                    return Ok(ApiResponseHelper.Error($"{missingField} is required"));

                // Only Postgres is supported in this build
                if (!_databaseProvider.Equals("postgres", StringComparison.OrdinalIgnoreCase))
                {
                    await _logService.WritePaymentLogAsync("DB Provider check failed", _databaseProvider ?? "null", "");
                    return Ok(ApiResponseHelper.Error("No database connection established."));
                }

                var dbResponse = await _pgdb.GetPaylinkActivePosEdcMachineinfoAsync(posrequest.edcsNo, posrequest.provider);

                if (!dbResponse.Status || dbResponse.Data == null)
                    return Ok(ApiResponseHelper.Error("No matching POS EDC machine configuration found."));

                var machineList = dbResponse.Data as List<PosActiveEdcMachineDetails>;

                if (machineList == null || machineList.Count == 0)
                    return Ok(ApiResponseHelper.Error("No POS EDC machine details found."));

                var machineDetails = machineList.First();

                switch (posrequest.provider?.ToLowerInvariant())
                {
                    case "razorpay":
                        return await razorpaymethod(posrequest, machineDetails, "initiate");
                    case "icici":
                        return await icicipaymethod(posrequest, machineDetails, "initiate");
                    case "hdfc":
                        return await hdfcpaymethod(posrequest, machineDetails, "initiate");
                    case "paytm":
                        return await paytmmethod(posrequest, machineDetails, "initiate");
                    case "worldline":
                        return await worldlinemethod(posrequest, machineDetails, "initiate");
                    case "mswipe":
                        return await MswipePaymentMethod(posrequest, machineDetails, "initiate");
                    case "pinelab":
                        return await pinelabmethod(posrequest, machineDetails, "initiate");
                    case "getepay":
                        return await getepaymethod(posrequest, machineDetails, "initiate");
                    case "phonepe":
                        return await PhonePeMethod(posrequest, machineDetails, "initiate");
                    case "icicipaylink":
                        return await icicipaylinkmethod(posrequest, machineDetails, "initiate");
                    default:
                        return Ok(ApiResponseHelper.Error("Unknown provider."));
                }
            }
            catch (Exception ex)
            {
                await _logService.WritePaymentLogAsync("PushTrans Exception", ex.Message, ex.StackTrace);
                return Ok(ApiResponseHelper.Error("An error occurred: " + ex.Message));
            }
        }

        [HttpPost("transstatus")]
        [Authorize]
        public async Task<IActionResult> PostPaymentstatus([FromBody] PosPaymentRequest posrequest)
        {
            try
            {
                if (posrequest is null)
                    return Ok(ApiResponseHelper.Error("Request body required."));

                await _logService.WritePaymentLogAsync(
                    $"Status API Provider: {posrequest.provider}",
                    $"EDC No {posrequest.edcsNo}, TransactionNo {posrequest.transactionNo}, ReferenceId {posrequest.refernceId}",
                    $"Amount {posrequest.amount}, TxnType {posrequest.txntype}"
                );

                var missingField =
                    string.IsNullOrWhiteSpace(posrequest.provider) ? "Provider" :
                    string.IsNullOrWhiteSpace(posrequest.amount) ? "Amount" :
                    string.IsNullOrWhiteSpace(posrequest.edcsNo) ? "EDC Machine Serial No" :
                    string.IsNullOrWhiteSpace(posrequest.transactionNo) ? "Transaction No" :
                    string.IsNullOrWhiteSpace(posrequest.refernceId) ? "Reference Id" :
                    string.IsNullOrWhiteSpace(posrequest.txntype) ? "Transaction Type" : null;

                if (missingField != null)
                    return Ok(ApiResponseHelper.Error($"{missingField} is required"));

                if (!_databaseProvider.Equals("postgres", StringComparison.OrdinalIgnoreCase))
                {
                    await _logService.WritePaymentLogAsync("DB Provider check failed", _databaseProvider ?? "null", "");
                    return Ok(ApiResponseHelper.Error("No database connection established."));
                }

                var dbResponse = await _pgdb.GetPaylinkActivePosEdcMachineinfoAsync(posrequest.edcsNo, posrequest.provider);

                if (!dbResponse.Status || dbResponse.Data == null)
                    return Ok(ApiResponseHelper.Error("No matching POS EDC machine configuration found."));

                var machineList = dbResponse.Data as List<PosActiveEdcMachineDetails>;

                if (machineList == null || machineList.Count == 0)
                    return Ok(ApiResponseHelper.Error("No POS EDC machine details found."));

                var machineDetails = machineList.First();

                switch (posrequest.provider?.ToLowerInvariant())
                {
                    case "razorpay": return await razorpaymethod(posrequest, machineDetails, "status");
                    case "icici": return await icicipaymethod(posrequest, machineDetails, "status");
                    case "icicipaylink": return await statusicicipaylinkmethod(posrequest, machineDetails, "status");
                    case "hdfc": return await hdfcpaymethod(posrequest, machineDetails, "status");
                    case "paytm": return await paytmmethod(posrequest, machineDetails, "status");
                    case "worldline": return await worldlinemethod(posrequest, machineDetails, "status");
                    case "mswipe": return await MswipePaymentMethod(posrequest, machineDetails, "status");
                    case "pinelab": return await pinelabmethod(posrequest, machineDetails, "status");
                    case "getepay": return await getepaymethod(posrequest, machineDetails, "status");
                    case "phonepe": return await PhonePeMethod(posrequest, machineDetails, "status");
                    default:
                        return Ok(ApiResponseHelper.Error("Unknown provider."));
                }
            }
            catch (Exception ex)
            {
                await _logService.WritePaymentLogAsync("TransStatus Exception", ex.Message, ex.StackTrace);
                return Ok(ApiResponseHelper.Error("An error occurred: " + ex.Message));
            }
        }

        [HttpPost("canceltrans")]
        [Authorize]
        public async Task<IActionResult> PostPaymentcancel([FromBody] PosPaymentRequest posrequest)
        {
            try
            {
                if (posrequest is null)
                    return Ok(ApiResponseHelper.Error("Request body required."));

                await _logService.WritePaymentLogAsync(
                    $"Cancel Transaction API Provider: {posrequest.provider}",
                    $"EDC No {posrequest.edcsNo}, TransactionNo {posrequest.transactionNo}, ReferenceId {posrequest.refernceId}",
                    $"Amount {posrequest.amount}, TxnType {posrequest.txntype}"
                );

                var missingField =
                    string.IsNullOrWhiteSpace(posrequest.provider) ? "Provider" :
                    string.IsNullOrWhiteSpace(posrequest.amount) ? "Amount" :
                    string.IsNullOrWhiteSpace(posrequest.edcsNo) ? "EDC Machine Serial No" :
                    string.IsNullOrWhiteSpace(posrequest.transactionNo) ? "Transaction No" :
                    string.IsNullOrWhiteSpace(posrequest.refernceId) ? "Reference Id" :
                    string.IsNullOrWhiteSpace(posrequest.txntype) ? "Transaction Type" : null;

                if (missingField != null)
                    return Ok(ApiResponseHelper.Error($"{missingField} is required"));

                if (!_databaseProvider.Equals("postgres", StringComparison.OrdinalIgnoreCase))
                {
                    await _logService.WritePaymentLogAsync("DB Provider check failed", _databaseProvider ?? "null", "");
                    return Ok(ApiResponseHelper.Error("No database connection established."));
                }

                var dbResponse = await _pgdb.GetPaylinkActivePosEdcMachineinfoAsync(posrequest.edcsNo, posrequest.provider);
                await _logService.WritePaymentLogAsync("DB FN: fn_get_pos_edc_machine_details", posrequest.edcsNo, posrequest.provider);

                if (!dbResponse.Status || dbResponse.Data == null)
                    return Ok(ApiResponseHelper.Error("No matching POS EDC machine configuration found."));

                var machineList = dbResponse.Data as List<PosActiveEdcMachineDetails>;

                if (machineList == null || machineList.Count == 0)
                    return Ok(ApiResponseHelper.Error("No POS EDC machine details found."));

                var machineDetails = machineList.First();

                switch (posrequest.provider?.ToLowerInvariant())
                {
                    case "razorpay": return await razorpaymethod(posrequest, machineDetails, "cancel");
                    case "icici": return await icicipaymethod(posrequest, machineDetails, "cancel");
                    case "hdfc": return await hdfcpaymethod(posrequest, machineDetails, "cancel");
                    case "paytm": return await paytmmethod(posrequest, machineDetails, "cancel");
                    case "worldline": return await worldlinemethod(posrequest, machineDetails, "cancel");
                    case "mswipe": return await MswipePaymentMethod(posrequest, machineDetails, "cancel");
                    case "pinelab": return await pinelabmethod(posrequest, machineDetails, "cancel");
                    case "getepay": return await getepaymethod(posrequest, machineDetails, "cancel");
                    case "phonepe": return await PhonePeMethod(posrequest, machineDetails, "cancel");
                    default:
                        return Ok(ApiResponseHelper.Error("Unknown provider."));
                }
            }
            catch (Exception ex)
            {
                await _logService.WritePaymentLogAsync("CancelTrans Exception", ex.Message, ex.StackTrace);
                return Ok(ApiResponseHelper.Error("An error occurred: " + ex.Message));
            }
        }



        [HttpPost("callback")]
        public async Task<IActionResult> PaymentCallback()
        {
            try
            {
                // 🔥 Step 1: Read raw callback data
                string rawData = "";
                using (var reader = new StreamReader(Request.Body))
                {
                    rawData = await reader.ReadToEndAsync();
                }

                // fallback (important)
                if (string.IsNullOrWhiteSpace(rawData) && Request.HasFormContentType)
                {
                    rawData = string.Join("&", Request.Form.Keys
                        .Select(k => $"{k}={Request.Form[k]}"));
                }

                await _logService.WritePaymentLogAsync("Callback Raw Data", rawData, "");

                // 🔥 Step 2: Parse callback data
                var parsedData = System.Web.HttpUtility.ParseQueryString(rawData);

                string invoiceNo = parsedData["invoiceNo"]?.Trim();
                string aggregatorId = parsedData["aggregatorID"]?.Trim();
                string merchantId = parsedData["merchantId"]?.Trim();
                string txnId = parsedData["txnID"];
                string amount = parsedData["amount"];
                string paymentMode = parsedData["paymentMode"];

                // 🔥 Step 3: Get machine details
                var dbResponse = await _pgdb.GetPaylinkActivePosEdcMachineinfoAsync(aggregatorId, "icicipaylink");

                if (!dbResponse.Status || dbResponse.Data == null)
                    return Ok("No configuration found");

                var machineDetails = (dbResponse.Data as List<PosActiveEdcMachineDetails>)?.FirstOrDefault();

                if (machineDetails == null)
                    return Ok("No machine details found");

                // 🔥 Step 4: Prepare params (Hash V1)
                var statusParams = new Dictionary<string, string>
                {
                    ["invoiceNo"] = invoiceNo,
                    ["merchantId"] = merchantId,
                    ["reqType"] = "status"
                };

                // 🔐 Step 5: Generate secure hash
                string secureHash = HashV1.Compute(statusParams, machineDetails.IciciPaylinkSecretKey);

                // 🔍 Debug (optional)
                string debugString = string.Concat(statusParams.OrderBy(x => x.Key).Select(x => x.Value));
                await _logService.WritePaymentLogAsync("Hash Input String", debugString, "");
                await _logService.WritePaymentLogAsync("Generated Hash", secureHash, "");

                // 🔥 Step 6: Build request
                var statusRequest = new InvoiceStatusRequest
                {
                    MerchantId = merchantId,
                    InvoiceNo = invoiceNo,
                    ReqType = "status",
                    SecureHash = secureHash
                };

                var jsonPayload = System.Text.Json.JsonSerializer.Serialize(statusRequest, new JsonSerializerOptions
                {
                    PropertyNamingPolicy = null
                });

                // 🔥 Step 7: Call ICICI Status API
                string statusUrl = machineDetails.PosInitiateUrl;

                var client = _httpClient.CreateClient();
                client.DefaultRequestHeaders.Clear();
                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

                var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

                var response = await client.PostAsync(statusUrl, content);
                var responseString = await response.Content.ReadAsStringAsync();

                await _logService.WritePaymentLogAsync("ICICI Status API Response", responseString, "");

                // 🔥 Step 8: Parse response
                var jsonObj = System.Text.Json.JsonSerializer.Deserialize<JsonElement>(responseString);

                // 🔥 Step 9: Prepare final response
                var paymentRequestRes = new PaymentRequestResponse();

                paymentRequestRes.code = 200;
                paymentRequestRes.status = "False";
                paymentRequestRes.message = "Transaction Failed";
                paymentRequestRes.transactionId = invoiceNo;
                paymentRequestRes.refernceId = null;
                paymentRequestRes.Orginalcode = "F";
                paymentRequestRes.OrginalMessage = "";
                paymentRequestRes.RRN = "";
                paymentRequestRes.cardNo = "";
                paymentRequestRes.upi = "";
                paymentRequestRes.paymentMode = paymentMode ?? "00";
                paymentRequestRes.QrCodeurl = "";
                paymentRequestRes.PaymentUrl = "";
                paymentRequestRes.originalResponse = responseString;

                // 🔥 Extract fields
                string responseCode = jsonObj.TryGetProperty("responseCode", out var codeProp)
                    ? codeProp.GetString()
                    : "";

                string respMessage = jsonObj.TryGetProperty("respDescription", out var descProp)
                    ? descProp.GetString()
                    : "";

                string txnIDFromICICI = jsonObj.TryGetProperty("txnID", out var txnProp)
                    ? txnProp.GetString()
                    : null;

                // 🔥 FINAL LOGIC (based on responseCode)
                if (responseCode == "0000")
                {
                    paymentRequestRes.status = "True";
                    paymentRequestRes.message = respMessage;
                    paymentRequestRes.Orginalcode = "S";
                }
                else
                {
                    paymentRequestRes.status = "False";
                    paymentRequestRes.message = respMessage;
                    paymentRequestRes.Orginalcode = "F";
                }

                // 🔥 Reference mapping
                paymentRequestRes.refernceId = txnId;
                paymentRequestRes.RRN = invoiceNo;

                // 🔥 Step 10: Save DB
                var posrequest = new PosPaymentRequest
                {
                    provider = "icicipaylink",
                    edcsNo = aggregatorId,
                    refernceId = txnId,
                    transactionNo = invoiceNo,
                    amount = amount,
                    txntype = paymentMode
                };

                await _pgdb.UpdateStatusPaylinkPosEdcMachineTransactionLogAsync(
                    posrequest,
                    jsonPayload,
                    responseString
                );

                // 🔥 Step 11: Return final response
                return Ok(paymentRequestRes);
            }
            catch (Exception ex)
            {
                await _logService.WritePaymentLogAsync("Callback Exception", ex.Message, ex.StackTrace);

                return Ok(new PaymentRequestResponse
                {
                    code = 500,
                    status = "False",
                    message = "Exception occurred",
                    originalResponse = ex.Message
                });
            }
        }
        // ------------------ Razor Pay ------------------

        private async Task<IActionResult> razorpaymethod(PosPaymentRequest posrequest, PosActiveEdcMachineDetails machineDetails, string action)
        {
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
            PaymentRequestResponse paymentRequestRes = new PaymentRequestResponse();
            string username = machineDetails.RazorpayUsername;
            string appKey = machineDetails.RazorpayAppKey;

            string url = action switch
            {
                "status" => machineDetails.PosStatusUrl,
                "cancel" => machineDetails.PosCancelUrl,
                _ => machineDetails.PosInitiateUrl // default = pay
            };

            // decide mode based on Txntype
            string txnMode = posrequest.txntype switch
            {
                "00" => "CARD",
                "01" => "UPI",
                "02" => "CASH",
                "03" => "BHARATQR",
                _ => "UPI"
            };

            object payload;

            // ✅ Build payload based on action
            if (action == "initiate")
            {
                payload = new
                {
                    username = username,
                    appKey = appKey,
                    amount = posrequest.amount,
                    customerMobileNumber = posrequest.mobileNumber,
                    externalRefNumber = posrequest.transactionNo,
                    externalRefNumber2 = posrequest.patientId,
                    externalRefNumber3 = posrequest.customerName,
                    externalRefNumber4 = posrequest.createby,
                    externalRefNumber5 = machineDetails.PosedcTerminalId,
                    pushTo = new
                    {
                        deviceId = machineDetails.PosedcTerminalId + "|ezetap_android"
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
                    origP2pRequestId = posrequest.refernceId
                };
            }
            else if (action == "cancel")
            {
                payload = new
                {
                    username = username,
                    appKey = appKey,
                    origP2pRequestId = posrequest.refernceId,
                    pushTo = new
                    {
                        deviceId = machineDetails.PosedcTerminalId + "|ezetap_android"
                    }
                };
            }
            else
            {
                // fallback default payload
                payload = new { username, appKey };
            }

            var jsonPayload = System.Text.Json.JsonSerializer.Serialize(payload);

            var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");
            var client = _httpClient.CreateClient();
            var response = await client.PostAsync(url, content);
            var responseString = await response.Content.ReadAsStringAsync();

            paymentRequestRes.upi = "";
            paymentRequestRes.cardNo = "";
            paymentRequestRes.QrCodeurl = "";
            paymentRequestRes.RRN = "";
            paymentRequestRes.OrginalMessage = "";
            paymentRequestRes.Orginalcode = "";
            paymentRequestRes.PaymentUrl = "";
            paymentRequestRes.paymentMode = posrequest.txntype;
            // ✅ Deserialize JSON into a dynamic object
            var jsonObj = System.Text.Json.JsonSerializer.Deserialize<JsonElement>(responseString);

            // Map response safely
            paymentRequestRes.code = 200;

            if (action == "initiate" || action == "cancel")
            {
                bool apiSuccess = jsonObj.TryGetProperty("success", out var succProp) && succProp.GetBoolean();

                if (!apiSuccess)
                {
                    // FAILED initiation
                    paymentRequestRes.status = "False";

                    // Prefer errorMessage; fallback message
                    string errMsg =
                        jsonObj.TryGetProperty("errorMessage", out var err1) ? err1.GetString() :
                        jsonObj.TryGetProperty("message", out var err2) ? err2.GetString() :
                        "Unable to initiate payment.";

                    paymentRequestRes.message = errMsg;
                    paymentRequestRes.OrginalMessage = errMsg;

                    // error code → OriginalCode
                    paymentRequestRes.Orginalcode =
                        jsonObj.TryGetProperty("errorCode", out var ecodeProp)
                        ? ecodeProp.GetString()
                        : "INITIATE_FAILED";

                    paymentRequestRes.refernceId = "";    // No p2pRequestId
                }
                else
                {
                    // SUCCESS initiation
                    paymentRequestRes.status = "True";

                    paymentRequestRes.refernceId =
                        jsonObj.TryGetProperty("p2pRequestId", out var p2pProp)
                        ? p2pProp.GetString()
                        : "";
                    if (action == "cancel")
                        paymentRequestRes.message = "Request Cancelled";
                    if (action == "initiate")
                        paymentRequestRes.message = "Initiated Successfully";
                    paymentRequestRes.OrginalMessage = paymentRequestRes.message;

                    // success → no error code
                    paymentRequestRes.Orginalcode = "0";  // you may set to "" also

                }

                // RRN always empty for INITIATE API
                paymentRequestRes.RRN = "";

                // TransactionId from your request
                paymentRequestRes.transactionId = posrequest.transactionNo;
            }

            if (action == "status")
            {
                // Default
                paymentRequestRes.refernceId = "";
                paymentRequestRes.RRN = "";
                paymentRequestRes.cardNo = "";
                paymentRequestRes.upi = "";

                // 1️⃣ Extract messageCode
                string messageCode = jsonObj.TryGetProperty("messageCode", out var msgCodeProp)
                                     ? msgCodeProp.GetString()
                                     : "";

                // 2️⃣ Determine final status
                paymentRequestRes.status = (messageCode == "P2P_DEVICE_TXN_DONE") ? "True" : "False";

                // 3️⃣ Store original code + message
                paymentRequestRes.Orginalcode = messageCode;

                paymentRequestRes.message =
                    jsonObj.TryGetProperty("message", out var msgProp) && !string.IsNullOrEmpty(msgProp.GetString())
                    ? msgProp.GetString()
                    : messageCode;

                paymentRequestRes.OrginalMessage = paymentRequestRes.message;

                // 4️⃣ Extract RRN (root-level)
                if (jsonObj.TryGetProperty("rrNumber", out var rrnProp))
                    paymentRequestRes.RRN = rrnProp.GetString();

                // 5️⃣ Extract TxnId (root-level)
                if (jsonObj.TryGetProperty("txnId", out var txnIdProp))
                    paymentRequestRes.refernceId = txnIdProp.GetString();

                // 6️⃣ Extract card number
                if (jsonObj.TryGetProperty("formattedPan", out var panProp))
                    paymentRequestRes.cardNo = panProp.GetString();
                else if (jsonObj.TryGetProperty("cardLastFourDigit", out var last4Prop))
                    paymentRequestRes.cardNo = last4Prop.GetString();

                // 7️⃣ Extract UPI VPA
                if (jsonObj.TryGetProperty("payerName", out var payerProp))
                    paymentRequestRes.upi = payerProp.GetString();


                paymentRequestRes.paymentMode = posrequest.txntype;

                // 9️⃣ transactionId from your request
                paymentRequestRes.transactionId = posrequest.transactionNo;
            }

            // 🔹 Keep the original JSON for debugging/logging
            paymentRequestRes.originalResponse = responseString;

            OperationResult result;
            if (action == "initiate")
                result = await _pgdb.InsertPaylinkPosEdcMachineTransactionLogAsync(posrequest, action, jsonPayload, responseString, "");
            else if (action == "status")
                result = await _pgdb.UpdateStatusPaylinkPosEdcMachineTransactionLogAsync(posrequest, jsonPayload, responseString);
            else if (action == "cancel")
                result = await _pgdb.UpdateCancelPaylinkPosEdcMachineTransactionLogAsync(posrequest, jsonPayload, responseString);

            return StatusCode(200, paymentRequestRes);
        }

        // ------------------ icici Pay ------------------
        private async Task<IActionResult> icicipaymethod(PosPaymentRequest posrequest, PosActiveEdcMachineDetails machineDetails, string action)
        {
            // Force TLS 1.2 (important for ICICI endpoint)
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;

            PaymentRequestResponse paymentRequestRes = new PaymentRequestResponse();

            string mid = machineDetails.IciciMid;
            string source_id = machineDetails.IciciSourceId;
            string erp_client_id = machineDetails.IciciErpClientId;

            string url = action switch
            {
                "status" => machineDetails.PosStatusUrl,
                "cancel" => machineDetails.PosCancelUrl,
                _ => machineDetails.PosInitiateUrl // default = initiate/pay
            };

            // decide mode based on Txntype
            string txnMode = posrequest.txntype switch
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
                    source_id = source_id,
                    erp_client_id = erp_client_id,
                    erp_tran_id = erp_tran_id,
                    tid = machineDetails.PosedcTerminalId,
                    tran_type = Convert.ToInt32(txnMode),
                    amount = posrequest.amount,
                    bill_no = posrequest.transactionNo,
                    tip = "0.00"
                };
            }
            else if (action == "status" || action == "cancel")
            {
                payload = new
                {
                    mid = mid,
                    source_id = source_id,
                    erp_client_id = erp_client_id,
                    erp_tran_id = posrequest.refernceId,
                    tid = machineDetails.PosedcTerminalId,
                    tran_type = Convert.ToInt32(txnMode),
                    bill_no = posrequest.transactionNo
                };
            }
            else
            {
                payload = new { mid = mid, tid = machineDetails.PosedcTerminalId }; // fallback
            }

            // ✅ Ensure exact casing (snake_case preserved in payload)
            var jsonPayload = System.Text.Json.JsonSerializer.Serialize(payload, new JsonSerializerOptions
            {
                PropertyNamingPolicy = null
            });
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
            paymentRequestRes.code = 200;

            try
            {
                // Check content type → if not JSON, return raw response
                if (response.Content.Headers.ContentType?.MediaType != "application/json")
                {
                    paymentRequestRes.status = "Failed";
                    paymentRequestRes.message = "Non-JSON response from ICICI";
                    return StatusCode(200, paymentRequestRes);
                }
                paymentRequestRes.upi = "";
                paymentRequestRes.cardNo = "";
                paymentRequestRes.QrCodeurl = "";
                paymentRequestRes.PaymentUrl = "";

                // ✅ Parse JSON safely
                var jsonObj = System.Text.Json.JsonSerializer.Deserialize<JsonElement>(responseString);

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
                    : posrequest.transactionNo;

                // Reference ID
                paymentRequestRes.refernceId = jsonObj.TryGetProperty("Tran_Id", out var tranIdProp)
                    ? tranIdProp.GetString()
                    : erp_tran_id;

                // Optional mappings
                paymentRequestRes.paymentMode = posrequest.txntype;
            }
            catch (System.Text.Json.JsonException ex)
            {
                // JSON parse error
                paymentRequestRes.status = "Failed";
                paymentRequestRes.message = $"Invalid JSON response: {ex.Message}";
            }
            string apiStatus = "";
            OperationResult result;
            if (action == "initiate")
                result = await _pgdb.InsertPaylinkPosEdcMachineTransactionLogAsync(posrequest, action, jsonPayload, responseString, apiStatus);
            else if (action == "status")
                result = await _pgdb.UpdateStatusPaylinkPosEdcMachineTransactionLogAsync(posrequest, jsonPayload, responseString);
            else if (action == "cancel")
                result = await _pgdb.UpdateCancelPaylinkPosEdcMachineTransactionLogAsync(posrequest, jsonPayload, responseString);


            return StatusCode(200, paymentRequestRes);
        }

        // ------------------ icici paylink ------------------
        //private async Task<IActionResult> icicipaylinkmethod( PosPaymentRequest posrequest, PosActiveEdcMachineDetails machineDetails, string action)
        //{
        //    ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
        //    PaymentRequestResponse paymentRequestRes = new PaymentRequestResponse();

        //    string url = machineDetails.PosInitiateUrl;
        //    string secretkey = machineDetails.IciciPaylinkSecretKey;
        //    string mid = machineDetails.IciciPaylinkMid;

        //    string erp_tran_id = DateTime.Now.ToString("yyMMddHHmmssffff");
        //  //  string paylinkdueDate = DateTime.Now.AddDays(1).ToString("dd/MM/yyyy");
        //    string paylinkdueDate = DateTime.Now.AddDays(1).ToString("dd/MM/yyyy", System.Globalization.CultureInfo.InvariantCulture);

        //    // ✅ STEP 1: Create payload EXACT like Postman
        //    var payload = new
        //    {
        //        addlParam1 = posrequest.customerName,
        //        addlParam2 = posrequest.patientId,
        //        aggregatorId = machineDetails.PosEdcSno,
        //        chargeAmount = posrequest.amount,
        //        chargeHead1 = posrequest.amount,
        //        currencyCode = "356",
        //        desc = "Payment Invoice Details",
        //        dueDate = paylinkdueDate,
        //        emailID = posrequest.refernceId,
        //        invoiceNo = erp_tran_id,
        //        merchantId = mid,
        //        mobileNo = posrequest.mobileNumber,
        //        paymentReturnURL = "https://webhook.site/61420e50-1e90-49ee-9f25-83b03454737f", // change if needed                
        //        userName = posrequest.createby
        //    };

        //    // ✅ STEP 2: Convert to MINIFIED JSON (VERY IMPORTANT)
        //    var jsonPayload = System.Text.Json.JsonSerializer.Serialize(payload, new JsonSerializerOptions
        //    {
        //        PropertyNamingPolicy = null,
        //        WriteIndented = false
        //    });

        //    // ✅ STEP 3: Generate HASH from JSON string
        //    string secureHash = HashV2.ComputeFromString(jsonPayload, secretkey);

        //    // ✅ STEP 4: Prepare request
        //    var client = _httpClient.CreateClient();

        //    client.DefaultRequestHeaders.Clear();
        //    client.DefaultRequestHeaders.Add("securehash", secureHash); // 🔥 HEADER (IMPORTANT)
        //    client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        //    var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

        //    // ✅ STEP 5: Send request
        //    var response = await client.PostAsync(url, content);
        //    var responseString = await response.Content.ReadAsStringAsync();

        //    // ✅ RESPONSE HANDLING


        //    paymentRequestRes.originalResponse = responseString;
        //    paymentRequestRes.code = 200;

        //    try
        //    {
        //        // Check content type → if not JSON, return raw response
        //        if (response.Content.Headers.ContentType?.MediaType != "application/json")
        //        {
        //            paymentRequestRes.status = "Failed";
        //            paymentRequestRes.message = "Non-JSON response from ICICI";
        //            return StatusCode(200, paymentRequestRes);
        //        }

        //        paymentRequestRes.upi = "";
        //        paymentRequestRes.cardNo = "";
        //        paymentRequestRes.QrCodeurl = "";
        //        paymentRequestRes.PaymentUrl = "";

        //        // ✅ Parse JSON safely
        //        var jsonObj = System.Text.Json.JsonSerializer.Deserialize<JsonElement>(responseString);

        //        // Default values
        //        paymentRequestRes.status = "False";
        //        string respCode = "";
        //        string respMsg = "";

        //        // ✅ Correct field names (IMPORTANT)
        //        if (jsonObj.TryGetProperty("responseCode", out var codeProp))
        //        {
        //            respCode = codeProp.GetString();
        //        }

        //        if (jsonObj.TryGetProperty("respDescription", out var descProp))
        //        {
        //            respMsg = descProp.GetString();
        //        }

        //        // ✅ Status mapping
        //        // ICICI success cases:
        //        // 405 = Invoice already exists (still usable)
        //        // 00  = success (in some APIs)
        //        if (respCode == "000" || respCode == "405")
        //        {
        //            paymentRequestRes.status = "True";
        //        }
        //        else
        //        {
        //            paymentRequestRes.status = "False";
        //        }

        //        // Message
        //        paymentRequestRes.message = string.IsNullOrEmpty(respMsg) ? "Unknown error" : respMsg;

        //        // Transaction ID
        //        paymentRequestRes.transactionId = jsonObj.TryGetProperty("txnID", out var txnProp)? txnProp.GetString(): posrequest.transactionNo;

        //        // Reference ID (invoiceNo)
        //        paymentRequestRes.refernceId = jsonObj.TryGetProperty("invoiceNo", out var invProp)? invProp.GetString(): erp_tran_id;

        //        // ✅ Payment URL (important for success)
        //        if (jsonObj.TryGetProperty("redirectionURL", out var urlProp))
        //        {
        //            paymentRequestRes.PaymentUrl = urlProp.GetString();
        //        }

        //        string apiStatus = "";
        //        OperationResult result;
        //        if (action == "initiate")
        //            result = await _pgdb.InsertPaylinkPosEdcMachineTransactionLogAsync(posrequest, action, jsonPayload, responseString, apiStatus);
        //        else if (action == "status")
        //            result = await _pgdb.UpdateStatusPaylinkPosEdcMachineTransactionLogAsync(posrequest, jsonPayload, responseString);
        //        else if (action == "cancel")
        //            result = await _pgdb.UpdateCancelPaylinkPosEdcMachineTransactionLogAsync(posrequest, jsonPayload, responseString);



        //    }
        //    catch (System.Text.Json.JsonException ex)
        //    {
        //        paymentRequestRes.status = "Failed";
        //        paymentRequestRes.message = $"Invalid JSON response: {ex.Message}";
        //    }

        //    return StatusCode(200, paymentRequestRes);
        //}
        ////public static string ComputeFromString(string data, string key)
        ////{
        ////    // Step 1: minified JSON
        ////    string json = System.Text.Json.JsonSerializer.Serialize(data, _minifyOptions);

        ////    Console.WriteLine($"[HashV2 Step 1] Minified JSON:\n  {json}");

        ////    // Steps 2-3: HMAC-SHA256 → lowercase hex
        ////    string result = HmacSha256Hex(json, key);

        ////    Console.WriteLine($"[HashV2 Step 4] Lowercase hex:\n  {result}");
        ////    return result;
        ////}
        ////private static string HmacSha256Hex(string data, string key)
        ////{
        ////    byte[] keyBytes = Encoding.UTF8.GetBytes(key);
        ////    byte[] msgBytes = Encoding.UTF8.GetBytes(data);
        ////    using var hmac = new HMACSHA256(keyBytes);
        ////    byte[] hash = hmac.ComputeHash(msgBytes);
        ////    return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
        ////}
        ////private static readonly JsonSerializerOptions _minifyOptions = new()
        ////{
        ////    WriteIndented = false,
        ////    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        ////};
        ////

        private async Task<IActionResult> icicipaylinkmethod(PosPaymentRequest posrequest, PosActiveEdcMachineDetails machineDetails, string action)
        {

            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
            PaymentRequestResponse paymentRequestRes = new PaymentRequestResponse();

            string url = machineDetails.PosInitiateUrl;
            string secretkey = machineDetails.IciciPaylinkSecretKey;
            string mid = machineDetails.IciciPaylinkMid;

            string erp_tran_id = DateTime.Now.ToString("yyMMddHHmmssffff");
            string paylinkdueDate = DateTime.Now.AddDays(1).ToString("dd/MM/yyyy", System.Globalization.CultureInfo.InvariantCulture);
            // posrequest.transactionNo = posrequest.transactionNo + '_' + erp_tran_id;
            var payload = new
            {
                addlParam1 = posrequest.customerName,
                addlParam2 = posrequest.patientId,
                aggregatorId = machineDetails.PosEdcSno,
                chargeAmount = posrequest.amount,
                chargeHead1 = posrequest.amount,
                currencyCode = "356",
                desc = "Payment Invoice Details",
                dueDate = paylinkdueDate,
                emailID = posrequest.refernceId,
                invoiceNo = posrequest.transactionNo, //erp_tran_id,
                merchantId = mid,
                mobileNo = posrequest.mobileNumber,
                paymentReturnURL = "https://paylink.doctor9.com/supaylink/paylink/callback",
                userName = posrequest.createby
            };

            var jsonPayload = System.Text.Json.JsonSerializer.Serialize(payload, new JsonSerializerOptions
            {
                PropertyNamingPolicy = null,
                WriteIndented = false
            });

            string secureHash = HashV2.ComputeFromString(jsonPayload, secretkey);

            var client = _httpClient.CreateClient();

            client.DefaultRequestHeaders.Clear();
            client.DefaultRequestHeaders.Add("securehash", secureHash);
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

            var response = await client.PostAsync(url, content);
            var responseString = await response.Content.ReadAsStringAsync();

            paymentRequestRes.originalResponse = responseString;
            paymentRequestRes.code = 200;

            try
            {
                paymentRequestRes.upi = "";
                paymentRequestRes.cardNo = "";
                paymentRequestRes.QrCodeurl = "";
                paymentRequestRes.PaymentUrl = "";

                var jsonObj = System.Text.Json.JsonSerializer.Deserialize<JsonElement>(responseString);

                // ✅ FIX: responseCode = "0000" (not "00")
                paymentRequestRes.status = "False";

                if (jsonObj.TryGetProperty("responseCode", out var codeProp))
                {
                    var respCode = codeProp.GetString();
                    paymentRequestRes.status = respCode == "0000" ? "True" : "False";
                }

                paymentRequestRes.message = jsonObj.TryGetProperty("respDescription", out var descProp)
                    ? descProp.GetString()
                    : "Unknown error";

                paymentRequestRes.transactionId = jsonObj.TryGetProperty("invoiceNo", out var invProp)
                    ? invProp.GetString()
                    : posrequest.transactionNo;

                paymentRequestRes.refernceId = jsonObj.TryGetProperty("txnID", out var txnProp)
                    ? txnProp.GetString()
                    : erp_tran_id;

                paymentRequestRes.paymentMode = posrequest.txntype;

                string apiStatus = "";
                OperationResult result;

                if (action == "initiate")
                    result = await _pgdb.InsertPaylinkPosEdcMachineTransactionLogAsync(posrequest, action, jsonPayload, responseString, apiStatus);
                else if (action == "status")
                    result = await _pgdb.UpdateStatusPaylinkPosEdcMachineTransactionLogAsync(posrequest, jsonPayload, responseString);
                else if (action == "cancel")
                    result = await _pgdb.UpdateCancelPaylinkPosEdcMachineTransactionLogAsync(posrequest, jsonPayload, responseString);
            }
            catch (System.Text.Json.JsonException ex)
            {
                paymentRequestRes.status = "Failed";
                paymentRequestRes.message = $"Invalid JSON response: {ex.Message}";
            }

            return StatusCode(200, paymentRequestRes);
        }

        private async Task<IActionResult> statusicicipaylinkmethod(PosPaymentRequest posrequest, PosActiveEdcMachineDetails machineDetails, string action)
        {
            try
            {

                ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;

                // 🔥 Step 4: Prepare params (Hash V1)
                var statusParams = new Dictionary<string, string>
                {
                    ["invoiceNo"] = posrequest.transactionNo,
                    ["merchantId"] = machineDetails.IciciPaylinkMid,
                    ["reqType"] = "status"
                };

                // 🔐 Step 5: Generate secure hash
                string secureHash = HashV1.Compute(statusParams, machineDetails.IciciPaylinkSecretKey);

                // 🔍 Debug (optional)
                string debugString = string.Concat(statusParams.OrderBy(x => x.Key).Select(x => x.Value));
                await _logService.WritePaymentLogAsync("Hash Input String", debugString, "");
                await _logService.WritePaymentLogAsync("Generated Hash", secureHash, "");

                // 🔥 Step 6: Build request
                var statusRequest = new InvoiceStatusRequest
                {
                    MerchantId = machineDetails.IciciPaylinkMid,
                    InvoiceNo = posrequest.transactionNo,
                    ReqType = "status",
                    SecureHash = secureHash
                };

                var jsonPayload = System.Text.Json.JsonSerializer.Serialize(statusRequest, new JsonSerializerOptions
                {
                    PropertyNamingPolicy = null
                });

                // 🔥 Step 7: Call ICICI Status API
                string statusUrl = machineDetails.PosStatusUrl;

                var client = _httpClient.CreateClient();
                client.DefaultRequestHeaders.Clear();
                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

                var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

                var response = await client.PostAsync(statusUrl, content);
                var responseString = await response.Content.ReadAsStringAsync();

                await _logService.WritePaymentLogAsync("ICICI Status API Response", responseString, "");

                // 🔥 Step 8: Parse response
                var jsonObj = System.Text.Json.JsonSerializer.Deserialize<JsonElement>(responseString);

                // 🔥 Step 9: Prepare final response
                var paymentRequestRes = new PaymentRequestResponse();

                paymentRequestRes.code = 200;
                paymentRequestRes.status = "False";
                paymentRequestRes.message = "Transaction Failed";
                paymentRequestRes.transactionId = posrequest.transactionNo;
                paymentRequestRes.refernceId = null;
                paymentRequestRes.Orginalcode = "F";
                paymentRequestRes.OrginalMessage = "";
                paymentRequestRes.RRN = "";
                paymentRequestRes.cardNo = "";
                paymentRequestRes.upi = "";
                paymentRequestRes.paymentMode = posrequest.txntype ?? "04";
                paymentRequestRes.QrCodeurl = "";
                paymentRequestRes.PaymentUrl = "";
                paymentRequestRes.originalResponse = responseString;


                string responseCode = jsonObj.TryGetProperty("responseCode", out var codeProp)
                   ? codeProp.GetString()
                   : "";

                string respMessage = jsonObj.TryGetProperty("respDescription", out var descProp)
                    ? descProp.GetString()
                    : "";

                string txnIDFromICICI = jsonObj.TryGetProperty("txnID", out var txnProp)
                    ? txnProp.GetString()
                    : null;
                string txtinvoiceno=jsonObj.TryGetProperty("invoiceNo", out var invoiceNoprop)
                   ? invoiceNoprop.GetString()
                   : "";

                // 🔥 FINAL LOGIC (based on responseCode)
                if (responseCode == "0000")
                {
                    paymentRequestRes.status = "True";
                    paymentRequestRes.message = respMessage;
                    paymentRequestRes.Orginalcode = "S";
                }
                else
                {
                    paymentRequestRes.status = "False";
                    paymentRequestRes.message = respMessage;
                    paymentRequestRes.Orginalcode = "F";
                }

                // 🔥 Reference mapping
                paymentRequestRes.refernceId = txnIDFromICICI;
                paymentRequestRes.RRN = txtinvoiceno;


                // 🔥 Step 10: Save DB
                var posrequest1 = new PosPaymentRequest
                {
                    provider = "icicipaylink",
                    edcsNo = posrequest.edcsNo,
                    refernceId = posrequest.transactionNo,
                    transactionNo = posrequest.transactionNo,
                    amount = posrequest.amount,
                    txntype = posrequest.txntype
                };

                await _pgdb.UpdateStatusPaylinkPosEdcMachineTransactionLogAsync(
                    posrequest,
                    jsonPayload,
                    responseString
                );

                // 🔥 Step 11: Return final response
                return Ok(paymentRequestRes);
            }
            catch (Exception ex)
            {
                await _logService.WritePaymentLogAsync("Callback Exception", ex.Message, ex.StackTrace);

                return Ok(new PaymentRequestResponse
                {
                    code = 500,
                    status = "False",
                    message = "Exception occurred",
                    originalResponse = ex.Message
                });
            }
        }


        // ------------------ hdfc Pay ------------------
        private async Task<IActionResult> hdfcpaymethod(PosPaymentRequest posrequest, PosActiveEdcMachineDetails machineDetails, string action)
        {
            // Force TLS 1.2 (important for ICICI endpoint)
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;

            PaymentRequestResponse paymentRequestRes = new PaymentRequestResponse();
            string erp_client_id = machineDetails.HdfcErpClientId;
            string source_id = machineDetails.HdfcSourceId;
            string mid = machineDetails.HdfcMid;

            string url = action switch
            {
                "status" => machineDetails.PosStatusUrl,
                "cancel" => machineDetails.PosCancelUrl,
                _ => machineDetails.PosInitiateUrl // default = initiate/pay
            };

            // decide mode based on Txntype
            string txnMode = posrequest.txntype switch
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
                    tid = machineDetails.PosedcTerminalId,
                    tran_type = Convert.ToInt32(txnMode),
                    amount = posrequest.amount,
                    bill_no = posrequest.transactionNo,
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
                    tid = machineDetails.PosedcTerminalId,
                    tran_type = Convert.ToInt32(txnMode),
                    bill_no = posrequest.transactionNo,
                    erp_tran_id = posrequest.refernceId,
                    erp_client_id = erp_client_id,
                    source_id = source_id
                };
            }
            else
            {
                payload = new { mid = mid, tid = machineDetails.PosedcTerminalId }; // fallback
            }

            // ✅ Ensure exact casing (snake_case preserved in payload)
            var jsonPayload = System.Text.Json.JsonSerializer.Serialize(payload, new JsonSerializerOptions
            {
                PropertyNamingPolicy = null
            });
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
            paymentRequestRes.code = 200;

            try
            {
                // Check content type → if not JSON, return raw response
                if (response.Content.Headers.ContentType?.MediaType != "application/json")
                {
                    paymentRequestRes.status = "Failed";
                    paymentRequestRes.message = "Non-JSON response from ICICI";
                    return StatusCode(200, paymentRequestRes);
                }

                // ✅ Parse JSON safely
                var jsonObj = System.Text.Json.JsonSerializer.Deserialize<JsonElement>(responseString);

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
                    : posrequest.transactionNo;

                // Reference ID
                paymentRequestRes.refernceId = jsonObj.TryGetProperty("Tran_Id", out var tranIdProp)
                    ? tranIdProp.GetString()
                    : erp_tran_id;

                // Optional mappings
                paymentRequestRes.cardNo = "";
                paymentRequestRes.upi = "";
                paymentRequestRes.paymentMode = posrequest.txntype;
            }
            catch (System.Text.Json.JsonException ex)
            {
                // JSON parse error
                paymentRequestRes.status = "Failed";
                paymentRequestRes.message = $"Invalid JSON response: {ex.Message}";
            }
            string apiStatus = "";
            OperationResult result;
            if (action == "initiate")
                result = await _pgdb.InsertPaylinkPosEdcMachineTransactionLogAsync(posrequest, action, jsonPayload, responseString, apiStatus);
            else if (action == "status")
                result = await _pgdb.UpdateStatusPaylinkPosEdcMachineTransactionLogAsync(posrequest, jsonPayload, responseString);
            else if (action == "cancel")
                result = await _pgdb.UpdateCancelPaylinkPosEdcMachineTransactionLogAsync(posrequest, jsonPayload, responseString);


            return StatusCode(200, paymentRequestRes);
        }

        // ------------------ mswipe Pay ------------------
        private async Task<IActionResult> MswipePaymentMethod(PosPaymentRequest posrequest, PosActiveEdcMachineDetails machineDetails, string action)
        {
            try
            {
                ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;

                PaymentRequestResponse paymentRequestRes = new PaymentRequestResponse();
                // var section = _config.GetSection("PaymentProviders:mswipe");

                string username = machineDetails.MswipeUsername;
                string password = machineDetails.MswipePassword; // new for status API
                string saltKey = machineDetails.MswipeSaltKey;
                string clientKey = machineDetails.StoreId;
                string clientcode = machineDetails.MswipeClientCode; //"9200371656"
                string userId = machineDetails.MswipeUsername;      // new for status API

                // 🔹 Select API URL
                string url = action switch
                {
                    "status" => machineDetails.PosStatusUrl,
                    "cancel" => machineDetails.PosCancelUrl,
                    _ => machineDetails.PosInitiateUrl   // default initiate
                };

                object payload;
                HttpRequestMessage requestMsg;

                // decide mode based on Txntype
                string txnMode = posrequest.txntype switch
                {
                    "00" => "00",  // CARD
                    "01" => "03",  // QRCODE
                    "02" => "00",  // CARD
                    _ => "00"
                };

                if (action == "initiate")
                {
                    decimal amount = Convert.ToDecimal(posrequest.amount, CultureInfo.InvariantCulture);

                    // Convert rupees to paise
                    long paise = (long)Math.Round(amount * 100M, MidpointRounding.AwayFromZero);

                    // Exact value to send in payload (no padding)
                    string formattedAmount = paise.ToString(CultureInfo.InvariantCulture);

                    // Use exact same string for MAC
                    string mac = GenerateMac(formattedAmount, txnMode, clientcode, saltKey);

                    payload = new
                    {
                        amount = formattedAmount,
                        clientcode = clientcode,
                        Mac = mac,
                        notes = "",
                        txntype = txnMode,
                        storeid = "",
                        tid = machineDetails.PosedcTerminalId,
                        invoiceno = posrequest.transactionNo,
                        extranotes1 = posrequest.customerName ?? "",
                        extranotes2 = posrequest.patientId ?? "",
                        extranotes3 = posrequest.mobileNumber ?? "",
                        extranotes4 = posrequest.refernceId ?? "",
                        extranotes5 = "",
                        extranotes6 = "",
                        extranotes7 = "",
                        extranotes8 = "",
                        extranotes9 = ""
                    };

                    requestMsg = new HttpRequestMessage(HttpMethod.Post, url);
                    requestMsg.Content = new StringContent(
                        System.Text.Json.JsonSerializer.Serialize(payload),
                        Encoding.UTF8,
                        "application/json"
                    );
                }
                else if (action == "cancel")
                {
                    payload = new
                    {
                        tokenid = posrequest.refernceId,
                        invoiceno = posrequest.transactionNo,
                        clientcode = clientcode
                    };

                    requestMsg = new HttpRequestMessage(HttpMethod.Post, url);
                    requestMsg.Content = new StringContent(System.Text.Json.JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
                }
                else if (action == "status")
                {
                    payload = new
                    {
                        client_code = clientKey,
                        mer_invoiceno = posrequest.transactionNo
                    };

                    requestMsg = new HttpRequestMessage(HttpMethod.Post, url);
                    requestMsg.Content = new StringContent(System.Text.Json.JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

                    // 🔹 Add required headers
                    requestMsg.Headers.Add("userId", userId);
                    requestMsg.Headers.Add("password", password);
                }
                else
                {
                    return BadRequest(new { error = "Invalid action. Use initiate, cancel or status." });
                }

                // 🔹 Send Request
                var client = _httpClient.CreateClient();
                var response = await client.SendAsync(requestMsg);
                var responseString = await response.Content.ReadAsStringAsync();

                var jsonObj = System.Text.Json.JsonSerializer.Deserialize<JsonElement>(responseString);
                string outgoingJson = System.Text.Json.JsonSerializer.Serialize(payload);

                // 🔹 Map Response
                paymentRequestRes.code = 200;
                paymentRequestRes.upi = "";
                paymentRequestRes.cardNo = "";
                paymentRequestRes.QrCodeurl = "";
                paymentRequestRes.PaymentUrl = "";

                if (action == "initiate")
                {
                    string f039 = jsonObj.TryGetProperty("F039", out var f039Prop)
                        ? (f039Prop.GetString() ?? "")
                        : "";

                    string desc = jsonObj.TryGetProperty("Desc", out var descProp)
                        ? (descProp.GetString() ?? "")
                        : "Unknown error";

                    string errorDesc = jsonObj.TryGetProperty("Error Desc", out var errDescProp)
                        ? (errDescProp.GetString() ?? "")
                        : "";

                    string token = jsonObj.TryGetProperty("token", out var tokenProp)
                        ? (tokenProp.GetString() ?? "")
                        : "";

                    // Success only if F039 == 00 and token exists
                    if (f039 == "00" && !string.IsNullOrWhiteSpace(token))
                    {
                        paymentRequestRes.status = "True";
                        paymentRequestRes.message = string.IsNullOrWhiteSpace(desc) ? "Success" : desc;
                        paymentRequestRes.refernceId = token;
                    }
                    else
                    {
                        paymentRequestRes.status = "False";
                        paymentRequestRes.message = !string.IsNullOrWhiteSpace(errorDesc) ? errorDesc : desc;
                        paymentRequestRes.refernceId = "";
                    }
                }
                else if (action == "cancel")
                {
                    paymentRequestRes.status = jsonObj.TryGetProperty("ResponseCode", out var respCode)
                                               ? (respCode.GetString() == "00" ? "True" : "False")
                                               : "False";

                    paymentRequestRes.message = jsonObj.TryGetProperty("ResponseMessage", out var msgProp)
                                                ? msgProp.GetString()
                                                : "Unknown error";

                    paymentRequestRes.refernceId = posrequest.refernceId; // comes from request
                }
                else if (action == "status")
                {
                    bool isTopLevelStatusTrue = false;
                    string topLevelErrMsg = "";
                    string finalMessage = "Transaction status not confirmed";

                    // Default values
                    paymentRequestRes.status = "False";
                    paymentRequestRes.refernceId = posrequest.transactionNo; // fallback

                    // Read top-level status
                    if (jsonObj.TryGetProperty("status", out var statusProp))
                    {
                        var statusText = statusProp.ValueKind == JsonValueKind.String
                            ? statusProp.GetString()
                            : statusProp.ToString();

                        isTopLevelStatusTrue = string.Equals(statusText, "true", StringComparison.OrdinalIgnoreCase);
                    }

                    // Read top-level ErrMsg
                    if (jsonObj.TryGetProperty("ErrMsg", out var topErrProp))
                    {
                        topLevelErrMsg = topErrProp.GetString() ?? "";
                    }

                    // Validate VerificationData
                    if (isTopLevelStatusTrue &&
                        jsonObj.TryGetProperty("VerificationData", out var verificationData) &&
                        verificationData.ValueKind == JsonValueKind.Object)
                    {
                        string invoiceNo = verificationData.TryGetProperty("InvoiceNO", out var invoiceProp)
                            ? (invoiceProp.GetString() ?? "")
                            : "";

                        string referenceNo = verificationData.TryGetProperty("ReferenceNo", out var refProp)
                            ? (refProp.GetString() ?? "")
                            : "";

                        decimal amount = 0M;
                        if (verificationData.TryGetProperty("Amount", out var amountProp))
                        {
                            if (amountProp.ValueKind == JsonValueKind.Number)
                            {
                                amountProp.TryGetDecimal(out amount);
                            }
                            else if (amountProp.ValueKind == JsonValueKind.String)
                            {
                                decimal.TryParse(amountProp.GetString(), NumberStyles.Any, CultureInfo.InvariantCulture, out amount);
                            }
                        }

                        string txnDate = verificationData.TryGetProperty("TxnDate", out var txnDateProp)
                            ? (txnDateProp.GetString() ?? "")
                            : "";

                        string responseMsg = verificationData.TryGetProperty("ResponseMsg", out var responseMsgProp)
                            ? (responseMsgProp.GetString() ?? "")
                            : "";

                        string responseCode = verificationData.TryGetProperty("ResponseCode", out var responseCodeProp)
                            ? (responseCodeProp.GetString() ?? "")
                            : "";

                        string rrnNo = verificationData.TryGetProperty("RRNO", out var rrnProp)
                            ? (rrnProp.GetString() ?? "")
                            : "";

                        string authCode = verificationData.TryGetProperty("AuthCode", out var authProp)
                            ? (authProp.GetString() ?? "")
                            : "";

                        string verificationErrMsg = verificationData.TryGetProperty("ErrMsg", out var verErrProp)
                            ? (verErrProp.GetString() ?? "")
                            : "";

                        string merchant = verificationData.TryGetProperty("Merchant", out var merchantProp)
                            ? (merchantProp.GetString() ?? "")
                            : "";

                        string cardHolder = verificationData.TryGetProperty("CardHolder", out var cardHolderProp)
                            ? (cardHolderProp.GetString() ?? "")
                            : "";

                        string cardLastDigits = verificationData.TryGetProperty("Card_Last_Digits", out var cardLastProp)
                            ? (cardLastProp.GetString() ?? "")
                            : "";

                        string transType = verificationData.TryGetProperty("Trans_Type", out var transTypeProp)
                            ? (transTypeProp.GetString() ?? "")
                            : "";

                        string jvVoucherNo = verificationData.TryGetProperty("JV_Voucher_No", out var jvProp)
                            ? (jvProp.GetString() ?? "")
                            : "";

                        string custDeviceId = verificationData.TryGetProperty("Cust_Device_Id", out var deviceProp)
                            ? (deviceProp.GetString() ?? "")
                            : "";

                        string merchantMobile = verificationData.TryGetProperty("MerchantMobile", out var merchantMobileProp)
                            ? (merchantMobileProp.GetString() ?? "")
                            : "";

                        string cardHolderMobile = verificationData.TryGetProperty("CardHolderMobile", out var cardHolderMobileProp)
                            ? (cardHolderMobileProp.GetString() ?? "")
                            : "";

                        long transactionNo = 0;
                        if (verificationData.TryGetProperty("TransactionNo", out var txnNoProp))
                        {
                            if (txnNoProp.ValueKind == JsonValueKind.Number)
                            {
                                if (!txnNoProp.TryGetInt64(out transactionNo))
                                {
                                    // Handles cases like 3217265512.0
                                    var txnDouble = txnNoProp.GetDouble();
                                    transactionNo = Convert.ToInt64(txnDouble);
                                }
                            }
                            else if (txnNoProp.ValueKind == JsonValueKind.String)
                            {
                                long.TryParse(txnNoProp.GetString(), out transactionNo);
                            }
                        }

                        bool paymentDone = false;
                        bool paymentDoneFieldExists = false;
                        if (verificationData.TryGetProperty("paymentDone", out var paymentDoneProp))
                        {
                            paymentDoneFieldExists = true;

                            if (paymentDoneProp.ValueKind == JsonValueKind.True || paymentDoneProp.ValueKind == JsonValueKind.False)
                            {
                                paymentDone = paymentDoneProp.GetBoolean();
                            }
                            else if (paymentDoneProp.ValueKind == JsonValueKind.String)
                            {
                                bool.TryParse(paymentDoneProp.GetString(), out paymentDone);
                            }
                        }

                        bool isApprovedResponse = responseMsg.Contains("approved", StringComparison.OrdinalIgnoreCase);
                        bool isResponseCodeOk = responseCode == "00";
                        bool hasInvoice = !string.IsNullOrWhiteSpace(invoiceNo);
                        bool hasTxnNo = transactionNo > 0;
                        bool hasRrn = !string.IsNullOrWhiteSpace(rrnNo);
                        bool hasAuthCode = !string.IsNullOrWhiteSpace(authCode);
                        bool hasAmount = amount > 0;
                        bool hasNoVerificationError = string.IsNullOrWhiteSpace(verificationErrMsg);

                        // Recommended: top-level status + RRN + ResponseCode=00
                        bool isStrictSuccess = isTopLevelStatusTrue && hasRrn && isResponseCodeOk;

                        if (isStrictSuccess)
                        {
                            paymentRequestRes.status = "True";
                            finalMessage = string.IsNullOrWhiteSpace(responseMsg) ? "Transaction approved" : responseMsg;
                            paymentRequestRes.refernceId = !string.IsNullOrWhiteSpace(referenceNo) ? referenceNo : invoiceNo;
                        }
                        else
                        {
                            paymentRequestRes.status = "False";

                            if (!isTopLevelStatusTrue)
                                finalMessage = !string.IsNullOrWhiteSpace(topLevelErrMsg) ? topLevelErrMsg : "Transaction status is false";
                            else if (!hasRrn)
                                finalMessage = "Transaction failed. RRN number is missing.";
                            else if (!isResponseCodeOk)
                                finalMessage = $"Transaction declined or not approved. ResponseCode: {responseCode}";
                            else
                                finalMessage = "Transaction status not confirmed";

                            paymentRequestRes.refernceId = !string.IsNullOrWhiteSpace(referenceNo) ? referenceNo : posrequest.transactionNo;
                        }
                        // Return important fields to frontend / caller
                        paymentRequestRes.originalResponse = responseString;

                        // Optional extra fields if your model supports them
                        paymentRequestRes.transactionId = !string.IsNullOrWhiteSpace(invoiceNo) ? invoiceNo : posrequest.transactionNo;
                        paymentRequestRes.cardNo = !string.IsNullOrWhiteSpace(cardLastDigits) ? $"XXXX-XXXX-XXXX-{cardLastDigits}" : "";
                        paymentRequestRes.upi = "";
                        paymentRequestRes.QrCodeurl = "";
                        paymentRequestRes.PaymentUrl = "";
                        paymentRequestRes.RRN = rrnNo;
                        paymentRequestRes.refernceId = invoiceNo;
                        paymentRequestRes.OrginalMessage = finalMessage;
                        paymentRequestRes.Orginalcode = "";
                        paymentRequestRes.PaymentUrl = "";
                        paymentRequestRes.paymentMode = posrequest.txntype;
                        // If your PaymentRequestResponse class has these properties, assign them:
                        // paymentRequestRes.amount = amount.ToString("0.00", CultureInfo.InvariantCulture);
                        // paymentRequestRes.authCode = authCode;
                        // paymentRequestRes.rrnNo = rrnNo;
                        // paymentRequestRes.responseCode = responseCode;
                        // paymentRequestRes.responseMsg = responseMsg;
                        // paymentRequestRes.invoiceNo = invoiceNo;
                        // paymentRequestRes.referenceNo = referenceNo;
                        // paymentRequestRes.transactionNo = transactionNo.ToString(CultureInfo.InvariantCulture);
                        // paymentRequestRes.txnDate = txnDate;
                        // paymentRequestRes.merchant = merchant;
                        // paymentRequestRes.cardHolder = cardHolder?.Trim();
                        // paymentRequestRes.transType = transType;
                        // paymentRequestRes.jvVoucherNo = jvVoucherNo;
                        // paymentRequestRes.custDeviceId = custDeviceId;
                        // paymentRequestRes.merchantMobile = merchantMobile;
                        // paymentRequestRes.cardHolderMobile = cardHolderMobile;
                        // paymentRequestRes.paymentDone = paymentDoneFieldExists ? paymentDone.ToString() : "";
                        // paymentRequestRes.providerErrMsg = verificationErrMsg;
                    }
                    else
                    {
                        paymentRequestRes.status = "False";
                        finalMessage = !string.IsNullOrWhiteSpace(topLevelErrMsg)
                            ? topLevelErrMsg
                            : "Invalid status response or VerificationData missing";
                        paymentRequestRes.refernceId = posrequest.transactionNo;
                    }

                    paymentRequestRes.message = finalMessage;
                }

                paymentRequestRes.transactionId = posrequest.transactionNo;
                paymentRequestRes.originalResponse = responseString;

                // 🔹 Set API status properly
                string apiStatus = paymentRequestRes.status == "True" ? "SUCCESS" : "FAILED";

                OperationResult result;
                if (action == "initiate")
                    result = await _pgdb.InsertPaylinkPosEdcMachineTransactionLogAsync(posrequest, action, outgoingJson, responseString, apiStatus);
                else if (action == "status")
                    result = await _pgdb.UpdateStatusPaylinkPosEdcMachineTransactionLogAsync(posrequest, outgoingJson, responseString);
                else // cancel
                    result = await _pgdb.UpdateCancelPaylinkPosEdcMachineTransactionLogAsync(posrequest, outgoingJson, responseString);

                return StatusCode(200, paymentRequestRes);
            }
            catch (Exception ex)
            {
                return StatusCode(200, new { error = ex.Message });
            }
        }
        // ------------------ PineLab Pay ------------------
        private async Task<IActionResult> pinelabmethod(PosPaymentRequest posrequest, PosActiveEdcMachineDetails machineDetails, string action)
        {
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
            PaymentRequestResponse paymentRequestRes = new PaymentRequestResponse();
            // var section = _config.GetSection("PaymentProviders:pinelab");
            string UserID = posrequest.createby;
            string MerchantID = machineDetails.PinelabMerchantId;
            string MerchantStorePosCode = machineDetails.MerchantPosCode;
            string StoreId = machineDetails.StoreId;
            string ClientId = machineDetails.ClientId;
            string SecurityToken = machineDetails.PinelabSecurityToken;

            string url = action switch
            {
                "status" => machineDetails.PosStatusUrl,
                "cancel" => machineDetails.PosCancelUrl,
                _ => machineDetails.PosInitiateUrl // default = pay
            };

            // decide mode based on Txntype
            string txnMode = posrequest.txntype switch
            {
                "00" => "1",
                "01" => "10",
                "02" => "CASH",
                _ => "10"
            };

            object payload;


            // ✅ Build payload based on action
            if (action == "initiate")
            {
                long Amountpaisa = ConvertRupeesToPaise(posrequest.amount);

                payload = new
                {
                    TransactionNumber = posrequest.transactionNo,
                    SequenceNumber = "1",
                    AllowedPaymentMode = txnMode,
                    // MerchantStorePosCode = MerchantStorePosCode,
                    Amount = Amountpaisa,
                    UserID = posrequest.createby,
                    MerchantID = MerchantID,
                    SecurityToken = SecurityToken,
                    Storeid = StoreId,
                    Clientid = ClientId,
                    AutoCancelDurationInMinutes = "3"
                    // PlutusTransactionReferenceID = posrequest.refernceId
                };

            }
            else if (action == "status")
            {
                payload = new
                {
                    MerchantID = MerchantID,
                    SecurityToken = SecurityToken,
                    Storeid = StoreId,
                    Clientid = ClientId,
                    // IMEI = machineDetails.PosedcTerminalId,
                    //MerchantStorePosCode = MerchantStorePosCode,
                    PlutusTransactionReferenceID = posrequest.refernceId
                };
            }
            else if (action == "cancel")
            {

                decimal originalAmount = decimal.Parse(posrequest.amount);  // convert string → decimal
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
                    SecurityToken = SecurityToken,
                    Storeid = StoreId,
                    Clientid = ClientId,
                    MerchantStorePosCode = MerchantStorePosCode,
                    SequenceNumber = "1",
                    TransactionNumber = posrequest.transactionNo,
                    Amount = Amount,
                    AllowedPaymentMode = "1",
                    AutoCancelDurationInMinutes = "2",
                    PlutusTransactionReferenceID = posrequest.refernceId
                };
            }
            else
            {
                // fallback default payload
                payload = new { UserID, MerchantID };
            }

            var jsonPayload = System.Text.Json.JsonSerializer.Serialize(payload);
            var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

            var client = _httpClient.CreateClient();
            var response = await client.PostAsync(url, content);
            var responseString = await response.Content.ReadAsStringAsync();

            // ✅ Deserialize JSON into a dynamic object
            var jsonObj = System.Text.Json.JsonSerializer.Deserialize<JsonElement>(responseString);

            // ========================================================================
            //                          DEFAULT VALUES
            // ========================================================================
            paymentRequestRes.upi = "";
            paymentRequestRes.cardNo = "";
            paymentRequestRes.QrCodeurl = "";
            paymentRequestRes.PaymentUrl = "";
            paymentRequestRes.RRN = "";
            paymentRequestRes.code = 200;

            // ========================================================================
            //                          ResponseCode → status
            // ========================================================================
            if (jsonObj.TryGetProperty("ResponseCode", out var respCodeProp))
            {
                int code = respCodeProp.ValueKind == JsonValueKind.Number ? respCodeProp.GetInt32() : -1;
                paymentRequestRes.status = code == 0 ? "True" : "False";
                paymentRequestRes.Orginalcode = code.ToString();
            }
            else
            {
                paymentRequestRes.status = "False";
                paymentRequestRes.Orginalcode = "";
            }

            // ========================================================================
            //                          ResponseMessage
            // ========================================================================
            paymentRequestRes.message = jsonObj.TryGetProperty("ResponseMessage", out var messageProp)
                                        ? messageProp.GetString()
                                        : "Unknown error";

            paymentRequestRes.OrginalMessage = paymentRequestRes.message;

            // ========================================================================
            //                          TransactionId
            // ========================================================================
            paymentRequestRes.transactionId = posrequest.transactionNo;


            // ========================================================================
            //                 Extract TransactionData Common Tags
            // ========================================================================
            string rrn = "";
            string approvalCode = "";
            string cardNumber = "";
            string invoiceNumber = "";
            string paymentMode = "";
            string amount = "";

            if (jsonObj.TryGetProperty("TransactionData", out var txnArray) &&
                txnArray.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in txnArray.EnumerateArray())
                {
                    if (!item.TryGetProperty("Tag", out var tagProp) ||
                        !item.TryGetProperty("Value", out var valueProp))
                        continue;

                    string tag = tagProp.GetString();
                    string val = valueProp.GetString();

                    switch (tag)
                    {
                        case "RRN":
                            rrn = val;
                            break;

                        case "ApprovalCode":
                            approvalCode = val;
                            break;

                        case "Card Number":
                        case "CardNumber":
                            cardNumber = val;
                            break;

                        case "Invoice Number":
                        case "InvoiceNumber":
                            invoiceNumber = val;
                            break;

                        case "Customer VPA":
                        case "CustomerVPA":
                        case "UPI VPA":
                        case "VPA":
                            paymentRequestRes.upi = val;   // <-- UPI VPA assigned here
                            break;

                        case "Amount":
                            amount = val;
                            break;
                    }
                }
            }

            // Assign extracted common values
            paymentRequestRes.RRN = rrn;
            paymentRequestRes.cardNo = cardNumber;
            paymentRequestRes.paymentMode = paymentMode != "" ? paymentMode : posrequest.txntype;


            // ========================================================================
            //                        Action-specific mappings
            // ========================================================================
            if (action == "initiate")
            {
                paymentRequestRes.refernceId = jsonObj.TryGetProperty("PlutusTransactionReferenceID", out var refProp)
                                               ? refProp.GetInt32().ToString()
                                               : "";
            }
            else if (action == "status")
            {
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
                paymentRequestRes.refernceId = posrequest.refernceId;
            }


            // ========================================================================
            //                     Keep Original Raw JSON Response
            // ========================================================================
            paymentRequestRes.originalResponse = responseString;

            string apiStatus = "";
            OperationResult result;
            if (action == "initiate")
                result = await _pgdb.InsertPaylinkPosEdcMachineTransactionLogAsync(posrequest, action, payload.ToString(), responseString, apiStatus);
            else if (action == "status")
                result = await _pgdb.UpdateStatusPaylinkPosEdcMachineTransactionLogAsync(posrequest, payload.ToString(), responseString);
            else if (action == "cancel")
                result = await _pgdb.UpdateCancelPaylinkPosEdcMachineTransactionLogAsync(posrequest, payload.ToString(), responseString);

            return StatusCode(200, paymentRequestRes);
        }

        // ------------------ WORLDLINE ------------------
        private async Task<IActionResult> worldlinemethod(PosPaymentRequest posrequest, PosActiveEdcMachineDetails machineDetails, string action)
        {
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
            PaymentRequestResponse paymentRequestRes = new PaymentRequestResponse();

            string encKey = machineDetails.WorldlineEncKey;
            string encIv = machineDetails.WorldlineEncIv;

            string url = action switch
            {
                "status" => machineDetails.PosStatusUrl,
                "cancel" => machineDetails.PosCancelUrl,
                _ => machineDetails.PosInitiateUrl
            };

            // Decide mode based on Txntype
            string txnMode = posrequest.txntype switch
            {
                "00" => "SALE",
                "01" => "SALE-BQR",
                "02" => "CASH",
                _ => "SALE"
            };

            string actionId = posrequest.txntype switch
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
                    tid = machineDetails.PosedcTerminalId,
                    amount = posrequest.amount,
                    organization_code = "Retail",
                    additional_attribute1 = posrequest.customerName,
                    additional_attribute2 = posrequest.patientId,
                    additional_attribute3 = posrequest.mobileNumber,
                    invoiceNumber = Convert.ToInt32(posrequest.transactionNo),
                    rrn = string.Empty,
                    type = txnMode,
                    cb_amt = string.Empty,
                    app_code = string.Empty,
                    tokenisedValue = posrequest.refernceId,
                    actionId,
                    request_urn = string.Empty
                };
            }
            else if (action == "status")
            {
                payload = new
                {
                    urn = posrequest.refernceId,
                    tid = machineDetails.PosedcTerminalId,
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
                    invoiceNumber = posrequest.transactionNo,
                    organization_code = "Retail",
                    request_urn = "",
                    rrn = "",
                    tid = machineDetails.PosedcTerminalId,
                    tokenisedValue = "",
                    type = txnMode,
                    urn = posrequest.refernceId


                };
            }
            else
            {
                // Safe fallback payload
                payload = new
                {
                    tid = machineDetails.PosedcTerminalId,
                    amount = posrequest.amount,
                    type = txnMode,
                    referenceId = posrequest.refernceId
                };
            }

            // Serialize + Encrypt payload
            string jsonPayload = System.Text.Json.JsonSerializer.Serialize(payload);

            string encryptedPayload;
            try
            {
                encryptedPayload = AES_256_CBC.Encrypt(jsonPayload, encKey, encIv);
            }
            catch (Exception ex)
            {
                return StatusCode(200, new { error = "Encryption failed", details = ex.Message });
            }



            // Build final request body (pretty JSON for readability)
            var finalBody = new { data = encryptedPayload };
            string requestBody = System.Text.Json.JsonSerializer.Serialize(
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
                return StatusCode(200, new { error = "Request to Worldline failed", details = ex.Message });
            }

            string responseString = await response.Content.ReadAsStringAsync();
            paymentRequestRes.upi = "";
            paymentRequestRes.cardNo = "";
            paymentRequestRes.QrCodeurl = "";
            paymentRequestRes.PaymentUrl = "";
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
            var jsonObj = System.Text.Json.JsonSerializer.Deserialize<JsonElement>(finalJson);

            // Map common fields
            paymentRequestRes.code = 200;

            if (action == "initiate")
            {
                // ✅ Status
                paymentRequestRes.status = jsonObj.TryGetProperty("response_code", out var respCode)
                    ? (
                        respCode.ValueKind == JsonValueKind.String
                            ? (respCode.GetString() == "0" ? "True" : "False")
                            : respCode.ValueKind == JsonValueKind.Number
                                ? (respCode.GetInt32() == 0 ? "True" : "False")
                                : "False"
                      )
                    : "False";


                // ✅ Message
                paymentRequestRes.message = jsonObj.TryGetProperty("response_message", out var messageProp)
                    ? messageProp.GetString()
                    : "Unknown error";

                // ✅ TransactionId
                paymentRequestRes.transactionId = posrequest.transactionNo;
                // ✅ ReferenceId (map urn → refernceId)
                if (action == "status")
                {
                    paymentRequestRes.refernceId = jsonObj.TryGetProperty("invoicenumber", out var invNo)
                                 ? invNo.GetString()
                                 : "";
                    paymentRequestRes.status =
      jsonObj.TryGetProperty("status", out var statusProp) &&
      statusProp.ValueKind == JsonValueKind.String &&
      statusProp.GetString()?.ToLower() == "success"
          ? "True"
          : "False";



                    // ✅ Message
                    paymentRequestRes.message = jsonObj.TryGetProperty("status", out var statusmsg)
                        ? statusmsg.GetString()
                        : "Unknown error";

                }
                else
                {
                    paymentRequestRes.refernceId = jsonObj.TryGetProperty("urn", out var urnProp)
                          ? urnProp.GetString()
                          : "";
                }
            }

            else if (action == "status")
            {
                bool isSuccess =
                    jsonObj.TryGetProperty("status", out var statusProp) &&
                    statusProp.ValueKind == JsonValueKind.String &&
                    statusProp.GetString()?.ToLower() == "success";

                // 🔹 Status Code (HTTP style)
                paymentRequestRes.code = isSuccess ? 200 : 400;

                // 🔹 Status
                paymentRequestRes.status = isSuccess ? "True" : "False";

                // 🔹 Message
                paymentRequestRes.message = jsonObj.TryGetProperty("status", out var statusMsg)
                    ? statusMsg.GetString()
                    : "Unknown error";

                // 🔹 Transaction Id
                paymentRequestRes.transactionId = posrequest.transactionNo;

                // 🔹 Reference Id (Invoice Number)
                paymentRequestRes.refernceId = jsonObj.TryGetProperty("invoicenumber", out var invProp)
                    ? invProp.GetString()
                    : "";

                // 🔹 RRN
                paymentRequestRes.RRN = jsonObj.TryGetProperty("rrn", out var rrnProp)
                    ? rrnProp.GetString()
                    : "";

                // 🔹 UPI Txn Id
                paymentRequestRes.upi = jsonObj.TryGetProperty("upi_txn_id", out var upiProp)
                    ? upiProp.GetString()
                    : "";

                // 🔹 Card / UPI Id
                paymentRequestRes.cardNo = jsonObj.TryGetProperty("masked_card_number", out var cardProp)
                    ? cardProp.GetString()
                    : "";
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
                paymentRequestRes.transactionId = posrequest.transactionNo;
            }
            else
            {
                paymentRequestRes.status = "False";
                paymentRequestRes.message = "Unsupported action mapping";
                paymentRequestRes.transactionId = posrequest.transactionNo;
                paymentRequestRes.refernceId = posrequest.refernceId;
            }

            // Always populate these for consistency

            paymentRequestRes.paymentMode = posrequest.txntype;
            paymentRequestRes.originalResponse = finalJson; // store decrypted if available

            string apiStatus = "";
            OperationResult result;
            if (action == "initiate")
                result = await _pgdb.InsertPaylinkPosEdcMachineTransactionLogAsync(posrequest, action, payload.ToString(), finalJson, apiStatus);
            else if (action == "status")
                result = await _pgdb.UpdateStatusPaylinkPosEdcMachineTransactionLogAsync(posrequest, payload.ToString(), finalJson);
            else if (action == "cancel")
                result = await _pgdb.UpdateCancelPaylinkPosEdcMachineTransactionLogAsync(posrequest, payload.ToString(), finalJson);

            return StatusCode(200, paymentRequestRes);


        }

        // ------------------ Paytm Pay ------------------
        private async Task<IActionResult> paytmmethod(PosPaymentRequest posrequest, PosActiveEdcMachineDetails machineDetails, string action)
        {

            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
            PaymentRequestResponse paymentRequestRes = new PaymentRequestResponse();

            string MID = machineDetails.PaytmMid;
            string MerchantKey = machineDetails.PaytmMerchantKey;
            await _logService.WritePaymentLogAsync("paytmmethod", machineDetails.PaytmMid, machineDetails.PaytmMerchantKey);

            string url = action switch
            {
                "status" => machineDetails.PosStatusUrl,
                "cancel" => machineDetails.PosCancelUrl,
                _ => machineDetails.PosInitiateUrl
            };

            string txnMode = posrequest.txntype switch
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
            { "paytmTid", machineDetails.PosedcTerminalId },
            { "transactionDateTime", dates },
            { "merchantTransactionId", posrequest.transactionNo }
        };
            }
            else if (action == "initiate")
            {
                long amountpaise = ConvertRupeesToPaise(posrequest.amount);
                // ✅ Initiate & Cancel require these
                Map = new Dictionary<string, string>
        {
            { "paytmMid", MID },
            { "paytmTid", machineDetails.PosedcTerminalId },
            { "transactionDateTime", dates },
            { "merchantTransactionId", posrequest.transactionNo },
            { "transactionAmount",  amountpaise.ToString()},
            { "paymentMode", txnMode }
        };
            }
            else if (action == "cancel")
            {
                // ✅ Status requires only these 4
                Map = new Dictionary<string, string>
        {
            { "paytmMid", MID },
            { "paytmTid", machineDetails.PosedcTerminalId },
            { "transactionDateTime", dates },
            { "merchantTransactionId", posrequest.transactionNo }
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
                return StatusCode(200, new { error = "Failed to send request", details = ex.Message });
            }

            string responseText = await response.Content.ReadAsStringAsync();


            var jsonObj = System.Text.Json.JsonSerializer.Deserialize<JsonElement>(responseText);

            // Map common fields
            paymentRequestRes.code = 200;
            paymentRequestRes.upi = "";
            paymentRequestRes.cardNo = "";
            paymentRequestRes.Orginalcode = "";
            paymentRequestRes.OrginalMessage = "";
            paymentRequestRes.RRN = "";
            paymentRequestRes.QrCodeurl = "";
            paymentRequestRes.PaymentUrl = "";

            if (action == "initiate")
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
                    paymentRequestRes.Orginalcode = resultCode;

                    paymentRequestRes.message = resultInfo.TryGetProperty("resultMsg", out var msgProp)
                        ? msgProp.GetString()
                        : "Unknown error";
                    paymentRequestRes.Orginalcode = paymentRequestRes.message;
                }
                else
                {
                    paymentRequestRes.status = "False";
                    paymentRequestRes.message = "Invalid Paytm response structure";
                }

                // ✅ TransactionId
                paymentRequestRes.transactionId = posrequest.transactionNo;

                // ✅ ReferenceId → use merchantTransactionId from body

                paymentRequestRes.refernceId = posrequest.transactionNo;

            }
            else if (action == "status")
            {
                if (jsonObj.TryGetProperty("body", out var bodyProp) &&
                    bodyProp.TryGetProperty("resultInfo", out var resultInfo))
                {
                    string resultStatus = resultInfo.TryGetProperty("resultStatus", out var statusProp)
                                          ? statusProp.GetString()
                                          : "FAIL";

                    // SUCCESS / FAIL
                    paymentRequestRes.status = resultStatus.Equals("SUCCESS", StringComparison.OrdinalIgnoreCase)
                                                ? "True"
                                                : "False";

                    paymentRequestRes.message = resultInfo.TryGetProperty("resultMsg", out var msgProp)
                                                ? msgProp.GetString()
                                                : "Unknown error";

                    paymentRequestRes.Orginalcode = resultInfo.TryGetProperty("resultCode", out var rcProp)
                                                    ? rcProp.GetString()
                                                    : "";

                    // TransactionId from your request
                    paymentRequestRes.transactionId = posrequest.transactionNo;



                    // -----------------------------
                    // ✅ Assign RRN ONLY IF SUCCESS
                    // -----------------------------
                    if (paymentRequestRes.status == "True")
                    {
                        paymentRequestRes.RRN = bodyProp.TryGetProperty("retrievalReferenceNo", out var rrnProp)
                                                ? rrnProp.GetString()
                                                : "";
                        // ReferenceId → acquirementId
                        paymentRequestRes.refernceId = bodyProp.TryGetProperty("acquirementId", out var txnIdProp)
                        ? txnIdProp.GetString()
                        : "";
                    }
                    else
                    {
                        paymentRequestRes.RRN = "";   // No RRN for failed/pending
                    }

                    // UPI Mode → no card number
                    paymentRequestRes.cardNo = "";
                }
                else
                {
                    paymentRequestRes.status = "False";
                    paymentRequestRes.message = "Invalid Paytm response";
                    paymentRequestRes.RRN = "";
                    paymentRequestRes.refernceId = "";
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

                paymentRequestRes.transactionId = posrequest.transactionNo;
            }
            else
            {
                paymentRequestRes.status = "False";
                paymentRequestRes.message = "Unsupported action mapping";
                paymentRequestRes.transactionId = posrequest.transactionNo;
                paymentRequestRes.refernceId = posrequest.refernceId;
            }

            // Always populate these for consistency
            // paymentRequestRes.cardNo = "";
            paymentRequestRes.paymentMode = posrequest.txntype;
            paymentRequestRes.originalResponse = responseText;

            string apiStatus = "";

            OperationResult result;
            if (action == "initiate")
                result = await _pgdb.InsertPaylinkPosEdcMachineTransactionLogAsync(posrequest, action, body.ToString(), responseText, apiStatus);
            else if (action == "status")
                result = await _pgdb.UpdateStatusPaylinkPosEdcMachineTransactionLogAsync(posrequest, body.ToString(), responseText);
            else if (action == "cancel")
                result = await _pgdb.UpdateCancelPaylinkPosEdcMachineTransactionLogAsync(posrequest, body.ToString(), responseText);

            return StatusCode(200, paymentRequestRes);
        }


        // ------------------ Gete Pay ------------------
        private async Task<IActionResult> getepaymethod(PosPaymentRequest posrequest, PosActiveEdcMachineDetails machineDetails, string action)
        {
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
            PaymentRequestResponse paymentRequestRes = new PaymentRequestResponse();

            string encKey = machineDetails.WorldlineEncKey;
            string encIv = machineDetails.WorldlineEncIv;

            string url = action switch
            {
                "status" => machineDetails.PosStatusUrl,
                "cancel" => machineDetails.PosCancelUrl,
                _ => machineDetails.PosInitiateUrl
            };

            // Decide mode based on Txntype
            string txnMode = posrequest.txntype switch
            {
                "00" => "SALE",
                "01" => "SALE-BQR",
                "02" => "CASH",
                _ => "SALE"
            };


            string actionId = posrequest.txntype switch
            {
                "00" => "1",
                "01" => "132",
                _ => "1"
            };
            object payload;

            // ✅ Build payload based on action
            if (action == "initiate")
            {
                DateTime dt = DateTime.Now;

                // Format dynamically
                string formatted = dt.ToString("ddd MMM dd HH:mm:ss 'IST' yyyy", CultureInfo.InvariantCulture);


                payload = new
                {
                    mid = machineDetails.PaytmMid,
                    amount = posrequest.amount,
                    merchantTransactionId = posrequest.transactionNo,
                    transactionDate = formatted,
                    terminalId = machineDetails.PosedcTerminalId,
                    udf1 = posrequest.mobileNumber,
                    udf2 = "",
                    udf3 = "",
                    udf4 = "",
                    udf5 = "",
                    udf6 = "",
                    udf7 = "",
                    udf8 = "",
                    udf9 = "",
                    udf10 = "",
                    ru = "http://safestaykswdc.com/success.php",
                    callbackUrl = "",
                    noQr = "1",
                    currency = "INR",
                    paymentMode = "ALL",
                    bankId = "",
                    txnType = "single",
                    productType = "IPG",
                    txnNote = "",
                    vpa = machineDetails.PosedcTerminalId//"getepay.merchant857048@axisbankmachineDetails.PosedcTerminalId
                };
            }
            else if (action == "status")
            {

                payload = new
                {
                    mid = machineDetails.PaytmMid,
                    paymentId = posrequest.refernceId,
                    referenceNo = "",
                    status = "",
                    terminalId = machineDetails.PosedcTerminalId,
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
                    invoiceNumber = posrequest.transactionNo,
                    organization_code = "Retail",
                    request_urn = "",
                    rrn = "",
                    tid = machineDetails.PosedcTerminalId,
                    tokenisedValue = "",
                    type = txnMode,
                    urn = posrequest.refernceId


                };
            }
            else
            {
                // Safe fallback payload
                payload = new
                {
                    tid = machineDetails.PosedcTerminalId,
                    amount = posrequest.amount,
                    type = txnMode,
                    referenceId = posrequest.refernceId
                };
            }

            // Serialize + Encrypt payload
            string jsonPayload = System.Text.Json.JsonSerializer.Serialize(payload);

            string encryptedPayload;
            try
            {
                encryptedPayload = GetPayAESDecryptor.EncryptHex(jsonPayload, encKey, encIv);
            }
            catch (Exception ex)
            {
                return StatusCode(200, new { error = "Encryption failed", details = ex.Message });
            }



            // Build final request body (pretty JSON for readability)
            var requestBody = new
            {
                mid = machineDetails.PaytmMid,
                terminalId = machineDetails.PosedcTerminalId,
                req = encryptedPayload
            };
            string jsonPayload1 = System.Text.Json.JsonSerializer.Serialize(requestBody);

            var content = new StringContent(
                System.Text.Json.JsonSerializer.Serialize(requestBody),
                Encoding.UTF8,
                "application/json"
            );


            var client = _httpClient.CreateClient();
            HttpResponseMessage response;
            try
            {
                response = await client.PostAsync(url, content);
            }
            catch (Exception ex)
            {
                return StatusCode(200, new { error = "Request to getepay failed", details = ex.Message });
            }

            string responseString = await response.Content.ReadAsStringAsync();

            // Determine final JSON string to parse
            string finalJson = responseString;

            try
            {
                using var doc = JsonDocument.Parse(responseString);
                if (doc.RootElement.TryGetProperty("response", out var responseElement))
                {
                    string encryptedResponse = responseElement.GetString();
                    if (!string.IsNullOrEmpty(encryptedResponse))
                    {
                        try
                        {
                            string decrypted = GetPayAESDecryptor.DecryptHex(encryptedResponse, encKey, encIv);
                            finalJson = decrypted; // overwrite with decrypted JSON payload
                        }
                        catch (Exception ex)
                        {
                            // Decryption failed, fallback to original hex string
                            finalJson = encryptedResponse;
                        }
                    }
                }

            }
            catch
            {
                // ignore → means response is already plain JSON
            }

            // Deserialize safely into JsonElement
            var jsonObj = System.Text.Json.JsonSerializer.Deserialize<JsonElement>(finalJson);

            // Map common fields
            paymentRequestRes.code = 200;

            if (action == "initiate")
            {
                // ✅ Status
                if (paymentRequestRes.code == 200 || paymentRequestRes.code == 201)
                {
                    paymentRequestRes.status = "True";
                    paymentRequestRes.message = "Payment Intitaied sucessfully";
                }
                else
                {
                    paymentRequestRes.status = "False";
                    paymentRequestRes.message = "Payment Intitaied failed";
                }

                // ✅ TransactionId
                paymentRequestRes.transactionId = posrequest.transactionNo;


                paymentRequestRes.refernceId = jsonObj.TryGetProperty("paymentId", out var urnProp)
                      ? urnProp.GetString()
                      : "";
                paymentRequestRes.PaymentUrl = jsonObj.TryGetProperty("paymentUrl", out var paymentUrl) ? paymentUrl.GetString() : "";
                paymentRequestRes.QrCodeurl = jsonObj.TryGetProperty("qrPath", out var qrPath) ? qrPath.GetString() : "";
                paymentRequestRes.cardNo = "";
                paymentRequestRes.upi = "";

            }
            else if (action == "status")
            {
                string txnStatus = jsonObj.TryGetProperty("txnStatus", out var txnProp)
                    ? txnProp.GetString()
                    : string.Empty;

                string paymentStatus = jsonObj.TryGetProperty("paymentStatus", out var payProp)
                    ? payProp.GetString()
                    : string.Empty;

                bool isSuccess =
                    txnStatus.Equals("SUCCESS", StringComparison.OrdinalIgnoreCase) &&
                    paymentStatus.Equals("SUCCESS", StringComparison.OrdinalIgnoreCase);

                if (isSuccess)
                {
                    // ✅ SUCCESS BINDINGS
                    paymentRequestRes.status = "True";
                    paymentRequestRes.code = 200;
                    paymentRequestRes.message = "Payment Successful";



                    paymentRequestRes.paymentMode = jsonObj.TryGetProperty("paymentMode", out var modeProp)
                        ? modeProp.GetString()
                        : "";


                }
                else
                {
                    // ❌ FAILED / PENDING
                    paymentRequestRes.status = "False";
                    paymentRequestRes.code = 400;
                    paymentRequestRes.message = "Payment Failed";
                }

                // 🔗 Common bindings
                paymentRequestRes.transactionId = posrequest.transactionNo;

                paymentRequestRes.refernceId = jsonObj.TryGetProperty("getepayTxnId", out var refProp)
                    ? refProp.GetString()
                    : "";

                paymentRequestRes.RRN = jsonObj.TryGetProperty("custRefNo", out var custProp)
                    ? custProp.GetString()
                    : "";

                paymentRequestRes.upi = jsonObj.TryGetProperty("udf44", out var upiProp)
                    ? upiProp.GetString()
                    : "";

                paymentRequestRes.cardNo = "";
                paymentRequestRes.PaymentUrl = "";
                paymentRequestRes.QrCodeurl = "";
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
                paymentRequestRes.transactionId = posrequest.transactionNo;
            }
            else
            {
                paymentRequestRes.status = "False";
                paymentRequestRes.message = "Unsupported action mapping";
                paymentRequestRes.transactionId = posrequest.transactionNo;
                paymentRequestRes.refernceId = posrequest.refernceId;
            }

            // Always populate these for consistency

            paymentRequestRes.paymentMode = posrequest.txntype;
            paymentRequestRes.originalResponse = finalJson; // store decrypted if available

            string apiStatus = "";
            OperationResult result;
            if (action == "initiate")
                result = await _pgdb.InsertPaylinkPosEdcMachineTransactionLogAsync(posrequest, action, payload.ToString(), finalJson, apiStatus);
            else if (action == "status")
                result = await _pgdb.UpdateStatusPaylinkPosEdcMachineTransactionLogAsync(posrequest, payload.ToString(), finalJson);
            else if (action == "cancel")
                result = await _pgdb.UpdateCancelPaylinkPosEdcMachineTransactionLogAsync(posrequest, payload.ToString(), finalJson);

            return StatusCode(200, paymentRequestRes);


        }


        // ------------------ PhonePe Provider ------------------
        private async Task<IActionResult> PhonePeMethod(
     PosPaymentRequest posRequest,
     PosActiveEdcMachineDetails machineDetails,
     string action)
        {
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;

            PaymentRequestResponse paymentResponse = new PaymentRequestResponse();

            string txnMode = posRequest.txntype switch
            {
                "00" => "CARD",
                "01" => "DQR",
                "02" => "CASH",
                _ => "UPI"
            };

            string providerId = machineDetails.PhonePeproviderId;
            string saltKey = machineDetails.PhonePeSaltKey;
            int saltIndex = Convert.ToInt32(machineDetails.PhonePeSaltIndex);
            string merchantId = machineDetails.PhonePeMerchantId;
            string terminalId = machineDetails.PosedcTerminalId;
            string storeId = machineDetails.StoreId;

            string url = string.Empty;
            string path = string.Empty;
            string body = string.Empty;

            HttpResponseMessage response;

            try
            {
                var client = _httpClient.CreateClient();
                client.DefaultRequestHeaders.Clear();
                client.DefaultRequestHeaders.Add("X-PROVIDER-ID", providerId);

                // =====================================================
                // ✅ INITIATE TRANSACTION (REQUEST ONLY)
                // =====================================================
                if (action.Equals("initiate", StringComparison.OrdinalIgnoreCase))
                {
                    url = machineDetails.PosInitiateUrl;
                    path = "/v1/edc/transaction/init";

                    long amount = ConvertRupeesToPaise(posRequest.amount);

                    var payload = new
                    {
                        merchantId,
                        storeId,
                        terminalId,
                        orderId = posRequest.transactionNo,
                        transactionId = posRequest.transactionNo,
                        amount,
                        paymentModes = new[] { txnMode },
                        timeAllowedForHandoverToTerminalSeconds = 180,
                        integrationMappingType = "ONE_TO_ONE"
                    };

                    string payloadJson = JsonConvert.SerializeObject(payload);
                    string base64Payload =
                        Convert.ToBase64String(Encoding.UTF8.GetBytes(payloadJson));

                    string verifyString = base64Payload + path + saltKey;
                    string xVerify =
                        $"{ComputeSHA256Hash(verifyString)}###{saltIndex}";

                    body = JsonConvert.SerializeObject(new { request = base64Payload });

                    client.DefaultRequestHeaders.Add("X-VERIFY", xVerify);

                    await _logService.WritePaymentLogAsync(
                        "PhonePe Initiate Request", url, body);

                    response = await client.PostAsync(
                        url,
                        new StringContent(body, Encoding.UTF8, "application/json"));

                    string responseText = await response.Content.ReadAsStringAsync();

                    // 🔹 INITIATE RESPONSE (NO STATUS DECISION)
                    paymentResponse.code = 200;
                    paymentResponse.status = "True";
                    paymentResponse.message = "Transaction initiated successfully";
                    paymentResponse.transactionId = posRequest.transactionNo;
                    paymentResponse.refernceId = posRequest.transactionNo;
                    paymentResponse.originalResponse = responseText;

                    await _pgdb.InsertPaylinkPosEdcMachineTransactionLogAsync(
                        posRequest, action, body, responseText, "INITIATED");

                    return StatusCode(200, paymentResponse);
                }

                // =====================================================
                // ✅ STATUS TRANSACTION (FINAL RESULT)
                // =====================================================
                else if (action.Equals("status", StringComparison.OrdinalIgnoreCase))
                {
                    string txnId = posRequest.transactionNo;

                    url = machineDetails.PosStatusUrl
                        .Replace("merchantId", merchantId)
                        .Replace("transactionId", txnId);

                    path = $"/v1/edc/transaction/{merchantId}/{txnId}/status";

                    string verifyString = path + saltKey;
                    string xVerify =
                        $"{ComputeSHA256Hash(verifyString)}###{saltIndex}";

                    client.DefaultRequestHeaders.Add("X-VERIFY", xVerify);

                    await _logService.WritePaymentLogAsync(
                        "PhonePe Status Request", url, "{}");

                    response = await client.GetAsync(url);

                    string responseText = await response.Content.ReadAsStringAsync();

                    paymentResponse.code = 200;
                    paymentResponse.originalResponse = responseText;
                    paymentResponse.transactionId = txnId;
                    paymentResponse.refernceId = txnId;
                    paymentResponse.status = "False";
                    paymentResponse.upi = "";
                    paymentResponse.cardNo = "";
                    paymentResponse.RRN = "";

                    var jsonObj = JsonDocument.Parse(responseText).RootElement;

                    bool apiSuccess =
                        jsonObj.TryGetProperty("success", out var s)
                        && s.GetBoolean();

                    paymentResponse.message =
                        jsonObj.TryGetProperty("message", out var m)
                        ? m.GetString()
                        : "";

                    if (!apiSuccess)
                        return StatusCode(200, paymentResponse);

                    if (!jsonObj.TryGetProperty("data", out var data)
                        || data.ValueKind == JsonValueKind.Null)
                        return StatusCode(200, paymentResponse);

                    string txnStatus =
                        data.TryGetProperty("status", out var st)
                        ? st.GetString()
                        : "";

                    // 🔹 RRN
                    paymentResponse.RRN =
                        data.TryGetProperty("referenceNumber", out var rrn)
                        ? rrn.GetString()
                        : "";

                    // ================= FINAL STATUS =================

                    if (txnStatus.Equals("SUCCESS", StringComparison.OrdinalIgnoreCase))
                    {
                        paymentResponse.status = "True";
                        paymentResponse.message = "Payment successful";

                        if (data.TryGetProperty("paymentInstruments", out var instruments)
                            && instruments.ValueKind == JsonValueKind.Array)
                        {
                            foreach (var inst in instruments.EnumerateArray())
                            {
                                string type = inst.GetProperty("type").GetString();

                                if (type == "ACCOUNT")
                                {
                                    string upiTxn =
                                        inst.TryGetProperty("upiTransactionId", out var u)
                                        ? u.GetString()
                                        : "";

                                    paymentResponse.upi = $"UPI:{upiTxn}";
                                }
                                else if (type == "CARD")
                                {
                                    string last4 =
                                        inst.TryGetProperty("last4Digits", out var l)
                                        ? l.GetString()
                                        : "";
                                    string network =
                                        inst.TryGetProperty("cardNetwork", out var n)
                                        ? n.GetString()
                                        : "";
                                    string cardType =
                                        inst.TryGetProperty("cardType", out var c)
                                        ? c.GetString()
                                        : "";

                                    paymentResponse.cardNo =
                                        $"CARD:{network}-{cardType}-{last4}";
                                }
                            }
                        }
                    }
                    else if (txnStatus.Equals("PENDING", StringComparison.OrdinalIgnoreCase))
                    {
                        paymentResponse.message = "Payment pending";
                    }
                    else if (txnStatus.Equals("EXPIRED", StringComparison.OrdinalIgnoreCase))
                    {
                        paymentResponse.message = "Transaction expired";
                    }
                    else if (txnStatus.Equals("FAILED", StringComparison.OrdinalIgnoreCase))
                    {
                        paymentResponse.message = "Payment failed";
                    }
                    else
                    {
                        paymentResponse.message = $"Unknown status: {txnStatus}";
                    }

                    await _pgdb.InsertPaylinkPosEdcMachineTransactionLogAsync(
                        posRequest, action, "{}", responseText, paymentResponse.status);

                    return StatusCode(200, paymentResponse);
                }

                else
                {
                    return StatusCode(200,
                        new { error = "Invalid action. Use initiate or status." });
                }
            }
            catch (Exception ex)
            {
                return StatusCode(500,
                    new { error = "PhonePe request failed", details = ex.Message });
            }
        }

        // ------------------ Convert Rupees To Paise ------------------
        public long ConvertRupeesToPaise(string inputAmount)
        {
            if (!decimal.TryParse(inputAmount, out decimal amount))
                throw new ArgumentException("Invalid amount format");

            amount = decimal.Round(amount, 2, MidpointRounding.AwayFromZero);

            if ((amount * 100) % 1 != 0)
                throw new ArgumentException("Amount can have at most 2 decimal places");

            long amountInPaise = (long)(amount * 100);
            return amountInPaise;
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

        private static string ComputeSHA256Hash(string rawData)
        {
            using (var sha256 = SHA256.Create())
            {
                byte[] bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(rawData));
                StringBuilder builder = new StringBuilder();
                foreach (var b in bytes)
                    builder.Append(b.ToString("x2"));
                return builder.ToString();
            }
        }
    }

}
