using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using PaymentAPI.Models;
using PaymentAPI.Services;
using LoginRequest = PaymentAPI.Models.LoginRequest;

namespace PaymentAPI.Controllers
{
    [Route("supay")]
    //[ApiController]
    public class LoginController : ControllerBase
    {
        private readonly IConfiguration _config;
        private readonly PostgresDatabaseService _pgdb;
        private readonly string _databaseProvider;
        private readonly LogService _logService;

        public LoginController(IConfiguration config, LogService logService, PostgresDatabaseService pgdb)
        {
            _config = config;
            _logService = logService;
            _pgdb = pgdb;
            _databaseProvider = config["DatabaseProvider"];
        }

        [HttpPost("login")]
        public async Task<IActionResult> Postlogin([FromBody] LoginRequest _login)
        {
            try
            {
                if (_login is null)
                    return StatusCode(200, new { code = 200, status = "false", message = "Request body is required." });

                if (string.IsNullOrEmpty(_login.UserName))
                    return StatusCode(200, new { code = 200, status = "false", message = "User Name is required.." });

                if (string.IsNullOrEmpty(_login.Password))
                    return StatusCode(200, new { code = 200, status = "false", message = "Password is required.." });

                // Use Postgres service (SqlServer service removed/not implemented)
                var result = await _pgdb.ValidateUserAsync(_login);

                return StatusCode(200, new
                {
                    code = result.code,
                    status = result.status,
                    message = result.message,
                    UserId = result.UserId,
                    USERNAME = result.USERNAME,
                    USERCODE = result.USERCODE,
                    DISPLAYNAME = result.DISPLAYNAME,
                    HOSPITALCD = result.HOSPITALCD,
                    HOSPITALNAME = result.HOSPITALNAME,
                    USERTYPE = result.USERTYPE,
                    COSTCENTERCD = result.COSTCENTERCD,
                    COMPANYID = result.COMPANYID
                });
            }
            catch (Exception ex)
            {
                _logService?.GetType(); // keep a guarded reference to avoid unused warning; replace with real logging if available
                return StatusCode(200, new
                {
                    code = 200,
                    status = "false",
                    message = ex.Message
                });
            }
        }
    }
}