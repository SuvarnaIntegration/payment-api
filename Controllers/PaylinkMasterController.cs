using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using PaymentAPI.Models;
using PaymentAPI.Services;

namespace PaymentAPI.Controllers
{
    [Route("supay")]
    [ApiController]
    public class PaylinkMasterController : ControllerBase
    {
        private readonly IConfiguration _config;
        private readonly PostgresDatabaseService _pgdb;
        private readonly IHttpClientFactory _httpClient;
        private readonly LogService _logService;
        private readonly string _databaseProvider;
        public PaylinkMasterController(IConfiguration config, IHttpClientFactory httpClientFactory, LogService logService, PostgresDatabaseService pgdb)
        {
            _pgdb = pgdb;
            _config = config;
            _logService = logService;
            _httpClient = httpClientFactory;
            _databaseProvider = config["DatabaseProvider"];
        }
        [HttpGet("company-code")]
        public async Task<IActionResult> GetCompanycodeMasterConfig()
        {
            try
            {



                // 🔹 Decide database provider
                if (_databaseProvider.Equals("postgres", StringComparison.OrdinalIgnoreCase))
                {
                    var result = await _pgdb.GenerateHospitalCodeAsync();

                    return StatusCode(200, new
                    {
                        code = result.code,
                        status = result.status,
                        message = result.message,
                        autocode = result.autocode
                    });
                }
                else if (_databaseProvider.Equals("sql", StringComparison.OrdinalIgnoreCase))
                {
                    var result = await _pgdb.GenerateHospitalCodeAsync();

                    return StatusCode(200, new
                    {
                        code = result.code,
                        status = result.status,
                        message = result.message,
                        autocode = result.autocode
                    });
                }
                else
                {
                    return StatusCode(200, new
                    {
                        code = 200,
                        status = "false",
                        message = "Unknown Error"
                    });
                }
            }
            catch (Exception ex)
            {
                // 🔹 Catch-all fallback
                return StatusCode(200, new
                {
                    code = 200,
                    status = "false",
                    message = ex.Message
                });
            }
        }

        [HttpGet("company-dtls")]
        public async Task<IActionResult> GetAllCompanys()
        {
            try
            {



                // 🔹 Decide database provider
                if (_databaseProvider.Equals("postgres", StringComparison.OrdinalIgnoreCase))
                {
                    var result = await _pgdb.GetAllPaylinkCompaniesAsync();

                    return StatusCode(200, new
                    {
                        code = "200",
                        status = "true",
                        message = "Successfully fetch Hospital Details",
                        data = result
                    });
                }
                else if (_databaseProvider.Equals("sql", StringComparison.OrdinalIgnoreCase))
                {
                    var result = await _pgdb.GetAllPaylinkCompaniesAsync();

                    return StatusCode(200, new
                    {
                        code = "200",
                        status = "true",
                        message = "Successfully fetch Hospital Details",
                        data = result
                    });
                }
                else
                {
                    return StatusCode(200, new
                    {
                        code = 200,
                        status = "false",
                        message = "Unknown Error"
                    });
                }
            }
            catch (Exception ex)
            {
                // 🔹 Catch-all fallback
                return StatusCode(200, new
                {
                    code = 200,
                    status = "false",
                    message = ex.Message
                });
            }
        }

        [HttpPost("company-ins-config")]
        public async Task<IActionResult> PostCompanySave([FromBody] CompanySettings company)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(company.CompanyCd))
                    return Ok(new { code = 200, status = "false", message = "Company Code is required." });

                if (string.IsNullOrWhiteSpace(company.CompanyName))
                    return Ok(new { code = 200, status = "false", message = "Company Name is required." });

                var result = await _pgdb.InsertPaylinkCompanyAsync(company);

