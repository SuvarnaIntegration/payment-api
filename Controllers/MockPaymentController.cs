using Azure.Core;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json.Linq;
using Npgsql.Internal.Postgres;
using PaymentAPI.Models;
using PaymentAPI.Services;
using Paytm;
using System.Globalization;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.NetworkInformation;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace PaymentAPI.Controllers
{
    [Route("mockpay")]
    [ApiController]
    public class MockPaymentController : ControllerBase
    {
        private readonly IConfiguration _config;
        private readonly JwtTokenService _jwtService;
        private string _bearerToken;

        public MockPaymentController(IConfiguration config, JwtTokenService jwtService)
        {
            _config = config;
            _jwtService = jwtService;
        }

        [HttpPost("authtoken")]
        [AllowAnonymous]
        public IActionResult authtoken([FromBody] authtokenprop model)
        {
            if (model is null)
                return StatusCode(200, new { code = 200, status = "false", message = "Request body required." });

            var section = _config.GetSection("Jwt");
            string UserName = section["UserName"];
            string Password = section["Password"];

            if (string.IsNullOrWhiteSpace(model.UserName) || string.IsNullOrWhiteSpace(model.Password))
                return StatusCode(200, new { code = 200, status = "false", message = "UserName and Password are required." });

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
                status = "false",
                message = "Please check User Name and Password..."
            });
        }

        [HttpPost("pushtrans")]
        [Authorize]
        public IActionResult PostPayment([FromBody] PosPaymentRequest request)
        {
            if (request is null)
                return StatusCode(200, new { code = 200, status = "false", message = "Request body required." });

            var provider = request.provider ?? string.Empty;

            if (string.Equals(provider, "pinelab", StringComparison.OrdinalIgnoreCase))
            {
                return Ok(new
                {
                    code = 200,
                    status = "True",
                    transactionId = "TXN12345678",
                    refernceId = "ABC1234567",
                    paymentMode = "00",
                    upi = "",
                    cardNo = "",
                    rrn = "",
                    Orginalcode = "00",
                    OrginalMessage = "Successs",
                    QrCodeurl = "",
                    PaymentUrl = "",
                    originalResponse = ""
                });
            }

            return StatusCode(200, new
            {
                code = 200,
                status = "False",
                transactionId = "TXN12345678",
                refernceId = "",
                paymentMode = "01",
                upi = "",
                cardNo = "",
                rrn = "",
                Orginalcode = "00",
                OrginalMessage = "failed",
                QrCodeurl = "",
                PaymentUrl = "",
                originalResponse = ""
            });
        }

        [HttpPost("transstatus")]
        [Authorize]
        public IActionResult CheckPaymentStatus([FromBody] PosPaymentRequest request)
        {
            if (request is null)
                return StatusCode(200, new { code = 200, status = "false", message = "Request body required." });

            var provider = request.provider ?? string.Empty;
            var refId = request.refernceId ?? string.Empty;

            if (string.Equals(provider, "pinelab", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(refId))
            {
                return Ok(new
                {
                    code = 200,
                    status = "True",
                    transactionId = "TXN12345678",
                    refernceId = "ABC1234567",
                    paymentMode = "00",
                    upi = "ravi@ybl",
                    cardNo = "858263141501",
                    rrn = "858263141501",
                    Orginalcode = "00",
                    OrginalMessage = "Successs",
                    QrCodeurl = "",
                    PaymentUrl = "",
                    originalResponse = ""
                });
            }

            return StatusCode(200, new
            {
                code = 200,
                status = "False",
                message = "Payment failed",
                transactionid = "TXN1234567890",
                refernceid = "",
                cardNo = "",
                rrn = "",
                Orginalcode = "00",
                OrginalMessage = "",
                upi = "",
                paymentMode = "00",
                qrCodeurl = "",
                paymentUrl = "",
                originalResponse = ""
            });
        }
    }
}