                return Ok(new
                {
                    code = result.code,
                    status = result.status,
                    message = result.message
                });
            }
            catch (Exception ex)
            {
                return Ok(new
                {
                    code = "200",
                    status = "false",
                    message = ex.Message
                });
            }
        }




        [HttpGet("location-code")]
        public async Task<IActionResult> GetLocationcodeMasterConfig()
        {
            try
            {



                // 🔹 Decide database provider
                if (_databaseProvider.Equals("postgres", StringComparison.OrdinalIgnoreCase))
                {
                    var result = await _pgdb.GenerateLocationCodeAsync();

                    return StatusCode(200, new
                    {
                        code = result.code,
                        status = result.status,
                        message = result.message,
                        autocode = result.autocode
                    });
                }
                else if (_databaseProvider.Equals("sql", StringComparison.OrdinalIgnoreCase))
                {
                    var result = await _pgdb.GenerateLocationCodeAsync();

                    return StatusCode(200, new
                    {
                        code = result.code,
                        status = result.status,
                        message = result.message,
                        autocode = result.autocode
                    });
                }
                else
                {
                    return StatusCode(200, new
                    {
                        code = 200,
                        status = "false",
                        message = "Unknown Error"
                    });
                }
            }
            catch (Exception ex)
            {
                // 🔹 Catch-all fallback
                return StatusCode(200, new
                {
                    code = 200,
                    status = "false",
                    message = ex.Message
                });
            }
        }

        [HttpGet("active-company-dtls")]
        public async Task<IActionResult> GetAllActiveCompanys()
        {
            try
            {



                // 🔹 Decide database provider
                if (_databaseProvider.Equals("postgres", StringComparison.OrdinalIgnoreCase))
                {
                    var result = await _pgdb.GetAllActivePaylinkCompaniesAsync();

                    return StatusCode(200, new
                    {
                        code = "200",
                        status = "true",
                        message = "Successfully fetch Hospital Details",
                        data = result
                    });
                }
                else if (_databaseProvider.Equals("sql", StringComparison.OrdinalIgnoreCase))
                {
                    var result = await _pgdb.GetAllPaylinkCompaniesAsync();

                    return StatusCode(200, new
                    {
                        code = "200",
                        status = "true",
                        message = "Successfully fetch Hospital Details",
                        data = result
                    });
                }
                else
                {
                    return StatusCode(200, new
                    {
                        code = 200,
                        status = "false",
                        message = "Unknown Error"
                    });
                }
            }
            catch (Exception ex)
            {
                // 🔹 Catch-all fallback
                return StatusCode(200, new
                {
                    code = 200,
                    status = "false",
                    message = ex.Message
                });
            }
        }

        [HttpGet("location-dtls/{companyId}")]
        public async Task<IActionResult> GetAllLocations(int companyId)
        {
            try
            {
                var result = await _pgdb.GetAllPaylinkLocationsAsync(companyId);

                return StatusCode(200, new
                {
                    code = "200",
                    status = "true",
                    message = "Successfully fetched Location Details",
                    data = result
                });
            }
            catch (Exception ex)
            {
                return StatusCode(200, new
                {
                    code = "200",
                    status = "false",
                    message = ex.Message
                });
            }
        }


        [HttpGet("company-location-dtls")]
        public async Task<IActionResult> GetCompanyAgainestLocations(string company_id)
        {
            try
            {



                // 🔹 Decide database provider
                if (_databaseProvider.Equals("postgres", StringComparison.OrdinalIgnoreCase))
                {
                    var result = await _pgdb.GetAllPaylinkCompanyAgainstLocationsAsync(company_id);

                    return StatusCode(200, new
                    {
                        code = "200",
                        status = "true",
                        message = "Successfully fetch Location Details",
                        data = result
                    });
                }
                else if (_databaseProvider.Equals("sql", StringComparison.OrdinalIgnoreCase))
                {
                    var result = await _pgdb.GetAllPaylinkCompanyAgainstLocationsAsync(company_id);

                    return StatusCode(200, new
                    {
                        code = "200",
                        status = "true",
                        message = "Successfully fetch Hospital Details",
                        data = result
                    });
                }
                else
                {
                    return StatusCode(200, new
                    {
                        code = 200,
                        status = "false",
                        message = "Unknown Error"
                    });
                }
            }
            catch (Exception ex)
            {
                // 🔹 Catch-all fallback
                return StatusCode(200, new
                {
                    code = 200,
                    status = "false",
                    message = ex.Message
                });
            }
        }

        [HttpPost("location-ins-config")]
        public async Task<IActionResult> SaveLocation([FromBody] LocationSettings loc)
        {
            try
            {
                if (string.IsNullOrEmpty(loc.LocationCd))
                    return Ok(new { code = 200, status = "false", message = "Location Code is required." });

                if (string.IsNullOrEmpty(loc.LocationName))
                    return Ok(new { code = 200, status = "false", message = "Location Name is required." });

                var result = await _pgdb.InsertPaylinklocationAsync(loc);

                return Ok(result);
            }
            catch (Exception ex)
            {
                return Ok(new { code = "200", status = "false", message = ex.Message });
            }
        }

        [HttpPost("location-upd-config")]
        public async Task<IActionResult> PostLocationUpdateMasterConfig([FromBody] LocationSettings _loc)
        {
            try
            {
                // 🔹 Basic validation
                if (string.IsNullOrEmpty(_loc.LocationCd))
                    return StatusCode(200, new { code = 200, status = "false", message = "Location Code is required.." });

                if (string.IsNullOrEmpty(_loc.LocationName))
                    return StatusCode(200, new { code = 200, status = "false", message = "Location Name is required.." });

                // 🔹 Decide database provider
                if (_databaseProvider.Equals("postgres", StringComparison.OrdinalIgnoreCase))
                {
                    var result = await _pgdb.UpdatePaylinkLocationAsync(
                        _loc
                    );

                    return StatusCode(200, new
                    {
                        code = result.code,
                        status = result.status,
                        message = result.message
                    });
                }
                else if (_databaseProvider.Equals("sql", StringComparison.OrdinalIgnoreCase))
                {
                    var result = await _pgdb.UpdatePaylinkLocationAsync(
                      _loc
                    );

                    return StatusCode(200, new
                    {
                        code = result.code,
                        status = result.status,
                        message = result.message
                    });
                }
                else
                {
                    return StatusCode(200, new
                    {
                        code = 200,
                        status = "false",
                        message = "Unknown Error"
                    });
                }
            }
            catch (Exception ex)
            {
                // 🔹 Catch-all fallback
                return StatusCode(200, new
                {
                    code = 200,
                    status = "false",
                    message = ex.Message
                });
            }
        }

        [HttpGet("provider-code")]
        public async Task<IActionResult> GetProvidercodeMasterConfig()
        {
            try
            {


                // 🔹 Decide database provider
                if (_databaseProvider.Equals("postgres", StringComparison.OrdinalIgnoreCase))
                {
                    var result = await _pgdb.GenerateProviderCodeAsync();

                    return StatusCode(200, new
                    {
                        code = result.code,
                        status = result.status,
                        message = result.message,
                        autocode = result.autocode
                    });
                }
                else if (_databaseProvider.Equals("sql", StringComparison.OrdinalIgnoreCase))
                {
                    var result = await _pgdb.GenerateProviderCodeAsync();

                    return StatusCode(200, new
                    {
                        code = result.code,
                        status = result.status,
                        message = result.message,
                        autocode = result.autocode
                    });
                }
                else
                {
                    return StatusCode(200, new
                    {
                        code = 200,
                        status = "false",
                        message = "Unknown Error"
                    });
                }
            }
            catch (Exception ex)
            {
                // 🔹 Catch-all fallback
                return StatusCode(200, new
                {
                    code = 200,
                    status = "false",
                    message = ex.Message
                });
            }
        }

        [HttpGet("provider-dtls")]
        public async Task<IActionResult> GetAllProviders()
        {
            try
            {
                // 🔹 Decide database provider
                if (_databaseProvider.Equals("postgres", StringComparison.OrdinalIgnoreCase))
                {
                    var result = await _pgdb.GetAllPaylinkProvidersAsync();

                    return StatusCode(200, new
                    {
                        code = "200",
                        status = "true",
                        message = "Successfully fetch Provider Details",
                        data = result
                    });
                }
                else if (_databaseProvider.Equals("sql", StringComparison.OrdinalIgnoreCase))
                {
                    var result = await _pgdb.GetAllPaylinkProvidersAsync();

                    return StatusCode(200, new
                    {
                        code = "200",
                        status = "true",
                        message = "Successfully fetch Provider Details",
                        data = result
                    });
                }
                else
                {
                    return StatusCode(200, new
                    {
                        code = 200,
                        status = "false",
                        message = "Unknown Error"
                    });
                }
            }
            catch (Exception ex)
            {
                // 🔹 Catch-all fallback
                return StatusCode(200, new
                {
                    code = 200,
                    status = "false",
                    message = ex.Message
                });
            }
        }

        [HttpGet("active_provider-dtls")]
        public async Task<IActionResult> GetActiveAllProviders()
        {
            try
            {
                // 🔹 Decide database provider
                if (_databaseProvider.Equals("postgres", StringComparison.OrdinalIgnoreCase))
                {
                    var result = await _pgdb.GetActivePaylinkProvidersAsync();

                    return StatusCode(200, new
                    {
                        code = "200",
                        status = "true",
                        message = "Successfully fetch Provider Details",
                        data = result
                    });
                }
                else if (_databaseProvider.Equals("sql", StringComparison.OrdinalIgnoreCase))
                {
                    var result = await _pgdb.GetActivePaylinkProvidersAsync();

                    return StatusCode(200, new
                    {
                        code = "200",
                        status = "true",
                        message = "Successfully fetch Provider Details",
                        data = result
                    });
                }
                else
                {
                    return StatusCode(200, new
                    {
                        code = 200,
                        status = "false",
                        message = "Unknown Error"
                    });
                }
            }
            catch (Exception ex)
            {
                // 🔹 Catch-all fallback
                return StatusCode(200, new
                {
                    code = 200,
                    status = "false",
                    message = ex.Message
                });
            }
        }
        [HttpPost("provider-ins-config")]
        public async Task<IActionResult> PostProviderInsertMasterConfig([FromBody] ProviderSettings prov)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(prov.ProviderCd))
                    return Ok(new { code = 200, status = "false", message = "Provider Code is required." });

                if (string.IsNullOrWhiteSpace(prov.ProviderName))
                    return Ok(new { code = 200, status = "false", message = "Provider Name is required." });

                var result = await _pgdb.InsertPaylinkProviderAsync(prov);

                return Ok(new
                {
                    code = result.code,
                    status = result.status,
                    message = result.message
                });
            }
            catch (Exception ex)
            {
                return Ok(new
                {
                    code = 200,
                    status = "false",
                    message = ex.Message
                });
            }
        }


        [HttpPost("provider-upd-config")]
        public async Task<IActionResult> PostProviderUpdateMasterConfig([FromBody] ProviderSettings prov)
        {
            try
            {
                // 🔹 Basic validation
                if (string.IsNullOrEmpty(prov.ProviderCd))
                    return StatusCode(200, new { code = 200, status = "false", message = "Provider Code is required.." });

                if (string.IsNullOrEmpty(prov.ProviderName))
                    return StatusCode(200, new { code = 200, status = "false", message = "Provider Name is required.." });

                // 🔹 Decide database provider
                if (_databaseProvider.Equals("postgres", StringComparison.OrdinalIgnoreCase))
                {
                    var result = await _pgdb.UpdatePaylinkProviderAsync(prov);

                    return StatusCode(200, new
                    {
                        code = result.code,
                        status = result.status,
                        message = result.message
                    });
                }
                else if (_databaseProvider.Equals("sql", StringComparison.OrdinalIgnoreCase))
                {
                    var result = await _pgdb.UpdatePaylinkProviderAsync(prov);
                    return StatusCode(200, new
                    {
                        code = result.code,
                        status = result.status,
                        message = result.message
                    });
                }
                else
                {
                    return StatusCode(200, new
                    {
                        code = 200,
                        status = "false",
                        message = "Unknown Error"
                    });
                }
            }
            catch (Exception ex)
            {
                // 🔹 Catch-all fallback
                return StatusCode(200, new
                {
                    code = 200,
                    status = "false",
                    message = ex.Message
                });
            }
        }

        [HttpGet("provider-account-code")]
        public async Task<IActionResult> GetProviderAccountcodeMasterConfig()
        {
            try
            {



                // 🔹 Decide database provider
                if (_databaseProvider.Equals("postgres", StringComparison.OrdinalIgnoreCase))
                {
                    var result = await _pgdb.GenerateProviderAccountsCodeAsync();

                    return StatusCode(200, new
                    {
                        code = result.code,
                        status = result.status,
                        message = result.message,
                        autocode = result.autocode
                    });
                }
                else if (_databaseProvider.Equals("sql", StringComparison.OrdinalIgnoreCase))
                {
                    var result = await _pgdb.GenerateProviderAccountsCodeAsync();

                    return StatusCode(200, new
                    {
                        code = result.code,
                        status = result.status,
                        message = result.message,
                        autocode = result.autocode
                    });
                }
                else
                {
                    return StatusCode(200, new
                    {
                        code = 200,
                        status = "false",
                        message = "Unknown Error"
                    });
                }
            }
            catch (Exception ex)
            {
                // 🔹 Catch-all fallback
                return StatusCode(200, new
                {
                    code = 200,
                    status = "false",
                    message = ex.Message
                });
            }
        }

        [HttpPost("provider-account-ins-config")]
        public async Task<IActionResult> PostProviderAccountInsertMasterConfig([FromBody] PaylinkProviderAccount prov)
        {
            try
            {
                // 🔹 Basic validation
                if (string.IsNullOrEmpty(prov.ProviderAccountCd))
                    return StatusCode(200, new { code = 200, status = "false", message = "Provider Code is required.." });

                if (string.IsNullOrEmpty(prov.AccountName))
                    return StatusCode(200, new { code = 200, status = "false", message = "Provider Name is required.." });

                // 🔹 Decide database provider
                if (_databaseProvider.Equals("postgres", StringComparison.OrdinalIgnoreCase))
                {
                    var result = await _pgdb.InsertPaylinkProviderAccountAsync(prov);

                    return StatusCode(200, new
                    {
                        code = result.code,
                        status = result.status,
                        message = result.message
                    });
                }
                else if (_databaseProvider.Equals("sql", StringComparison.OrdinalIgnoreCase))
                {
                    var result = await _pgdb.InsertPaylinkProviderAccountAsync(prov);

                    return StatusCode(200, new
                    {
                        code = result.code,
                        status = result.status,
                        message = result.message
                    });
                }
                else
                {
                    return StatusCode(200, new
                    {
                        code = 200,
                        status = "false",
                        message = "Unknown Error"
                    });
                }
            }
            catch (Exception ex)
            {
                // 🔹 Catch-all fallback
                return StatusCode(200, new
                {
                    code = 200,
                    status = "false",
                    message = ex.Message
                });
            }
        }

        [HttpPost("provider-account-upd-config")]
        public async Task<IActionResult> PostProviderAccountUpdateMasterConfig([FromBody] PaylinkProviderAccount prov)
        {
            try
            {
                // 🔹 Basic validation
                if (string.IsNullOrEmpty(prov.ProviderAccountCd))
                    return StatusCode(200, new { code = 200, status = "false", message = "Provider Account Code is required.." });

                if (string.IsNullOrEmpty(prov.AccountName))
                    return StatusCode(200, new { code = 200, status = "false", message = "Account Name is required.." });

                // 🔹 Decide database provider
                if (_databaseProvider.Equals("postgres", StringComparison.OrdinalIgnoreCase))
                {
                    var result = await _pgdb.UpdatePaylinkProviderAccountAsync(prov);

                    return StatusCode(200, new
                    {
                        code = result.code,
                        status = result.status,
                        message = result.message
                    });
                }
                else if (_databaseProvider.Equals("sql", StringComparison.OrdinalIgnoreCase))
                {
                    var result = await _pgdb.UpdatePaylinkProviderAccountAsync(prov);
                    return StatusCode(200, new
                    {
                        code = result.code,
                        status = result.status,
                        message = result.message
                    });
                }
                else
                {
                    return StatusCode(200, new
                    {
                        code = 200,
                        status = "false",
                        message = "Unknown Error"
                    });
                }
            }
            catch (Exception ex)
            {
                // 🔹 Catch-all fallback
                return StatusCode(200, new
                {
                    code = 200,
                    status = "false",
                    message = ex.Message
                });
            }
        }

        [HttpGet("provider-accounts-dtls")]
        public async Task<IActionResult> GetAllProviderAccounts([FromQuery] int companyId, [FromQuery] int? locationId = null, [FromQuery] int? providerId = null)
       {
            try
            {
                if (!_databaseProvider.Equals("postgres", StringComparison.OrdinalIgnoreCase))
                {
                    return StatusCode(200, new
                    {
                        code = "200",
                        status = "false",
                        message = "Unsupported database provider"
                    });
                }

                var result = await _pgdb.GetAllPaylinkProviderAccountsAsync(companyId, locationId, providerId);

                return StatusCode(200, new
                {
                    code = "200",
                    status = "true",
                    message = "Successfully fetched provider account details",
                    data = result
                });
            }
            catch (Exception ex)
            {
                return StatusCode(200, new
                {
                    code = "200",
                    status = "false",
                    message = ex.Message
                });
            }
        }

        [HttpPost("provider-account-active-inactive")]
        public async Task<IActionResult> ActiveInactiveProviderAccount([FromBody] ProviderAccountStatusRequest req)
        {
            try
            {
                var result = await _pgdb.ActiveInactiveProviderAccountAsync(
                    req.ProviderAccountId, req.RecordStatus, req.ModifyBy);

                return StatusCode(200, new
                {
                    code = result.Code,
                    status = result.Status,
                    message = result.Message
                });
            }
            catch (Exception Ex)
            {
                return StatusCode(200, new
                {
                    code = "200",
                    status = "false",
                    message = Ex.Message.ToString()
                });
            }
        }

        [HttpGet("posedc-code")]
        public async Task<IActionResult> GetPosMachinecodeMasterConfig()
        {
            try
            {



                // 🔹 Decide database provider
                if (_databaseProvider.Equals("postgres", StringComparison.OrdinalIgnoreCase))
                {
                    var result = await _pgdb.GeneratePosMachineCodeAsync();

                    return StatusCode(200, new
                    {
                        code = result.code,
                        status = result.status,
                        message = result.message,
                        autocode = result.autocode
                    });
                }
                else if (_databaseProvider.Equals("sql", StringComparison.OrdinalIgnoreCase))
                {
                    var result = await _pgdb.GeneratePosMachineCodeAsync();

                    return StatusCode(200, new
                    {
                        code = result.code,
                        status = result.status,
                        message = result.message,
                        autocode = result.autocode
                    });
                }
                else
                {
                    return StatusCode(200, new
                    {
                        code = 200,
                        status = "false",
                        message = "Unknown Error"
                    });
                }
            }
            catch (Exception ex)
            {
                // 🔹 Catch-all fallback
                return StatusCode(200, new
                {
                    code = 200,
                    status = "false",
                    message = ex.Message
                });
            }
        }

        [HttpGet("provider-accounts-by-company")]
        public async Task<IActionResult> GetProviderAccountsByCompany([FromQuery] int companyId)
        {
            try
            {
                if (companyId <= 0)
                {
                    return StatusCode(200, new
                    {
                        code = "400",
                        status = "false",
                        message = "Invalid companyId"
                    });
                }

                var result = await _pgdb.GetProviderAccountsByCompanyAsync(companyId);

                return StatusCode(200, new
                {
                    code = result.Code,
                    status = result.Status,
                    message = result.Message,
                    data = result.Data
                });
            }
            catch (Exception ex)
            {
                return StatusCode(200, new
                {
                    code = "500",
                    status = "false",
                    message = ex.Message
                });
            }
        }

        [HttpPost("posedc-ins-config")]
        public async Task<IActionResult> PostPosEdcInsertMasterConfig([FromBody] PosEdcMachineSettings _posedc)
        {
            try
            {
                // 🔹 Basic validation
                if (string.IsNullOrEmpty(_posedc.posEdcMachineCd))
                    return StatusCode(200, new { code = 200, status = "false", message = "Pos Edc Code is required.." });

                if (string.IsNullOrEmpty(_posedc.posEdcMachineName))
                    return StatusCode(200, new { code = 200, status = "false", message = "Pos Edc Machine Name is required.." });

                // 🔹 Decide database provider
                if (_databaseProvider.Equals("postgres", StringComparison.OrdinalIgnoreCase))
                {
                    var result = await _pgdb.InsertPaylinkPosEdcMachineAsync(_posedc);

                    return StatusCode(200, new
                    {
                        code = result.code,
                        status = result.status,
                        message = result.message
                    });
                }
                else if (_databaseProvider.Equals("sql", StringComparison.OrdinalIgnoreCase))
                {
                    var result = await _pgdb.InsertPaylinkPosEdcMachineAsync(_posedc);

                    return StatusCode(200, new
                    {
                        code = result.code,
                        status = result.status,
                        message = result.message
                    });
                }
                else
                {
                    return StatusCode(200, new
                    {
                        code = 200,
                        status = "false",
                        message = "Unknown Error"
                    });
                }
            }
            catch (Exception ex)
            {
                // 🔹 Catch-all fallback
                return StatusCode(200, new
                {
                    code = 200,
                    status = "false",
                    message = ex.Message
                });
            }
        }

        [HttpPost("posedc-upd-config")]
        public async Task<IActionResult> PostPosEdcUpdateMasterConfig([FromBody] PosEdcMachineSettings _posedc)
        {
            try
            {
                // 🔹 Basic validation
                if (string.IsNullOrEmpty(_posedc.posEdcMachineCd))
                    return StatusCode(200, new { code = 200, status = "false", message = "Pos Edc Code is required.." });

                if (string.IsNullOrEmpty(_posedc.posEdcMachineName))
                    return StatusCode(200, new { code = 200, status = "false", message = "Pos Edc Machine Name is required.." });

                // 🔹 Decide database provider
                if (_databaseProvider.Equals("postgres", StringComparison.OrdinalIgnoreCase))
                {
                    var result = await _pgdb.UpdatePaylinkPosEdcMachineAsync(_posedc);

                    return StatusCode(200, new
                    {
                        code = result.code,
                        status = result.status,
                        message = result.message
                    });
                }
                else if (_databaseProvider.Equals("sql", StringComparison.OrdinalIgnoreCase))
                {
                    var result = await _pgdb.UpdatePaylinkPosEdcMachineAsync(_posedc);

                    return StatusCode(200, new
                    {
                        code = result.code,
                        status = result.status,
                        message = result.message
                    });
                }
                else
                {
                    return StatusCode(200, new
                    {
                        code = 200,
                        status = "false",
                        message = "Unknown Error"
                    });
                }
            }
            catch (Exception ex)
            {
                // 🔹 Catch-all fallback
                return StatusCode(200, new
                {
                    code = 200,
                    status = "false",
                    message = ex.Message
                });
            }
        }

        [HttpGet("cmp-loc-provider-dtls")]
        public async Task<IActionResult> GetPaylinkCompanyLocationAgainstProvidersAsync(string company_id, string location_id)
        {
            try
            {



                // 🔹 Decide database provider
                if (_databaseProvider.Equals("postgres", StringComparison.OrdinalIgnoreCase))
                {
                    var result = await _pgdb.GetAllPaylinkCompanyLocationAgainstProvidersAsync(company_id, location_id);

                    return StatusCode(200, new
                    {
                        code = "200",
                        status = "true",
                        message = "Successfully fetch Provider Details",
                        data = result
                    });
                }
                else if (_databaseProvider.Equals("sql", StringComparison.OrdinalIgnoreCase))
                {
                    var result = await _pgdb.GetAllPaylinkCompanyLocationAgainstProvidersAsync(company_id, location_id);

                    return StatusCode(200, new
                    {
                        code = "200",
                        status = "true",
                        message = "Successfully fetch Provider Details",
                        data = result
                    });
                }
                else
                {
                    return StatusCode(200, new
                    {
                        code = 200,
                        status = "false",
                        message = "Unknown Error"
                    });
                }
            }
            catch (Exception ex)
            {
                // 🔹 Catch-all fallback
                return StatusCode(200, new
                {
                    code = 200,
                    status = "false",
                    message = ex.Message
                });
            }
        }

        [HttpGet("cmp-loc-prov-account-dtls")]
        public async Task<IActionResult> GetPaylinkCompanyandLocationsprovaccountsAsync(string company_id, string location_id, string provider_id)
        {
            try
            {



                // 🔹 Decide database provider
                if (_databaseProvider.Equals("postgres", StringComparison.OrdinalIgnoreCase))
                {
                    var result = await _pgdb.GetAllPaylinkCompanyandLocationsprovaccountsAsync(company_id, location_id, provider_id);

                    return StatusCode(200, new
                    {
                        code = "200",
                        status = "true",
                        message = "Successfully fetch provider accounts Details",
                        data = result
                    });
                }
                else if (_databaseProvider.Equals("sql", StringComparison.OrdinalIgnoreCase))
                {
                    var result = await _pgdb.GetAllPaylinkCompanyandLocationsprovaccountsAsync(company_id, location_id, provider_id);

                    return StatusCode(200, new
                    {
                        code = "200",
                        status = "true",
                        message = "Successfully fetch provider accounts Details",
                        data = result
                    });
                }
                else
                {
                    return StatusCode(200, new
                    {
                        code = 200,
                        status = "false",
                        message = "Unknown Error"
                    });
                }
            }
            catch (Exception ex)
            {
                // 🔹 Catch-all fallback
                return StatusCode(200, new
                {
                    code = 200,
                    status = "false",
                    message = ex.Message
                });
            }
        }

        [HttpGet("pos-edc-dtls")]
        public async Task<IActionResult> GetAllPosMachineList(int companyId)
        {
            try
            {
                // 🔹 Decide database provider
                if (_databaseProvider.Equals("postgres", StringComparison.OrdinalIgnoreCase))
                {
                    var result = await _pgdb.GetAllPaylinkPosEdcMachineListAsync(companyId);

                    return StatusCode(200, new
                    {
                        code = "200",
                        status = "true",
                        message = "Successfully fetch Pos Edc Machine Details",
                        data = result
                    });
                }
                else if (_databaseProvider.Equals("sql", StringComparison.OrdinalIgnoreCase))
                {
                    var result = await _pgdb.GetAllPaylinkPosEdcMachineListAsync(companyId);

                    return StatusCode(200, new
                    {
                        code = "200",
                        status = "true",
                        message = "Successfully fetch Pos Edc Machine Details",
                        data = result
                    });
                }
                else
                {
                    return StatusCode(200, new
                    {
                        code = 200,
                        status = "false",
                        message = "Unknown Error"
                    });
                }
            }
            catch (Exception ex)
            {
                // 🔹 Catch-all fallback
                return StatusCode(200, new
                {
                    code = 200,
                    status = "false",
                    message = ex.Message
                });
            }
        }

        [HttpPost("save-edc-machine")]
        public async Task<IActionResult> SaveEdcMachine([FromBody] EdcMachineRequest model)
        {
            try
            {
                var result = await _pgdb.InsertOrUpdatePosEdcMachine(model);

                return StatusCode(200, new ApiStatusResponse
                {
                    Status = result.Status,
                    Code = result.Code,
                    Message = result.Message
                });
            }
            catch (Exception ex)
            {
                return StatusCode(200, new ApiStatusResponse
                {
                    Status = "false",
                    Code = "200",
                    Message = ex.Message.ToString()
                });
            }
        }

        [HttpGet("active-pos-edc-list")]
        public async Task<IActionResult> GetPosEdcList([FromQuery] int companyId, [FromQuery] int? providerAccountId)
        {
            try
            {



                if (companyId <= 0)
                {
                    return StatusCode(200, new ApiEdcStatusResponse
                    {
                        Status = "false",
                        Code = "200",
                        Message = "companyId is mandatory",
                        Data = null
                    });
                }

                var result = await _pgdb.GetPosEdcMachines(companyId, providerAccountId);
                return StatusCode(200, result);
            }
            catch (Exception Ex)
            {
                return StatusCode(200, new ApiEdcStatusResponse
                {
                    Status = "false",
                    Code = "200",
                    Message = Ex.Message.ToString(),
                    Data = null
                });
            }
        }



        [HttpGet("paylinktransactions")]
        public async Task<IActionResult> GetPaylinkTransactionsLogsAsync(string from_date, string to_date, string company_id)
        {
            try
            {
                DateTime fromdate = Convert.ToDateTime(from_date);
                DateTime todate = Convert.ToDateTime(to_date);



                // 🔹 Decide database provider
                if (_databaseProvider.Equals("postgres", StringComparison.OrdinalIgnoreCase))
                {
                    var result = await _pgdb.GetPaylinkPosEdcTransactioninfoAsync(fromdate, todate, company_id);

                    return StatusCode(200, new
                    {
                        code = "200",
                        status = "true",
                        message = "Successfully fetch transaction log Details",
                        data = result
                    });
                }
                else if (_databaseProvider.Equals("sql", StringComparison.OrdinalIgnoreCase))
                {
                    var result = await _pgdb.GetPaylinkPosEdcTransactioninfoAsync(fromdate, todate, company_id);

                    return StatusCode(200, new
                    {
                        code = "200",
                        status = "true",
                        message = "Successfully fetch transaction log Details",
                        data = result
                    });
                }
                else
                {
                    return StatusCode(200, new
                    {
                        code = 200,
                        status = "false",
                        message = "Unknown Error"
                    });
                }
            }
            catch (Exception ex)
            {
                // 🔹 Catch-all fallback
                return StatusCode(200, new
                {
                    code = 200,
                    status = "false",
                    message = ex.Message
                });
            }
        }

        [HttpGet("paylinktransactionsidjson")]
        public async Task<IActionResult> GetPaylinkTransactionsLogsIdJsonAsync(int transaction_log_id)
        {
            try
            {



                // 🔹 Decide database provider
                if (_databaseProvider.Equals("postgres", StringComparison.OrdinalIgnoreCase))
                {
                    var result = await _pgdb.GetPaylinkPosEdcTransactionJsonLoginfoAsync(transaction_log_id);

                    return StatusCode(200, new
                    {
                        code = "200",
                        status = "true",
                        message = "Successfully fetch transaction Josn log Details",
                        data = result
                    });
                }
                else if (_databaseProvider.Equals("sql", StringComparison.OrdinalIgnoreCase))
                {
                    var result = await _pgdb.GetPaylinkPosEdcTransactionJsonLoginfoAsync(transaction_log_id);

                    return StatusCode(200, new
                    {
                        code = "200",
                        status = "true",
                        message = "Successfully fetch transaction Josn log Details",
                        data = result
                    });
                }
                else
                {
                    return StatusCode(200, new
                    {
                        code = 200,
                        status = "false",
                        message = "Unknown Error"
                    });
                }
            }
            catch (Exception ex)
            {
                // 🔹 Catch-all fallback
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