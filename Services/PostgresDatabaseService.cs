using Dapper;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Npgsql;
using NpgsqlTypes;
using PaymentAPI.Models;
using System.Data;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace PaymentAPI.Services
{
    public class PostgresDatabaseService
    {
        private readonly string _connectionString;
        private readonly ILogger<PostgresDatabaseService> _logger;

        public PostgresDatabaseService(IConfiguration configuration, ILogger<PostgresDatabaseService> logger)
        {
            _connectionString = configuration.GetConnectionString("Postgres") ?? string.Empty;
            _logger = logger;
        }

        #region Company Master

        public async Task<OperationMasterCode> GenerateHospitalCodeAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                await using var conn = new NpgsqlConnection(_connectionString);
                await conn.OpenAsync(cancellationToken);

                using var cmd = new NpgsqlCommand("SELECT fn_generate_paylink_hospital_code() as hospital_cd;", conn);
                var result = await cmd.ExecuteScalarAsync(cancellationToken);

                return new OperationMasterCode
                {
                    code = "200",
                    status = "true",
                    message = "Hospital code generated successfully",
                    autocode = result?.ToString()
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "GenerateHospitalCodeAsync failed");
                return new OperationMasterCode
                {
                    code = "200",
                    status = "false",
                    message = $"Error: {ex.Message}",
                    autocode = ""
                };
            }
        }

        public async Task<List<CompanySettingsResponse>> GetAllPaylinkCompaniesAsync(CancellationToken cancellationToken = default)
        {
            var companies = new List<CompanySettingsResponse>();

            try
            {
                await using var conn = new NpgsqlConnection(_connectionString);
                await conn.OpenAsync(cancellationToken);

                using var cmd = new NpgsqlCommand("SELECT * FROM fn_get_paylink_companies();", conn);
                await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);

                while (await reader.ReadAsync(cancellationToken))
                {
                    companies.Add(new CompanySettingsResponse
                    {
                        CompanyId = reader.GetInt32(reader.GetOrdinal("paylink_companyid")),
                        CompanyCd = reader.GetString(reader.GetOrdinal("companycd")),
                        CompanyName = reader.GetString(reader.GetOrdinal("companyname")),
                        MobileNumber = reader.IsDBNull(reader.GetOrdinal("mobilenumber")) ? null : reader.GetString(reader.GetOrdinal("mobilenumber")),
                        address = reader.IsDBNull(reader.GetOrdinal("address1")) ? null : reader.GetString(reader.GetOrdinal("address1")),
                        faxNumber = reader.IsDBNull(reader.GetOrdinal("fax_number")) ? null : reader.GetString(reader.GetOrdinal("fax_number")),
                        website = reader.IsDBNull(reader.GetOrdinal("website_url")) ? null : reader.GetString(reader.GetOrdinal("website_url")),
                        CreateDt = reader.IsDBNull(reader.GetOrdinal("createdt")) ? (DateTime?)null : reader.GetDateTime(reader.GetOrdinal("createdt")),
                        CreateBy = reader.IsDBNull(reader.GetOrdinal("createby")) ? null : reader.GetString(reader.GetOrdinal("createby")),
                        ModifyDt = reader.IsDBNull(reader.GetOrdinal("modifydt")) ? (DateTime?)null : reader.GetDateTime(reader.GetOrdinal("modifydt")),
                        ModifyBy = reader.IsDBNull(reader.GetOrdinal("modifyby")) ? null : reader.GetString(reader.GetOrdinal("modifyby")),
                        recordStatus = reader.IsDBNull(reader.GetOrdinal("recordstatus")) ? null : reader.GetString(reader.GetOrdinal("recordstatus"))
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "GetAllPaylinkCompaniesAsync failed");
                throw;
            }

            return companies;
        }

        public async Task<OperationResult> InsertPaylinkCompanyAsync(CompanySettings company, CancellationToken cancellationToken = default)
        {
            try
            {
                await using var connection = new NpgsqlConnection(_connectionString);
                await connection.OpenAsync(cancellationToken);

                var param = new DynamicParameters();
                param.Add("p_company_id", company.CompanyId);
                param.Add("p_company_cd", company.CompanyCd);
                param.Add("p_company_name", company.CompanyName);
                param.Add("p_mobileno", company.MobileNumber);
                param.Add("p_address", company.address);
                param.Add("p_faxnumber", company.faxNumber);
                param.Add("p_website", company.website);
                param.Add("p_create_by", company.CreateBy);
                param.Add("p_record_status", company.recordStatus);

                param.Add("o_company_id", dbType: DbType.Int32, direction: ParameterDirection.Output);
                param.Add("o_status", dbType: DbType.String, size: 50, direction: ParameterDirection.Output);
                param.Add("o_message", dbType: DbType.String, size: 200, direction: ParameterDirection.Output);

                await connection.ExecuteAsync(
                    "pr_insert_paylink_company",
                    param,
                    commandType: CommandType.StoredProcedure);

                return new OperationResult
                {
                    code = "200",
                    status = param.Get<string>("o_status") == "SUCCESS" ? "true" : "false",
                    message = param.Get<string>("o_message")
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "InsertPaylinkCompanyAsync failed for {@Company}", company);
                return new OperationResult
                {
                    code = "200",
                    status = "false",
                    message = ex.Message
                };
            }
        }

        #endregion Company Master

        #region Location Master

        public async Task<OperationMasterCode> GenerateLocationCodeAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                await using var conn = new NpgsqlConnection(_connectionString);
                await conn.OpenAsync(cancellationToken);

                using var cmd = new NpgsqlCommand("SELECT fn_generate_paylink_location_code() as location_cd;", conn);
                var result = await cmd.ExecuteScalarAsync(cancellationToken);

                return new OperationMasterCode
                {
                    code = "200",
                    status = "true",
                    message = "Location code generated successfully",
                    autocode = result?.ToString()
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "GenerateLocationCodeAsync failed");
                return new OperationMasterCode
                {
                    code = "200",
                    status = "false",
                    message = $"Error: {ex.Message}",
                    autocode = ""
                };
            }
        }

        public async Task<List<CompanySettingsResponse>> GetAllActivePaylinkCompaniesAsync(CancellationToken cancellationToken = default)
        {
            var companies = new List<CompanySettingsResponse>();

            try
            {
                await using var conn = new NpgsqlConnection(_connectionString);
                await conn.OpenAsync(cancellationToken);

                using var cmd = new NpgsqlCommand("SELECT * FROM fn_get_active_paylink_companies();", conn);
                await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);

                while (await reader.ReadAsync(cancellationToken))
                {
                    companies.Add(new CompanySettingsResponse
                    {
                        CompanyId = reader.GetInt32(reader.GetOrdinal("paylink_companyid")),
                        CompanyCd = reader.GetString(reader.GetOrdinal("companycd")),
                        CompanyName = reader.GetString(reader.GetOrdinal("companyname")),
                        MobileNumber = reader.IsDBNull(reader.GetOrdinal("mobilenumber")) ? null : reader.GetString(reader.GetOrdinal("mobilenumber")),
                        address = reader.IsDBNull(reader.GetOrdinal("address1")) ? null : reader.GetString(reader.GetOrdinal("address1")),
                        faxNumber = reader.IsDBNull(reader.GetOrdinal("fax_number")) ? null : reader.GetString(reader.GetOrdinal("fax_number")),
                        website = reader.IsDBNull(reader.GetOrdinal("website_url")) ? null : reader.GetString(reader.GetOrdinal("website_url")),
                        CreateDt = reader.IsDBNull(reader.GetOrdinal("createdt")) ? (DateTime?)null : reader.GetDateTime(reader.GetOrdinal("createdt")),
                        CreateBy = reader.IsDBNull(reader.GetOrdinal("createby")) ? null : reader.GetString(reader.GetOrdinal("createby")),
                        ModifyDt = reader.IsDBNull(reader.GetOrdinal("modifydt")) ? (DateTime?)null : reader.GetDateTime(reader.GetOrdinal("modifydt")),
                        ModifyBy = reader.IsDBNull(reader.GetOrdinal("modifyby")) ? null : reader.GetString(reader.GetOrdinal("modifyby")),
                        recordStatus = reader.IsDBNull(reader.GetOrdinal("recordstatus")) ? null : reader.GetString(reader.GetOrdinal("recordstatus"))
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "GetAllActivePaylinkCompaniesAsync failed");
                throw;
            }

            return companies;
        }

        public async Task<List<LocationSettingsResponse>> GetAllPaylinkLocationsAsync(int companyId, CancellationToken cancellationToken = default)
        {
            var locations = new List<LocationSettingsResponse>();

            try
            {
                await using var conn = new NpgsqlConnection(_connectionString);
                await conn.OpenAsync(cancellationToken);

                using var cmd = new NpgsqlCommand("SELECT * FROM fn_get_paylink_locations(@p_company_id);", conn);
                cmd.Parameters.AddWithValue("p_company_id", companyId);

                await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
                while (await reader.ReadAsync(cancellationToken))
                {
                    locations.Add(new LocationSettingsResponse
                    {
                        LocationId = reader.GetInt32(reader.GetOrdinal("locationid")),
                        LocationCd = reader.GetString(reader.GetOrdinal("locationcd")),
                        LocationName = reader.GetString(reader.GetOrdinal("locationname")),
                        CompanyId = reader.GetInt32(reader.GetOrdinal("companyid")),
                        CompanyName = reader.GetString(reader.GetOrdinal("companyname")),
                        CreateDt = reader.IsDBNull(reader.GetOrdinal("createdt")) ? null : reader.GetDateTime(reader.GetOrdinal("createdt")),
                        CreateBy = reader.IsDBNull(reader.GetOrdinal("createby")) ? null : reader.GetString(reader.GetOrdinal("createby")),
                        ModifyDt = reader.IsDBNull(reader.GetOrdinal("modifydt")) ? null : reader.GetDateTime(reader.GetOrdinal("modifydt")),
                        ModifyBy = reader.IsDBNull(reader.GetOrdinal("modifyby")) ? null : reader.GetString(reader.GetOrdinal("modifyby")),
                        recordStatus = reader.IsDBNull(reader.GetOrdinal("recordstatus")) ? null : reader.GetString(reader.GetOrdinal("recordstatus"))
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "GetAllPaylinkLocationsAsync failed for companyId {CompanyId}", companyId);
                throw;
            }

            return locations;
        }

        public async Task<List<LocationSettingsResponse>> GetAllPaylinkCompanyAgainstLocationsAsync(string company_id, CancellationToken cancellationToken = default)
        {
            var companies = new List<LocationSettingsResponse>();
            if (!int.TryParse(company_id, out var CompanyId))
            {
                throw new ArgumentException("company_id must be an integer", nameof(company_id));
            }

            try
            {
                await using var conn = new NpgsqlConnection(_connectionString);
                await conn.OpenAsync(cancellationToken);

                using var cmd = new NpgsqlCommand("SELECT * FROM fn_get_paylink_company_locations(@p_company_id);", conn);
                cmd.Parameters.AddWithValue("p_company_id", CompanyId);

                await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
                while (await reader.ReadAsync(cancellationToken))
                {
                    companies.Add(new LocationSettingsResponse
                    {
                        LocationId = reader.GetInt32(reader.GetOrdinal("locationid")),
                        LocationCd = reader.GetString(reader.GetOrdinal("locationcd")),
                        LocationName = reader.GetString(reader.GetOrdinal("locationname")),
                        CompanyId = reader.GetInt32(reader.GetOrdinal("companyid")),
                        CompanyName = reader.GetString(reader.GetOrdinal("companyname")),
                        CreateDt = reader.IsDBNull(reader.GetOrdinal("createdt")) ? (DateTime?)null : reader.GetDateTime(reader.GetOrdinal("createdt")),
                        CreateBy = reader.IsDBNull(reader.GetOrdinal("createby")) ? null : reader.GetString(reader.GetOrdinal("createby")),
                        ModifyDt = reader.IsDBNull(reader.GetOrdinal("modifydt")) ? (DateTime?)null : reader.GetDateTime(reader.GetOrdinal("modifydt")),
                        ModifyBy = reader.IsDBNull(reader.GetOrdinal("modifyby")) ? null : reader.GetString(reader.GetOrdinal("modifyby")),
                        recordStatus = reader.IsDBNull(reader.GetOrdinal("recordstatus")) ? null : reader.GetString(reader.GetOrdinal("recordstatus"))
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "GetAllPaylinkCompanyAgainstLocationsAsync failed for company_id {CompanyId}", company_id);
                throw;
            }

            return companies;
        }

        public async Task<OperationResult> InsertPaylinklocationAsync(LocationSettings _loc, CancellationToken cancellationToken = default)
        {
            try
            {
                await using var connection = new NpgsqlConnection(_connectionString);
                await connection.OpenAsync(cancellationToken);

                var parameters = new DynamicParameters();
                parameters.Add("p_location_id", _loc.LocationId);
                parameters.Add("p_location_cd", _loc.LocationCd);
                parameters.Add("p_company_id", _loc.CompanyId);
                parameters.Add("p_company_name", _loc.CompanyName);
                parameters.Add("p_location_name", _loc.LocationName);
                parameters.Add("p_create_by", _loc.CreateBy);
                parameters.Add("p_record_status", _loc.recordStatus);

                parameters.Add("o_location_id", dbType: DbType.Int32, direction: ParameterDirection.Output);
                parameters.Add("o_status", dbType: DbType.String, size: 50, direction: ParameterDirection.Output);
                parameters.Add("o_message", dbType: DbType.String, size: 200, direction: ParameterDirection.Output);

                await connection.ExecuteAsync(
                    "pr_insert_paylink_location",
                    parameters,
                    commandType: CommandType.StoredProcedure);

                string dbStatus = parameters.Get<string>("o_status");
                string message = parameters.Get<string>("o_message");
                return new OperationResult
                {
                    code = "200",
                    status = dbStatus == "SUCCESS" ? "true" : "false",
                    message = message
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "InsertPaylinklocationAsync failed for {@Location}", _loc);
                return new OperationResult
                {
                    code = "500",
                    status = "false",
                    message = ex.Message
                };
            }
        }

        public async Task<OperationResult> UpdatePaylinkLocationAsync(LocationSettings _loc, CancellationToken cancellationToken = default)
        {
            try
            {
                await using var connection = new NpgsqlConnection(_connectionString);
                await connection.OpenAsync(cancellationToken);

                var parameters = new DynamicParameters();
                parameters.Add("p_paylink_location_id", _loc.LocationId);
                parameters.Add("p_company_id", _loc.CompanyId);
                parameters.Add("p_company_name", _loc.CompanyName);
                parameters.Add("p_location_cd", _loc.LocationCd);
                parameters.Add("p_location_name", _loc.LocationName);
                parameters.Add("p_modify_by", _loc.CreateBy);
                parameters.Add("p_record_status", _loc.recordStatus);
                parameters.Add("o_rows_updated", dbType: DbType.Int32, direction: ParameterDirection.Output);

                await connection.ExecuteAsync(
                    "pr_update_paylink_location",
                    parameters,
                    commandType: CommandType.StoredProcedure);

                int rowsAffected = parameters.Get<int>("o_rows_updated");
                return new OperationResult
                {
                    code = "200",
                    status = rowsAffected > 0 ? "true" : "false",
                    message = rowsAffected > 0 ? "Location Updated successfully." : "Location Updated failed."
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "UpdatePaylinkLocationAsync failed for {@Location}", _loc);
                return new OperationResult
                {
                    code = "500",
                    status = "false",
                    message = $"Unexpected error: {ex.Message}"
                };
            }
        }

        #endregion Location Master

        #region Provider Master

        public async Task<OperationMasterCode> GenerateProviderCodeAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                await using var conn = new NpgsqlConnection(_connectionString);
                await conn.OpenAsync(cancellationToken);

                using var cmd = new NpgsqlCommand("SELECT fn_generate_paylink_provider_code() as provider_cd;", conn);
                var result = await cmd.ExecuteScalarAsync(cancellationToken);

                return new OperationMasterCode
                {
                    code = "200",
                    status = "true",
                    message = "Provider code generated successfully",
                    autocode = result?.ToString()
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "GenerateProviderCodeAsync failed");
                return new OperationMasterCode
                {
                    code = "200",
                    status = "false",
                    message = $"Error: {ex.Message}",
                    autocode = ""
                };
            }
        }

        public async Task<List<ProviderSettingsResponse>> GetAllPaylinkProvidersAsync(CancellationToken cancellationToken = default)
        {
            var providers = new List<ProviderSettingsResponse>();

            try
            {
                await using var conn = new NpgsqlConnection(_connectionString);
                await conn.OpenAsync(cancellationToken);

                using var cmd = new NpgsqlCommand("SELECT * FROM fn_get_paylink_providers();", conn);
                await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);

                while (await reader.ReadAsync(cancellationToken))
                {
                    providers.Add(new ProviderSettingsResponse
                    {
                        ProviderId = reader.GetInt32(reader.GetOrdinal("providerid")),
                        ProviderCd = reader.GetString(reader.GetOrdinal("providercd")),
                        ProviderName = reader.GetString(reader.GetOrdinal("providername")),
                        Environment = reader.IsDBNull(reader.GetOrdinal("providerenvironment")) ? null : reader.GetString(reader.GetOrdinal("providerenvironment")),
                        PosInitiateUrl = reader.IsDBNull(reader.GetOrdinal("posinitiateurl")) ? null : reader.GetString(reader.GetOrdinal("posinitiateurl")),
                        PosStatusUrl = reader.IsDBNull(reader.GetOrdinal("posstatusurl")) ? null : reader.GetString(reader.GetOrdinal("posstatusurl")),
                        PosCancelUrl = reader.IsDBNull(reader.GetOrdinal("poscancelurl")) ? null : reader.GetString(reader.GetOrdinal("poscancelurl")),
                        CreateDt = reader.IsDBNull(reader.GetOrdinal("createdt")) ? (DateTime?)null : reader.GetDateTime(reader.GetOrdinal("createdt")),
                        CreateBy = reader.IsDBNull(reader.GetOrdinal("createby")) ? null : reader.GetString(reader.GetOrdinal("createby")),
                        ModifyDt = reader.IsDBNull(reader.GetOrdinal("modifydt")) ? (DateTime?)null : reader.GetDateTime(reader.GetOrdinal("modifydt")),
                        ModifyBy = reader.IsDBNull(reader.GetOrdinal("modifyby")) ? null : reader.GetString(reader.GetOrdinal("modifyby")),
                        recordStatus = reader.IsDBNull(reader.GetOrdinal("recordstatus")) ? null : reader.GetString(reader.GetOrdinal("recordstatus"))
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "GetAllPaylinkProvidersAsync failed");
                throw;
            }

            return providers;
        }

        public async Task<OperationResult> InsertPaylinkProviderAsync(ProviderSettings _pro, CancellationToken cancellationToken = default)
        {
            try
            {
                await using var connection = new NpgsqlConnection(_connectionString);
                await connection.OpenAsync(cancellationToken);

                var parameters = new DynamicParameters();
                parameters.Add("p_provider_id", _pro.ProviderId);
                parameters.Add("p_provider_cd", _pro.ProviderCd);
                parameters.Add("p_provider_name", _pro.ProviderName);
                parameters.Add("p_environment", _pro.Environment);
                parameters.Add("p_pos_initiate_url", _pro.PosInitiateUrl);
                parameters.Add("p_pos_status_url", _pro.PosStatusUrl);
                parameters.Add("p_pos_cancel_url", _pro.PosCancelUrl);
                parameters.Add("p_create_by", _pro.CreateBy);
                parameters.Add("p_record_status", _pro.recordStatus);

                parameters.Add("o_provider_id", dbType: DbType.Int32, direction: ParameterDirection.Output);
                parameters.Add("o_status", dbType: DbType.String, direction: ParameterDirection.Output);
                parameters.Add("o_message", dbType: DbType.String, direction: ParameterDirection.Output);

                await connection.ExecuteAsync(
                    "pr_insert_paylink_provider",
                    parameters,
                    commandType: CommandType.StoredProcedure);

                return new OperationResult
                {
                    code = parameters.Get<string>("o_status") == "SUCCESS" ? "200" : "400",
                    status = parameters.Get<string>("o_status") == "SUCCESS" ? "true" : "false",
                    message = parameters.Get<string>("o_message")
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "InsertPaylinkProviderAsync failed for {@Provider}", _pro);
                return new OperationResult
                {
                    code = "200",
                    status = "false",
                    message = ex.Message
                };
            }
        }

        public async Task<OperationResult> UpdatePaylinkProviderAsync(ProviderSettings _pro, CancellationToken cancellationToken = default)
        {
            try
            {
                await using var connection = new NpgsqlConnection(_connectionString);
                await connection.OpenAsync(cancellationToken);

                var parameters = new DynamicParameters();
                parameters.Add("p_paylink_provider_id", _pro.ProviderId);
                parameters.Add("p_provider_cd", _pro.ProviderCd);
                parameters.Add("p_provider_name", _pro.ProviderName);
                parameters.Add("p_pos_initiate_url", _pro.PosInitiateUrl);
                parameters.Add("p_pos_status_url", _pro.PosStatusUrl);
                parameters.Add("p_pos_cancel_url", _pro.PosCancelUrl);
                parameters.Add("p_create_by", _pro.CreateBy);
                parameters.Add("p_record_status", _pro.recordStatus);
                parameters.Add("o_rows_updated", dbType: DbType.Int32, direction: ParameterDirection.Output);

                await connection.ExecuteAsync(
                    "pr_update_paylink_provider",
                    parameters,
                    commandType: CommandType.StoredProcedure);

                int rowsAffected = parameters.Get<int>("o_rows_updated");
                return new OperationResult
                {
                    code = "200",
                    status = rowsAffected > 0 ? "true" : "false",
                    message = rowsAffected > 0 ? "Provider Updated successfully." : "Provider Updated failed."
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "UpdatePaylinkProviderAsync failed for {@Provider}", _pro);
                return new OperationResult
                {
                    code = "500",
                    status = "false",
                    message = $"Unexpected error: {ex.Message}"
                };
            }
        }

        #endregion Provider Master

        #region Provider Accounts

        public async Task<OperationMasterCode> GenerateProviderAccountsCodeAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                await using var conn = new NpgsqlConnection(_connectionString);
                await conn.OpenAsync(cancellationToken);

                using var cmd = new NpgsqlCommand("SELECT fn_generate_paylink_provider_account_code() as provider_Acc_cd;", conn);
                var result = await cmd.ExecuteScalarAsync(cancellationToken);

                return new OperationMasterCode
                {
                    code = "200",
                    status = "true",
                    message = "Provider Account code generated successfully",
                    autocode = result?.ToString()
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "GenerateProviderAccountsCodeAsync failed");
                return new OperationMasterCode
                {
                    code = "200",
                    status = "false",
                    message = $"Error: {ex.Message}",
                    autocode = ""
                };
            }
        }

        public async Task<List<ProviderSettingsResponse>> GetActivePaylinkProvidersAsync(CancellationToken cancellationToken = default)
        {
            var providers = new List<ProviderSettingsResponse>();

            try
            {
                await using var conn = new NpgsqlConnection(_connectionString);
                await conn.OpenAsync(cancellationToken);

                using var cmd = new NpgsqlCommand("SELECT * FROM fn_get_active_paylink_providers();", conn);
                await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);

                while (await reader.ReadAsync(cancellationToken))
                {
                    providers.Add(new ProviderSettingsResponse
                    {
                        ProviderId = reader.GetInt32(reader.GetOrdinal("providerid")),
                        ProviderCd = reader.GetString(reader.GetOrdinal("providercd")),
                        ProviderName = reader.GetString(reader.GetOrdinal("providername")),
                        Environment = reader.IsDBNull(reader.GetOrdinal("providerenvironment")) ? null : reader.GetString(reader.GetOrdinal("providerenvironment")),
                        PosInitiateUrl = reader.IsDBNull(reader.GetOrdinal("posinitiateurl")) ? null : reader.GetString(reader.GetOrdinal("posinitiateurl")),
                        PosStatusUrl = reader.IsDBNull(reader.GetOrdinal("posstatusurl")) ? null : reader.GetString(reader.GetOrdinal("posstatusurl")),
                        PosCancelUrl = reader.IsDBNull(reader.GetOrdinal("poscancelurl")) ? null : reader.GetString(reader.GetOrdinal("poscancelurl")),
                        CreateDt = reader.IsDBNull(reader.GetOrdinal("createdt")) ? (DateTime?)null : reader.GetDateTime(reader.GetOrdinal("createdt")),
                        CreateBy = reader.IsDBNull(reader.GetOrdinal("createby")) ? null : reader.GetString(reader.GetOrdinal("createby")),
                        ModifyDt = reader.IsDBNull(reader.GetOrdinal("modifydt")) ? (DateTime?)null : reader.GetDateTime(reader.GetOrdinal("modifydt")),
                        ModifyBy = reader.IsDBNull(reader.GetOrdinal("modifyby")) ? null : reader.GetString(reader.GetOrdinal("modifyby")),
                        recordStatus = reader.IsDBNull(reader.GetOrdinal("recordstatus")) ? null : reader.GetString(reader.GetOrdinal("recordstatus"))
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "GetActivePaylinkProvidersAsync failed");
                throw;
            }

            return providers;
        }

        public async Task<List<PaylinkProviderAccountResponse>> GetAllPaylinkProviderAccountsAsync(
            int companyId,
            int? locationId,
            int? providerId,
            CancellationToken cancellationToken = default)
        {
            var accounts = new List<PaylinkProviderAccountResponse>();
            await using var conn = new NpgsqlConnection(_connectionString);

            try
            {
                await conn.OpenAsync(cancellationToken);

                using var cmd = new NpgsqlCommand(
                    "SELECT * FROM fn_get_paylink_provider_accounts(@p_company_id, @p_location_id, @p_provider_id);", conn);

                cmd.Parameters.AddWithValue("p_company_id", companyId);
                cmd.Parameters.AddWithValue("p_location_id", (object?)locationId ?? DBNull.Value);
                cmd.Parameters.AddWithValue("p_provider_id", (object?)providerId ?? DBNull.Value);

                await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);

                while (await reader.ReadAsync(cancellationToken))
                {
                    accounts.Add(new PaylinkProviderAccountResponse
                    {
                        PrvAcId = reader.GetInt32(reader.GetOrdinal("prvacid")),
                        PrvAccd = reader.GetString(reader.GetOrdinal("prvaccd")),
                        ProviderId = reader.GetInt32(reader.GetOrdinal("providerid")),
                        ProviderName = reader.GetString(reader.GetOrdinal("providername")),
                        LocationId = reader.GetInt32(reader.GetOrdinal("locationid")),
                        LocationName = reader.GetString(reader.GetOrdinal("locationname")),
                        CompanyId = reader.GetInt32(reader.GetOrdinal("companyid")),
                        CompanyName = reader.GetString(reader.GetOrdinal("companyname")),
                        AccountName = reader.GetString(reader.GetOrdinal("accountname")),

                        WorldlineEncKey = reader.IsDBNull(reader.GetOrdinal("worldlineenckey")) ? null : reader.GetString(reader.GetOrdinal("worldlineenckey")),
                        WorldlineEncIv = reader.IsDBNull(reader.GetOrdinal("worldlineenciv")) ? null : reader.GetString(reader.GetOrdinal("worldlineenciv")),
                        RazorpayUsername = reader.IsDBNull(reader.GetOrdinal("razorpayusername")) ? null : reader.GetString(reader.GetOrdinal("razorpayusername")),
                        RazorpayAppKey = reader.IsDBNull(reader.GetOrdinal("razorpayappkey")) ? null : reader.GetString(reader.GetOrdinal("razorpayappkey")),

                        PinelabUserId = reader.IsDBNull(reader.GetOrdinal("pinelabuserid")) ? null : reader.GetString(reader.GetOrdinal("pinelabuserid")),
                        PinelabMerchantId = reader.IsDBNull(reader.GetOrdinal("pinelabmerchantid")) ? null : reader.GetString(reader.GetOrdinal("pinelabmerchantid")),
                        PinelabSecurityToken = reader.IsDBNull(reader.GetOrdinal("pinelabsecuritytoken")) ? null : reader.GetString(reader.GetOrdinal("pinelabsecuritytoken")),
                        PinelabMerchantStorePosCode = reader.IsDBNull(reader.GetOrdinal("pinelabmerchantstoreposcode")) ? null : reader.GetString(reader.GetOrdinal("pinelabmerchantstoreposcode")),

                        IciciMid = reader.IsDBNull(reader.GetOrdinal("icicimid")) ? null : reader.GetString(reader.GetOrdinal("icicimid")),
                        IciciErpTranId = reader.IsDBNull(reader.GetOrdinal("icicierptranid")) ? null : reader.GetString(reader.GetOrdinal("icicierptranid")),
                        IciciErpClientId = reader.IsDBNull(reader.GetOrdinal("icicierpclientid")) ? null : reader.GetString(reader.GetOrdinal("icicierpclientid")),
                        IciciSourceId = reader.IsDBNull(reader.GetOrdinal("icicisourceid")) ? null : reader.GetString(reader.GetOrdinal("icicisourceid")),

                        icicipaylinkmid = reader.IsDBNull(reader.GetOrdinal("icicipaylinkmid")) ? null : reader.GetString(reader.GetOrdinal("icicipaylinkmid")),
                        icicipaylinksecretkey = reader.IsDBNull(reader.GetOrdinal("icicipaylinksecretkey")) ? null : reader.GetString(reader.GetOrdinal("icicipaylinksecretkey")),

                        HdfcMid = reader.IsDBNull(reader.GetOrdinal("hdfcmid")) ? null : reader.GetString(reader.GetOrdinal("hdfcmid")),
                        HdfcErpTranId = reader.IsDBNull(reader.GetOrdinal("hdfcerptranid")) ? null : reader.GetString(reader.GetOrdinal("hdfcerptranid")),
                        HdfcErpClientId = reader.IsDBNull(reader.GetOrdinal("hdfcerpclientid")) ? null : reader.GetString(reader.GetOrdinal("hdfcerpclientid")),
                        HdfcSourceId = reader.IsDBNull(reader.GetOrdinal("hdfcsourceid")) ? null : reader.GetString(reader.GetOrdinal("hdfcsourceid")),

                        MswipeUsername = reader.IsDBNull(reader.GetOrdinal("mswipeusername")) ? null : reader.GetString(reader.GetOrdinal("mswipeusername")),
                        MswipePassword = reader.IsDBNull(reader.GetOrdinal("mswipepassword")) ? null : reader.GetString(reader.GetOrdinal("mswipepassword")),
                        MswipeSaltKey = reader.IsDBNull(reader.GetOrdinal("mswipesaltkey")) ? null : reader.GetString(reader.GetOrdinal("mswipesaltkey")),
                        MswipeClientKey = reader.IsDBNull(reader.GetOrdinal("mswipeclientkey")) ? null : reader.GetString(reader.GetOrdinal("mswipeclientkey")),
                        MswipeClientSecret = reader.IsDBNull(reader.GetOrdinal("mswipeclientsecret")) ? null : reader.GetString(reader.GetOrdinal("mswipeclientsecret")),
                        MswipeClientCode = reader.IsDBNull(reader.GetOrdinal("mswipeclientcode")) ? null : reader.GetString(reader.GetOrdinal("mswipeclientcode")),

                        PaytmMid = reader.IsDBNull(reader.GetOrdinal("paytmmid")) ? null : reader.GetString(reader.GetOrdinal("paytmmid")),
                        PaytmMerchantKey = reader.IsDBNull(reader.GetOrdinal("paytmmerchantkey")) ? null : reader.GetString(reader.GetOrdinal("paytmmerchantkey")),

                        PhonePeproviderId = reader.IsDBNull(reader.GetOrdinal("phonepeproviderid")) ? null : reader.GetString(reader.GetOrdinal("phonepeproviderid")),
                        PhonePeSaltKey = reader.IsDBNull(reader.GetOrdinal("phonepesaltkey")) ? null : reader.GetString(reader.GetOrdinal("phonepesaltkey")),
                        PhonePeSaltIndex = reader.IsDBNull(reader.GetOrdinal("phonepesaltindex")) ? null : reader.GetString(reader.GetOrdinal("phonepesaltindex")),
                        PhonePeMerchantId = reader.IsDBNull(reader.GetOrdinal("phonepemerchantid")) ? null : reader.GetString(reader.GetOrdinal("phonepemerchantid")),

                        CreateBy = reader.IsDBNull(reader.GetOrdinal("createby")) ? null : reader.GetString(reader.GetOrdinal("createby")),
                        CreateDt = reader.IsDBNull(reader.GetOrdinal("createdt")) ? null : reader.GetDateTime(reader.GetOrdinal("createdt")),
                        ModifyBy = reader.IsDBNull(reader.GetOrdinal("modifyby")) ? null : reader.GetString(reader.GetOrdinal("modifyby")),
                        ModifyDt = reader.IsDBNull(reader.GetOrdinal("modifydt")) ? null : reader.GetDateTime(reader.GetOrdinal("modifydt")),

                        RecordStatus = reader.IsDBNull(reader.GetOrdinal("recordstatus")) ? '\0' : reader.GetString(reader.GetOrdinal("recordstatus"))[0]
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "GetAllPaylinkProviderAccountsAsync failed");
                throw;
            }

            return accounts;
        }

        public async Task<OperationResult> InsertPaylinkProviderAccountAsync(PaylinkProviderAccount acc, CancellationToken cancellationToken = default)
        {
            try
            {
                await using var connection = new NpgsqlConnection(_connectionString);
                await connection.OpenAsync(cancellationToken);

                var parameters = new DynamicParameters();

                parameters.Add("p_provider_account_cd", acc.ProviderAccountCd);
                parameters.Add("p_provider_id", acc.ProviderId);
                parameters.Add("p_provider_name", acc.ProviderName);
                parameters.Add("p_location_id", acc.LocationId);
                parameters.Add("p_location_name", acc.LocationName);
                parameters.Add("p_company_id", acc.CompanyId);
                parameters.Add("p_company_name", acc.CompanyName);
                parameters.Add("p_account_name", acc.AccountName);

                parameters.Add("p_worldline_enc_key", acc.WorldlineEncKey);
                parameters.Add("p_worldline_enc_iv", acc.WorldlineEncIv);

                parameters.Add("p_razorpay_username", acc.RazorpayUsername);
                parameters.Add("p_razorpay_appkey", acc.RazorpayAppKey);

                parameters.Add("p_pinelab_userid", acc.PinelabUserId);
                parameters.Add("p_pinelab_merchantid", acc.PinelabMerchantId);
                parameters.Add("p_pinelab_securitytoken", acc.PinelabSecurityToken);
                parameters.Add("p_pinelab_merchantstoreposcode", acc.PinelabMerchantStorePosCode);

                parameters.Add("p_icici_mid", acc.IciciMid);
                parameters.Add("p_icici_erp_tran_id", acc.IciciErpTranId);
                parameters.Add("p_icici_erp_client_id", acc.IciciErpClientId);
                parameters.Add("p_icici_source_id", acc.IciciSourceId);

                // 👇 ADD HERE
                parameters.Add("p_icici_paylink_mid", acc.IciciPaylinkMid);
                parameters.Add("p_icici_paylink_secret_key", acc.IciciPaylinkSecretKey);

                parameters.Add("p_hdfc_mid", acc.HdfcMid);
                parameters.Add("p_hdfc_erp_tran_id", acc.HdfcErpTranId);
                parameters.Add("p_hdfc_erp_client_id", acc.HdfcErpClientId);
                parameters.Add("p_hdfc_source_id", acc.HdfcSourceId);

                parameters.Add("p_mswipe_username", acc.MswipeUsername);
                parameters.Add("p_mswipe_password", acc.MswipePassword);
                parameters.Add("p_mswipe_salt_key", acc.MswipeSaltKey);
                parameters.Add("p_mswipe_clientkey", acc.MswipeClientKey);
                parameters.Add("p_mswipe_clientsecret", acc.MswipeClientSecret);
                parameters.Add("p_mswipe_clientcode", acc.MswipeClientCode);

                parameters.Add("p_paytm_mid", acc.PaytmMid);
                parameters.Add("p_paytm_merchantkey", acc.PaytmMerchantKey);

                parameters.Add("p_phonepe_providerid", acc.PhonePeproviderId);
                parameters.Add("p_phonepe_saltkey", acc.PhonePeSaltKey);
                parameters.Add("p_phonepe_saltindex", acc.PhonePeSaltIndex);
                parameters.Add("p_phonepe_merchantid", acc.PhonePeMerchantId);

                parameters.Add("p_create_by", acc.CreateBy);
                parameters.Add("p_record_status", acc.RecordStatus);

                parameters.Add("o_rows_affected", dbType: DbType.Int32, direction: ParameterDirection.Output);

                await connection.ExecuteAsync(
                    "pr_insert_paylink_provider_account",
                    parameters,
                    commandType: CommandType.StoredProcedure);

                int rowsAffected = parameters.Get<int>("o_rows_affected");

                return new OperationResult
                {
                    code = "200",
                    status = rowsAffected > 0 ? "true" : "false",
                    message = rowsAffected > 0 ? "Provider account inserted successfully." : "Insert failed."
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "InsertPaylinkProviderAccountAsync failed for {@Account}", acc);
                return new OperationResult
                {
                    code = "200",
                    status = "false",
                    message = $"Unexpected error: {ex.Message}"
                };
            }
        }

        public async Task<OperationResult> UpdatePaylinkProviderAccountAsync(PaylinkProviderAccount acc, CancellationToken cancellationToken = default)
        {
            try
            {
                await using var connection = new NpgsqlConnection(_connectionString);
                await connection.OpenAsync(cancellationToken);

                var parameters = new DynamicParameters();

                parameters.Add("p_provider_account_id", acc.ProviderAccountId);
                parameters.Add("p_provider_account_cd", acc.ProviderAccountCd);
                parameters.Add("p_provider_id", acc.ProviderId);
                parameters.Add("p_provider_name", acc.ProviderName);
                parameters.Add("p_location_id", acc.LocationId);
                parameters.Add("p_location_name", acc.LocationName);
                parameters.Add("p_company_id", acc.CompanyId);
                parameters.Add("p_company_name", acc.CompanyName);
                parameters.Add("p_account_name", acc.AccountName);

                parameters.Add("p_worldline_enc_key", acc.WorldlineEncKey);
                parameters.Add("p_worldline_enc_iv", acc.WorldlineEncIv);

                parameters.Add("p_razorpay_username", acc.RazorpayUsername);
                parameters.Add("p_razorpay_appkey", acc.RazorpayAppKey);

                parameters.Add("p_pinelab_userid", acc.PinelabUserId);
                parameters.Add("p_pinelab_merchantid", acc.PinelabMerchantId);
                parameters.Add("p_pinelab_securitytoken", acc.PinelabSecurityToken);
                parameters.Add("p_pinelab_merchantstoreposcode", acc.PinelabMerchantStorePosCode);

                parameters.Add("p_icici_mid", acc.IciciMid);
                parameters.Add("p_icici_erp_tran_id", acc.IciciErpTranId);
                parameters.Add("p_icici_erp_client_id", acc.IciciErpClientId);
                parameters.Add("p_icici_source_id", acc.IciciSourceId);

                // 👇 ADD HERE
                parameters.Add("p_icici_paylink_mid", acc.IciciPaylinkMid);
                parameters.Add("p_icici_paylink_secret_key", acc.IciciPaylinkSecretKey);

                parameters.Add("p_hdfc_mid", acc.HdfcMid);
                parameters.Add("p_hdfc_erp_tran_id", acc.HdfcErpTranId);
                parameters.Add("p_hdfc_erp_client_id", acc.HdfcErpClientId);
                parameters.Add("p_hdfc_source_id", acc.HdfcSourceId);

                parameters.Add("p_mswipe_username", acc.MswipeUsername);
                parameters.Add("p_mswipe_password", acc.MswipePassword);
                parameters.Add("p_mswipe_salt_key", acc.MswipeSaltKey);
                parameters.Add("p_mswipe_clientkey", acc.MswipeClientKey);
                parameters.Add("p_mswipe_clientsecret", acc.MswipeClientSecret);
                parameters.Add("p_mswipe_clientcode", acc.MswipeClientCode);

                parameters.Add("p_paytm_mid", acc.PaytmMid);
                parameters.Add("p_paytm_merchantkey", acc.PaytmMerchantKey);

                parameters.Add("p_phonepe_providerid", acc.PhonePeproviderId);
                parameters.Add("p_phonepe_saltkey", acc.PhonePeSaltKey);
                parameters.Add("p_phonepe_saltindex", acc.PhonePeSaltIndex);
                parameters.Add("p_phonepe_merchantid", acc.PhonePeMerchantId);

                parameters.Add("p_create_by", acc.CreateBy);
                parameters.Add("p_record_status", acc.RecordStatus);

                parameters.Add("o_rows_affected", dbType: DbType.Int32, direction: ParameterDirection.Output);

                await connection.ExecuteAsync(
                    "pr_update_paylink_provider_account",
                    parameters,
                    commandType: CommandType.StoredProcedure);

                int rowsAffected = parameters.Get<int>("o_rows_affected");

                return new OperationResult
                {
                    code = "200",
                    status = rowsAffected > 0 ? "true" : "false",
                    message = rowsAffected > 0 ? "Provider account updated successfully." : "Update failed."
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "UpdatePaylinkProviderAccountAsync failed for {@Account}", acc);
                return new OperationResult
                {
                    code = "200",
                    status = "false",
                    message = $"Unexpected error: {ex.Message}"
                };
            }
        }

        public async Task<ApiStatusResponse> ActiveInactiveProviderAccountAsync(
            int providerAccountId, string recordStatus, int modifyBy, CancellationToken cancellationToken = default)
        {
            var response = new ApiStatusResponse();

            try
            {
                await using var conn = new NpgsqlConnection(_connectionString);
                await conn.OpenAsync(cancellationToken);

                using var cmd = new NpgsqlCommand(
                    "CALL pr_active_inactive_provider_account(@p_provider_account_id, @p_record_status, @p_modify_by, @o_code, @o_status, @o_message);",
                    conn);

                cmd.Parameters.AddWithValue("p_provider_account_id", providerAccountId);
                cmd.Parameters.AddWithValue("p_record_status", recordStatus);
                cmd.Parameters.AddWithValue("p_modify_by", modifyBy);

                var o_code = new NpgsqlParameter("o_code", NpgsqlDbType.Varchar) { Direction = ParameterDirection.InputOutput, Value = DBNull.Value };
                var o_status = new NpgsqlParameter("o_status", NpgsqlDbType.Varchar) { Direction = ParameterDirection.InputOutput, Value = DBNull.Value };
                var o_message = new NpgsqlParameter("o_message", NpgsqlDbType.Varchar) { Direction = ParameterDirection.InputOutput, Value = DBNull.Value };

                cmd.Parameters.Add(o_code);
                cmd.Parameters.Add(o_status);
                cmd.Parameters.Add(o_message);

                await cmd.ExecuteNonQueryAsync(cancellationToken);

                response.Code = o_code.Value?.ToString() ?? "200";
                response.Status = o_status.Value?.ToString() ?? "false";
                response.Message = o_message.Value?.ToString() ?? "Unknown error";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ActiveInactiveProviderAccountAsync failed for ProviderAccountId {ProviderAccountId}", providerAccountId);
                response.Code = "200";
                response.Status = "false";
                response.Message = ex.Message;
            }

            return response;
        }

        #endregion Provider Acoounts

        #region Pos Machine  Master

        public async Task<OperationMasterCode> GeneratePosMachineCodeAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                await using var conn = new NpgsqlConnection(_connectionString);
                await conn.OpenAsync(cancellationToken);

                using var cmd = new NpgsqlCommand("SELECT fn_generate_paylink_pos_edc_machine_code() as pos_cd;", conn);
                var result = await cmd.ExecuteScalarAsync(cancellationToken);

                return new OperationMasterCode
                {
                    code = "200",
                    status = "true",
                    message = "Pos Machine code generated successfully",
                    autocode = result?.ToString()
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "GeneratePosMachineCodeAsync failed");
                return new OperationMasterCode
                {
                    code = "200",
                    status = "false",
                    message = $"Error: {ex.Message}",
                    autocode = ""
                };
            }
        }

        public async Task<ApiListResponse<ProviderAccountListResponse>> GetProviderAccountsByCompanyAsync(int companyId, CancellationToken cancellationToken = default)
        {
            var response = new ApiListResponse<ProviderAccountListResponse>();
            var accounts = new List<ProviderAccountListResponse>();

            try
            {
                await using var conn = new NpgsqlConnection(_connectionString);
                await conn.OpenAsync(cancellationToken);

                using var cmd = new NpgsqlCommand(
                    "SELECT * FROM fn_get_provider_accounts_by_company(@p_company_id);", conn);

                cmd.Parameters.AddWithValue("p_company_id", companyId);

                await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);

                while (await reader.ReadAsync(cancellationToken))
                {
                    accounts.Add(new ProviderAccountListResponse
                    {
                        ProviderWithAccount = reader.GetString(reader.GetOrdinal("provider_with_account")),
                        ProviderAccountId = reader.GetInt32(reader.GetOrdinal("provider_account_id")),
                        CompanyId = reader.GetInt32(reader.GetOrdinal("paylink_company_id")),
                        CompanyName = reader.GetString(reader.GetOrdinal("company_name")),
                        LocationId = reader.GetInt32(reader.GetOrdinal("paylink_location_id")),
                        LocationName = reader.GetString(reader.GetOrdinal("location_name")),
                        ProviderId = reader.GetInt32(reader.GetOrdinal("paylink_provider_id")),
                        ProviderName = reader.GetString(reader.GetOrdinal("provider_name"))
                    });
                }

                response.Code = "200";
                response.Status = "true";
                response.Message = "Provider account list fetched successfully";
                response.Data = accounts;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "GetProviderAccountsByCompanyAsync failed for CompanyId {CompanyId}", companyId);
                response.Code = "200";
                response.Status = "false";
                response.Message = ex.Message;
                response.Data = null;
            }

            return response;
        }

        public async Task<OperationResult> InsertPaylinkPosEdcMachineAsync(PosEdcMachineSettings _posedc, CancellationToken cancellationToken = default)
        {
            try
            {
                await using var connection = new NpgsqlConnection(_connectionString);
                await connection.OpenAsync(cancellationToken);

                var parameters = new DynamicParameters();
                parameters.Add("p_pos_edc_machine_cd", _posedc.posEdcMachineCd);
                parameters.Add("p_company_id", _posedc.CompanyId);
                parameters.Add("p_company_name", _posedc.CompanyName);
                parameters.Add("p_location_id", _posedc.LocationId);
                parameters.Add("p_location_name", _posedc.LocationName);
                parameters.Add("p_provider_id", _posedc.ProviderId);
                parameters.Add("p_provider_name", _posedc.ProviderName);
                parameters.Add("p_provider_account_id", _posedc.ProviderAccountId);
                parameters.Add("p_provider_account_name", _posedc.ProviderAccountName);
                parameters.Add("p_pos_edc_machine_name", _posedc.posEdcMachineName);
                parameters.Add("p_pos_edc_sno", _posedc.PosEdcSno);
                parameters.Add("p_pos_edc_terminal_id", _posedc.PosEdcTerminalId);
                parameters.Add("p_create_by", _posedc.CreateBy);
                parameters.Add("p_record_status", _posedc.recordStatus);
                parameters.Add("o_rows_inserted", dbType: DbType.Int32, direction: ParameterDirection.Output);

                await connection.ExecuteAsync(
                    "pr_insert_paylink_pos_edc_machine",
                    parameters,
                    commandType: CommandType.StoredProcedure);

                int rowsAffected = parameters.Get<int>("o_rows_inserted");

                return new OperationResult
                {
                    code = "200",
                    status = rowsAffected > 0 ? "true" : "false",
                    message = rowsAffected > 0 ? "Pos Edc Machine Created successfully." : "Pos Edc Machine Created failed."
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "InsertPaylinkPosEdcMachineAsync failed for {@Machine}", _posedc);
                return new OperationResult
                {
                    code = "200",
                    status = "false",
                    message = $"Unexpected error: {ex.Message}"
                };
            }
        }

        public async Task<OperationResult> UpdatePaylinkPosEdcMachineAsync(PosEdcMachineSettings _posedc, CancellationToken cancellationToken = default)
        {
            try
            {
                await using var connection = new NpgsqlConnection(_connectionString);
                await connection.OpenAsync(cancellationToken);

                var parameters = new DynamicParameters();
                parameters.Add("p_pos_edc_machine_id", _posedc.posedcMachineId);
                parameters.Add("p_pos_edc_machine_cd", _posedc.posEdcMachineCd);
                parameters.Add("p_company_id", _posedc.CompanyId);
                parameters.Add("p_company_name", _posedc.CompanyName);
                parameters.Add("p_location_id", _posedc.LocationId);
                parameters.Add("p_location_name", _posedc.LocationName);
                parameters.Add("p_provider_id", _posedc.ProviderId);
                parameters.Add("p_provider_name", _posedc.ProviderName);
                parameters.Add("p_provider_account_id", _posedc.ProviderAccountId);
                parameters.Add("p_provider_account_name", _posedc.ProviderAccountName);
                parameters.Add("p_pos_edc_machine_name", _posedc.posEdcMachineName);
                parameters.Add("p_pos_edc_sno", _posedc.PosEdcSno);
                parameters.Add("p_pos_edc_terminal_id", _posedc.PosEdcTerminalId);
                parameters.Add("p_create_by", _posedc.CreateBy);
                parameters.Add("p_record_status", _posedc.recordStatus);
                parameters.Add("o_rows_updated", dbType: DbType.Int32, direction: ParameterDirection.Output);

                await connection.ExecuteAsync(
                   "pr_update_paylink_pos_edc_machine",
                   parameters,
                   commandType: CommandType.StoredProcedure);

                int rowsAffected = parameters.Get<int>("o_rows_updated");

                return new OperationResult
                {
                    code = "200",
                    status = rowsAffected > 0 ? "true" : "false",
                    message = rowsAffected > 0 ? "Pos EDC Machine Updated successfully." : "Pos EDC Machine Updated failed."
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "UpdatePaylinkPosEdcMachineAsync failed for {@Machine}", _posedc);
                return new OperationResult
                {
                    code = "200",
                    status = "false",
                    message = $"Unexpected error: {ex.Message}"
                };
            }
        }

        public async Task<ApiStatusResponse> InsertOrUpdatePosEdcMachine(EdcMachineRequest req, CancellationToken cancellationToken = default)
        {
            var response = new ApiStatusResponse();

            await using var conn = new NpgsqlConnection(_connectionString);
            await using var cmd = new NpgsqlCommand("pr_insupd_paylink_pos_edc_machine", conn)
            {
                CommandType = CommandType.StoredProcedure
            };

            try
            {
                cmd.Parameters.AddWithValue("p_pos_edc_machine_id", req.PosEdcMachineId);
                cmd.Parameters.AddWithValue("p_pos_edc_machine_cd", req.PosEdcMachineCd);
                cmd.Parameters.AddWithValue("p_company_id", req.CompanyId);
                cmd.Parameters.AddWithValue("p_company_name", req.CompanyName);
                cmd.Parameters.AddWithValue("p_provider_account_id", req.ProviderAccountId);
                cmd.Parameters.AddWithValue("p_pos_edc_sno", req.PosEdcSno);
                cmd.Parameters.AddWithValue("p_pos_edc_terminal_id", req.PosEdcTerminalId);
                cmd.Parameters.AddWithValue("p_storeid", req.StoreId);
                cmd.Parameters.AddWithValue("p_clientid", req.ClientId);
                cmd.Parameters.AddWithValue("p_merchantposcode", req.MerchantPosCode);
                cmd.Parameters.AddWithValue("p_paytmmid", req.PaytmMID);
                cmd.Parameters.AddWithValue("p_paytmmerchantkey", req.PaytmMerchantKey);
                cmd.Parameters.AddWithValue("p_create_by", req.CreateBy);
                cmd.Parameters.AddWithValue("p_record_status", req.RecordStatus);

                cmd.Parameters.Add("o_rows_inserted", NpgsqlDbType.Integer).Direction = ParameterDirection.Output;
                cmd.Parameters.Add("o_status", NpgsqlDbType.Varchar).Direction = ParameterDirection.Output;
                cmd.Parameters.Add("o_message", NpgsqlDbType.Varchar).Direction = ParameterDirection.Output;

                await conn.OpenAsync(cancellationToken);
                await cmd.ExecuteNonQueryAsync(cancellationToken);

                response.Status = cmd.Parameters["o_status"].Value?.ToString() ?? "false";
                response.Code = response.Status == "true" ? "200" : "400";
                response.Message = cmd.Parameters["o_message"].Value?.ToString() ?? "No message returned";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "InsertOrUpdatePosEdcMachine failed for {@Request}", req);
                response.Status = "false";
                response.Code = "200";
                response.Message = ex.Message;
            }

            return response;
        }

        public async Task<ApiEdcStatusResponse> GetPosEdcMachines(int companyId, int? providerAccountId, CancellationToken cancellationToken = default)
        {
            var response = new ApiEdcStatusResponse();

            try
            {
                await using var conn = new NpgsqlConnection(_connectionString);
                await conn.OpenAsync(cancellationToken);

                using var cmd = new NpgsqlCommand("SELECT * FROM fn_get_pos_edc_machines(@p_company_id, @p_provider_account_id)", conn);
                cmd.Parameters.AddWithValue("p_company_id", companyId);
                cmd.Parameters.AddWithValue("p_provider_account_id", (object?)providerAccountId ?? DBNull.Value);

                var list = new List<Dictionary<string, object>>();
                await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
                while (await reader.ReadAsync(cancellationToken))
                {
                    var row = new Dictionary<string, object>();
                    for (int i = 0; i < reader.FieldCount; i++)
                        row.Add(reader.GetName(i), reader.IsDBNull(i) ? null : reader.GetValue(i));
                    list.Add(row);
                }

                if (list.Count == 0)
                {
                    response.Status = "false";
                    response.Code = "200";
                    response.Message = "No data found";
                    response.Data = null;
                }
                else
                {
                    response.Status = "true";
                    response.Code = "200";
                    response.Message = "Data fetched successfully";
                    response.Data = list;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "GetPosEdcMachines failed for CompanyId {CompanyId}", companyId);
                response.Status = "false";
                response.Code = "200";
                response.Message = ex.Message;
                response.Data = null;
            }

            return response;
        }

        public async Task<List<ProviderSettingsResponse>> GetAllPaylinkCompanyLocationAgainstProvidersAsync(string company_id, string location_id, CancellationToken cancellationToken = default)
        {
            var providers = new List<ProviderSettingsResponse>();

            if (!int.TryParse(company_id, out var CompanyId))
                throw new ArgumentException("company_id must be integer", nameof(company_id));
            if (!int.TryParse(location_id, out var LocationId))
                throw new ArgumentException("location_id must be integer", nameof(location_id));

            try
            {
                await using var conn = new NpgsqlConnection(_connectionString);
                await conn.OpenAsync(cancellationToken);

                using var cmd = new NpgsqlCommand("SELECT * FROM fn_get_paylink_cmp_loc_provider_list(@p_company_id,@p_location_id);", conn);
                cmd.Parameters.AddWithValue("p_company_id", CompanyId);
                cmd.Parameters.AddWithValue("p_location_id", LocationId);

                await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
                while (await reader.ReadAsync(cancellationToken))
                {
                    providers.Add(new ProviderSettingsResponse
                    {
                        ProviderId = reader.GetInt32(reader.GetOrdinal("providerid")),
                        ProviderName = reader.GetString(reader.GetOrdinal("providername"))
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "GetAllPaylinkCompanyLocationAgainstProvidersAsync failed");
                throw;
            }

            return providers;
        }

        public async Task<List<PaylinkProviderAccountResponse>> GetAllPaylinkCompanyandLocationsprovaccountsAsync(string company_id, string location_id, string provider_id, CancellationToken cancellationToken = default)
        {
            var provaccounts = new List<PaylinkProviderAccountResponse>();

            if (!int.TryParse(company_id, out var CompanyId))
                throw new ArgumentException("company_id must be integer", nameof(company_id));
            if (!int.TryParse(location_id, out var LocationId))
                throw new ArgumentException("location_id must be integer", nameof(location_id));
            if (!int.TryParse(provider_id, out var ProviderId))
                throw new ArgumentException("provider_id must be integer", nameof(provider_id));

            try
            {
                await using var conn = new NpgsqlConnection(_connectionString);
                await conn.OpenAsync(cancellationToken);

                using var cmd = new NpgsqlCommand(
                    "SELECT * FROM fn_get_paylink_cmp_loc_provider_account_list(@p_company_id,@p_location_id,@p_provider_id);",
                    conn);
                cmd.Parameters.AddWithValue("p_company_id", CompanyId);
                cmd.Parameters.AddWithValue("p_location_id", LocationId);
                cmd.Parameters.AddWithValue("p_provider_id", ProviderId);

                await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
                while (await reader.ReadAsync(cancellationToken))
                {
                    provaccounts.Add(new PaylinkProviderAccountResponse
                    {
                        PrvAcId = reader.GetInt32(reader.GetOrdinal("accountid")),
                        AccountName = reader.GetString(reader.GetOrdinal("accountname"))
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "GetAllPaylinkCompanyandLocationsprovaccountsAsync failed");
                throw;
            }

            return provaccounts;
        }

        public async Task<List<PosEdcMachineResponse>> GetAllPaylinkPosEdcMachineListAsync(int companyId, CancellationToken cancellationToken = default)
        {
            var posedcmachines = new List<PosEdcMachineResponse>();

            try
            {
                await using var conn = new NpgsqlConnection(_connectionString);
                await conn.OpenAsync(cancellationToken);

                using var cmd = new NpgsqlCommand("SELECT * FROM fn_get_paylink_pos_edc_machine_list(@p_company_id);", conn);
                cmd.Parameters.AddWithValue("p_company_id", companyId > 0 ? companyId : (object)DBNull.Value);

                await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
                while (await reader.ReadAsync(cancellationToken))
                {
                    posedcmachines.Add(new PosEdcMachineResponse
                    {
                        PosedcMachineId = reader.GetInt32(reader.GetOrdinal("posedcmachineid")),
                        PosedcMachineCd = reader.GetString(reader.GetOrdinal("posedcmachinecd")),
                        CompanyId = reader.GetInt32(reader.GetOrdinal("companyid")),
                        CompanyName = reader.GetString(reader.GetOrdinal("companyname")),
                        LocationId = reader.GetInt32(reader.GetOrdinal("locationid")),
                        LocationName = reader.GetString(reader.GetOrdinal("locationname")),
                        ProviderId = reader.GetInt32(reader.GetOrdinal("providerid")),
                        ProviderName = reader.GetString(reader.GetOrdinal("providername")),
                        ProviderAccountId = reader.GetInt32(reader.GetOrdinal("provideraccountid")),
                        ProviderAccountName = reader.GetString(reader.GetOrdinal("provideraccountname")),
                        PosedcMachineName = reader.GetString(reader.GetOrdinal("posedcmachinename")),
                        PosedcSno = reader.GetString(reader.GetOrdinal("posedcsno")),
                        PosedcTerminalId = reader.GetString(reader.GetOrdinal("posedcterminal_id")),
                        CreateDt = reader.IsDBNull(reader.GetOrdinal("createdt")) ? (DateTime?)null : reader.GetDateTime(reader.GetOrdinal("createdt")),
                        CreateBy = reader.IsDBNull(reader.GetOrdinal("createby")) ? null : reader.GetString(reader.GetOrdinal("createby")),
                        ModifyDt = reader.IsDBNull(reader.GetOrdinal("modifydt")) ? (DateTime?)null : reader.GetDateTime(reader.GetOrdinal("modifydt")),
                        ModifyBy = reader.IsDBNull(reader.GetOrdinal("modifyby")) ? null : reader.GetString(reader.GetOrdinal("modifyby")),
                        RecordStatus = reader.IsDBNull(reader.GetOrdinal("recordstatus")) ? null : reader.GetString(reader.GetOrdinal("recordstatus"))
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "GetAllPaylinkPosEdcMachineListAsync failed for CompanyId {CompanyId}", companyId);
                throw;
            }

            return posedcmachines;
        }

        public async Task<List<PosEdcMachineDetails>> GetPaylinkPosEdcMachineinfoAsync(string posEdcMachine, string providerName, CancellationToken cancellationToken = default)
        {
            var posedcinfo = new List<PosEdcMachineDetails>();

            try
            {
                await using var conn = new NpgsqlConnection(_connectionString);
                await conn.OpenAsync(cancellationToken);

                using var cmd = new NpgsqlCommand(
                    "SELECT * FROM fn_get_pos_edc_machine_details(@p_pos_edc_sno,@p_provider_name);",
                    conn);
                cmd.Parameters.AddWithValue("p_pos_edc_sno", posEdcMachine);
                cmd.Parameters.AddWithValue("p_provider_name", providerName);

                await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
                while (await reader.ReadAsync(cancellationToken))
                {
                    posedcinfo.Add(new PosEdcMachineDetails
                    {
                        PosedcSno = reader.IsDBNull(reader.GetOrdinal("posedcsno")) ? null : reader.GetString(reader.GetOrdinal("posedcsno")),
                        PosedcTerminalId = reader.IsDBNull(reader.GetOrdinal("posedcterminalid")) ? null : reader.GetString(reader.GetOrdinal("posedcterminalid")),
                        ProviderName = reader.IsDBNull(reader.GetOrdinal("providername")) ? null : reader.GetString(reader.GetOrdinal("providername")),
                        RazorpayUsername = reader.IsDBNull(reader.GetOrdinal("razorpayusername")) ? null : reader.GetString(reader.GetOrdinal("razorpayusername")),
                        RazorpayAppKey = reader.IsDBNull(reader.GetOrdinal("razorpayappkey")) ? null : reader.GetString(reader.GetOrdinal("razorpayappkey")),
                        PosInitiateUrl = reader.IsDBNull(reader.GetOrdinal("posinitiateurl")) ? null : reader.GetString(reader.GetOrdinal("posinitiateurl")),
                        PosStatusUrl = reader.IsDBNull(reader.GetOrdinal("posstatusurl")) ? null : reader.GetString(reader.GetOrdinal("posstatusurl")),
                        PosCancelUrl = reader.IsDBNull(reader.GetOrdinal("poscancelurl")) ? null : reader.GetString(reader.GetOrdinal("poscancelurl")),
                        WorldlineEncKey = reader.IsDBNull(reader.GetOrdinal("worldline_enckey")) ? null : reader.GetString(reader.GetOrdinal("worldline_enckey")),
                        WorldlineEncIv = reader.IsDBNull(reader.GetOrdinal("worldline_enciv")) ? null : reader.GetString(reader.GetOrdinal("worldline_enciv")),
                        PinelabUserId = reader.IsDBNull(reader.GetOrdinal("pinelab_userid")) ? null : reader.GetString(reader.GetOrdinal("pinelab_userid")),
                        PinelabMerchantId = reader.IsDBNull(reader.GetOrdinal("pinelab_merchantid")) ? null : reader.GetString(reader.GetOrdinal("pinelab_merchantid")),
                        PinelabSecurityToken = reader.IsDBNull(reader.GetOrdinal("pinelab_securitytoken")) ? null : reader.GetString(reader.GetOrdinal("pinelab_securitytoken")),
                        PinelabMerchantStorePosCode = reader.IsDBNull(reader.GetOrdinal("pinelab_merchantstoreposcode")) ? null : reader.GetString(reader.GetOrdinal("pinelab_merchantstoreposcode")),
                        IciciMid = reader.IsDBNull(reader.GetOrdinal("icici_mid")) ? null : reader.GetString(reader.GetOrdinal("icici_mid")),
                        IciciErpTranId = reader.IsDBNull(reader.GetOrdinal("icici_erp_tran_id")) ? null : reader.GetString(reader.GetOrdinal("icici_erp_tran_id")),
                        IciciErpClientId = reader.IsDBNull(reader.GetOrdinal("icici_erp_client_id")) ? null : reader.GetString(reader.GetOrdinal("icici_erp_client_id")),
                        IciciSourceId = reader.IsDBNull(reader.GetOrdinal("icici_source_id")) ? null : reader.GetString(reader.GetOrdinal("icici_source_id")),
                        HdfcMid = reader.IsDBNull(reader.GetOrdinal("hdfc_mid")) ? null : reader.GetString(reader.GetOrdinal("hdfc_mid")),
                        HdfcErpTranId = reader.IsDBNull(reader.GetOrdinal("hdfc_erp_tran_id")) ? null : reader.GetString(reader.GetOrdinal("hdfc_erp_tran_id")),
                        HdfcErpClientId = reader.IsDBNull(reader.GetOrdinal("hdfc_erp_client_id")) ? null : reader.GetString(reader.GetOrdinal("hdfc_erp_client_id")),
                        HdfcSourceId = reader.IsDBNull(reader.GetOrdinal("hdfc_source_id")) ? null : reader.GetString(reader.GetOrdinal("hdfc_source_id")),
                        MswipeUsername = reader.IsDBNull(reader.GetOrdinal("mswipe_username")) ? null : reader.GetString(reader.GetOrdinal("mswipe_username")),
                        MswipePassword = reader.IsDBNull(reader.GetOrdinal("mswipe_password")) ? null : reader.GetString(reader.GetOrdinal("mswipe_password")),
                        MswipeSaltKey = reader.IsDBNull(reader.GetOrdinal("mswipe_salt_key")) ? null : reader.GetString(reader.GetOrdinal("mswipe_salt_key")),
                        MswipeClientKey = reader.IsDBNull(reader.GetOrdinal("mswipe_clientkey")) ? null : reader.GetString(reader.GetOrdinal("mswipe_clientkey")),
                        MswipeClientSecret = reader.IsDBNull(reader.GetOrdinal("mswipe_clientsecret")) ? null : reader.GetString(reader.GetOrdinal("mswipe_clientsecret")),
                        MswipeClientCode = reader.IsDBNull(reader.GetOrdinal("mswipe_clientcode")) ? null : reader.GetString(reader.GetOrdinal("mswipe_clientcode")),
                        PaytmMid = reader.IsDBNull(reader.GetOrdinal("paytm_mid")) ? null : reader.GetString(reader.GetOrdinal("paytm_mid")),
                        PaytmMerchantKey = reader.IsDBNull(reader.GetOrdinal("paytm_merchantkey")) ? null : reader.GetString(reader.GetOrdinal("paytm_merchantkey")),
                        PhonePeproviderId = reader.IsDBNull(reader.GetOrdinal("phonepe_providerid")) ? null : reader.GetString(reader.GetOrdinal("phonepe_providerid")),
                        PhonePeSaltKey = reader.IsDBNull(reader.GetOrdinal("phonepe_saltkey")) ? null : reader.GetString(reader.GetOrdinal("phonepe_saltkey")),
                        PhonePeMerchantId = reader.IsDBNull(reader.GetOrdinal("phonepe_merchantid")) ? null : reader.GetString(reader.GetOrdinal("phonepe_merchantid")),
                        PhonePeSaltIndex = reader.IsDBNull(reader.GetOrdinal("phonepe_saltindex")) ? null : reader.GetString(reader.GetOrdinal("phonepe_saltindex")),
                        PhonePeStoreId = reader.IsDBNull(reader.GetOrdinal("phonepestoreid")) ? null : reader.GetString(reader.GetOrdinal("phonepestoreid")),
                        PhonePeStoreName = reader.IsDBNull(reader.GetOrdinal("phonepestorename")) ? null : reader.GetString(reader.GetOrdinal("phonepestorename"))
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "GetPaylinkPosEdcMachineinfoAsync failed for posEdcMachine {PosEdcMachine}, provider {ProviderName}", posEdcMachine, providerName);
                throw;
            }

            return posedcinfo;
        }

        public async Task<ApiResponse> GetPaylinkActivePosEdcMachineinfoAsync(string posEdcMachine, string providerName, CancellationToken cancellationToken = default)
        {
            var posedcinfo = new List<PosActiveEdcMachineDetails>();

            try
            {
                await using var conn = new NpgsqlConnection(_connectionString);
                await conn.OpenAsync(cancellationToken);

                using var cmd = new NpgsqlCommand(
                    "SELECT * FROM fn_get_active_pos_edc_machine_details_test(@p_pos_edc_sno, @p_provider_name);",
                    conn);

                cmd.Parameters.AddWithValue("p_pos_edc_sno", posEdcMachine);
                cmd.Parameters.AddWithValue("p_provider_name", providerName);

                await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
                while (await reader.ReadAsync(cancellationToken))
                {
                    posedcinfo.Add(new PosActiveEdcMachineDetails
                    {
                        PosEdcSno = reader.IsDBNull(reader.GetOrdinal("pos_edc_sno")) ? string.Empty : reader.GetString(reader.GetOrdinal("pos_edc_sno")),
                        PosedcTerminalId = reader.IsDBNull(reader.GetOrdinal("pos_edc_terminal_id")) ? string.Empty : reader.GetString(reader.GetOrdinal("pos_edc_terminal_id")),
                        StoreId = reader.IsDBNull(reader.GetOrdinal("storeid")) ? string.Empty : reader.GetString(reader.GetOrdinal("storeid")),
                        ClientId = reader.IsDBNull(reader.GetOrdinal("clientid")) ? string.Empty : reader.GetString(reader.GetOrdinal("clientid")),
                        MerchantPosCode = reader.IsDBNull(reader.GetOrdinal("merchantposcode")) ? string.Empty : reader.GetString(reader.GetOrdinal("merchantposcode")),

                        PaytmMid = reader.IsDBNull(reader.GetOrdinal("paytmmid")) ? string.Empty : reader.GetString(reader.GetOrdinal("paytmmid")),
                        PaytmMerchantKey = reader.IsDBNull(reader.GetOrdinal("paytmmerchantkey")) ? string.Empty : reader.GetString(reader.GetOrdinal("paytmmerchantkey")),

                        ProviderName = reader.IsDBNull(reader.GetOrdinal("provider_name")) ? string.Empty : reader.GetString(reader.GetOrdinal("provider_name")),

                        RazorpayUsername = reader.IsDBNull(reader.GetOrdinal("razorpay_username")) ? string.Empty : reader.GetString(reader.GetOrdinal("razorpay_username")),
                        RazorpayAppKey = reader.IsDBNull(reader.GetOrdinal("razorpay_appkey")) ? string.Empty : reader.GetString(reader.GetOrdinal("razorpay_appkey")),

                        PosInitiateUrl = reader.IsDBNull(reader.GetOrdinal("pos_initiate_url")) ? string.Empty : reader.GetString(reader.GetOrdinal("pos_initiate_url")),
                        PosStatusUrl = reader.IsDBNull(reader.GetOrdinal("pos_status_url")) ? string.Empty : reader.GetString(reader.GetOrdinal("pos_status_url")),
                        PosCancelUrl = reader.IsDBNull(reader.GetOrdinal("pos_cancel_url")) ? string.Empty : reader.GetString(reader.GetOrdinal("pos_cancel_url")),

                        WorldlineEncKey = reader.IsDBNull(reader.GetOrdinal("worldline_enc_key")) ? string.Empty : reader.GetString(reader.GetOrdinal("worldline_enc_key")),
                        WorldlineEncIv = reader.IsDBNull(reader.GetOrdinal("worldline_enc_iv")) ? string.Empty : reader.GetString(reader.GetOrdinal("worldline_enc_iv")),

                        PinelabUserId = reader.IsDBNull(reader.GetOrdinal("pinelab_userid")) ? string.Empty : reader.GetString(reader.GetOrdinal("pinelab_userid")),
                        PinelabMerchantId = reader.IsDBNull(reader.GetOrdinal("pinelab_merchantid")) ? string.Empty : reader.GetString(reader.GetOrdinal("pinelab_merchantid")),
                        PinelabSecurityToken = reader.IsDBNull(reader.GetOrdinal("pinelab_securitytoken")) ? string.Empty : reader.GetString(reader.GetOrdinal("pinelab_securitytoken")),
                        PinelabMerchantStorePosCode = reader.IsDBNull(reader.GetOrdinal("pinelab_merchantstoreposcode")) ? string.Empty : reader.GetString(reader.GetOrdinal("pinelab_merchantstoreposcode")),

                        IciciMid = reader.IsDBNull(reader.GetOrdinal("icici_mid")) ? string.Empty : reader.GetString(reader.GetOrdinal("icici_mid")),
                        IciciErpTranId = reader.IsDBNull(reader.GetOrdinal("icici_erp_tran_id")) ? string.Empty : reader.GetString(reader.GetOrdinal("icici_erp_tran_id")),
                        IciciErpClientId = reader.IsDBNull(reader.GetOrdinal("icici_erp_client_id")) ? string.Empty : reader.GetString(reader.GetOrdinal("icici_erp_client_id")),
                        IciciSourceId = reader.IsDBNull(reader.GetOrdinal("icici_source_id")) ? string.Empty : reader.GetString(reader.GetOrdinal("icici_source_id")),

                        IciciPaylinkMid = reader.IsDBNull(reader.GetOrdinal("icici_paylink_mid")) ? string.Empty : reader.GetString(reader.GetOrdinal("icici_paylink_mid")),
                        IciciPaylinkSecretKey = reader.IsDBNull(reader.GetOrdinal("icici_paylink_secret_key")) ? string.Empty : reader.GetString(reader.GetOrdinal("icici_paylink_secret_key")),

                        HdfcMid = reader.IsDBNull(reader.GetOrdinal("hdfc_mid")) ? string.Empty : reader.GetString(reader.GetOrdinal("hdfc_mid")),
                        HdfcErpTranId = reader.IsDBNull(reader.GetOrdinal("hdfc_erp_tran_id")) ? string.Empty : reader.GetString(reader.GetOrdinal("hdfc_erp_tran_id")),
                        HdfcErpClientId = reader.IsDBNull(reader.GetOrdinal("hdfc_erp_client_id")) ? string.Empty : reader.GetString(reader.GetOrdinal("hdfc_erp_client_id")),
                        HdfcSourceId = reader.IsDBNull(reader.GetOrdinal("hdfc_source_id")) ? string.Empty : reader.GetString(reader.GetOrdinal("hdfc_source_id")),

                        MswipeUsername = reader.IsDBNull(reader.GetOrdinal("mswipe_username")) ? string.Empty : reader.GetString(reader.GetOrdinal("mswipe_username")),
                        MswipePassword = reader.IsDBNull(reader.GetOrdinal("mswipe_password")) ? string.Empty : reader.GetString(reader.GetOrdinal("mswipe_password")),
                        MswipeSaltKey = reader.IsDBNull(reader.GetOrdinal("mswipe_salt_key")) ? string.Empty : reader.GetString(reader.GetOrdinal("mswipe_salt_key")),
                        MswipeClientKey = reader.IsDBNull(reader.GetOrdinal("mswipe_clientkey")) ? string.Empty : reader.GetString(reader.GetOrdinal("mswipe_clientkey")),
                        MswipeClientSecret = reader.IsDBNull(reader.GetOrdinal("mswipe_clientsecret")) ? string.Empty : reader.GetString(reader.GetOrdinal("mswipe_clientsecret")),
                        MswipeClientCode = reader.IsDBNull(reader.GetOrdinal("mswipe_clientcode")) ? string.Empty : reader.GetString(reader.GetOrdinal("mswipe_clientcode")),

                        PhonePeproviderId = reader.IsDBNull(reader.GetOrdinal("phonepe_providerid")) ? string.Empty : reader.GetString(reader.GetOrdinal("phonepe_providerid")),
                        PhonePeSaltKey = reader.IsDBNull(reader.GetOrdinal("phonepe_saltkey")) ? string.Empty : reader.GetString(reader.GetOrdinal("phonepe_saltkey")),
                        PhonePeSaltIndex = reader.IsDBNull(reader.GetOrdinal("phonepe_saltindex")) ? string.Empty : reader.GetString(reader.GetOrdinal("phonepe_saltindex")),
                        PhonePeMerchantId = reader.IsDBNull(reader.GetOrdinal("phonepe_merchantid")) ? string.Empty : reader.GetString(reader.GetOrdinal("phonepe_merchantid"))
                    });
                }

                if (posedcinfo.Count == 0)
                    return ApiResponseHelper.Error("No POS EDC machine details found");

                return ApiResponseHelper.Success("POS EDC machine details fetched successfully", posedcinfo);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "GetPaylinkActivePosEdcMachineinfoAsync failed for posEdcMachine {PosEdcMachine}, provider {ProviderName}", posEdcMachine, providerName);
                return ApiResponseHelper.Error("Error: " + ex.Message);
            }
        }

        public async Task<OperationResult> InsertPaylinkPosEdcMachineTransactionLogAsync(PosPaymentRequest paymentRequest, string request_name, string requestjsonPayload, string responsePayload, string status, CancellationToken cancellationToken = default)
        {
            try
            {
                await using var connection = new NpgsqlConnection(_connectionString);
                await connection.OpenAsync(cancellationToken);

                var parameters = new DynamicParameters();
                parameters.Add("p_provider_name", paymentRequest.provider);
                parameters.Add("p_customer_name", paymentRequest.customerName);
                parameters.Add("p_mobile_number", paymentRequest.mobileNumber);
                parameters.Add("p_pos_machine_sno", paymentRequest.edcsNo);
                parameters.Add("p_customer_id", paymentRequest.patientId);
                parameters.Add("p_transaction_no", paymentRequest.transactionNo);
                parameters.Add("p_amount", paymentRequest.amount);
                parameters.Add("p_transaction_type", paymentRequest.txntype);
                parameters.Add("p_pos_transaction_ref_no", paymentRequest.refernceId);
                parameters.Add("p_request_name", request_name);
                parameters.Add("p_status", status);

                parameters.Add("p_request_json", requestjsonPayload, dbType: DbType.String);
                parameters.Add("p_response_json", responsePayload, dbType: DbType.String);

                parameters.Add("p_create_by", paymentRequest.createby);
                parameters.Add("o_rows_inserted", dbType: DbType.Int32, direction: ParameterDirection.Output);

                await connection.ExecuteAsync(
                    "pr_insert_paylink_pos_edc_transaction_log",
                    parameters,
                    commandType: CommandType.StoredProcedure);

                int rowsAffected = parameters.Get<int>("o_rows_inserted");

                return new OperationResult
                {
                    code = "200",
                    status = rowsAffected > 0 ? "true" : "false",
                    message = rowsAffected > 0 ? "Pos Edc Initiate Transaction Log Created successfully." : "Pos Edc Initiate Transaction Log Created failed."
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "InsertPaylinkPosEdcMachineTransactionLogAsync failed for {@PaymentRequest}", paymentRequest);
                return new OperationResult
                {
                    code = "200",
                    status = "false",
                    message = $"Unexpected error: {ex.Message}"
                };
            }
        }

        public async Task<OperationResult> UpdateStatusPaylinkPosEdcMachineTransactionLogAsync(PosPaymentRequest paymentRequest, string requestjsonPayload, string responsePayload, CancellationToken cancellationToken = default)
        {
            try
            {
                await using var connection = new NpgsqlConnection(_connectionString);
                await connection.OpenAsync(cancellationToken);

                var parameters = new DynamicParameters();
                parameters.Add("p_pos_machine_sno", paymentRequest.edcsNo);
                parameters.Add("p_transaction_no", paymentRequest.transactionNo);
                parameters.Add("p_reference_no", paymentRequest.refernceId);
                parameters.Add("p_status_request_json", requestjsonPayload, dbType: DbType.String);
                parameters.Add("p_status_response_json", responsePayload, dbType: DbType.String);
                parameters.Add("o_rows_updated", dbType: DbType.Int32, direction: ParameterDirection.Output);

                await connection.ExecuteAsync(
                    "pr_update_status_paylink_pos_edc_transaction_log",
                    parameters,
                    commandType: CommandType.StoredProcedure);

                int rowsAffected = parameters.Get<int>("o_rows_updated");

                return new OperationResult
                {
                    code = "200",
                    status = rowsAffected > 0 ? "true" : "false",
                    message = rowsAffected > 0 ? "Pos Edc Status Transaction log Created successfully." : "Pos Edc Status Transaction log creation failed."
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "UpdateStatusPaylinkPosEdcMachineTransactionLogAsync failed for {@PaymentRequest}", paymentRequest);
                return new OperationResult
                {
                    code = "200",
                    status = "false",
                    message = $"Unexpected error: {ex.Message}"
                };
            }
        }

        public async Task<OperationResult> UpdateCancelPaylinkPosEdcMachineTransactionLogAsync(PosPaymentRequest paymentRequest, string requestjsonPayload, string responsePayload, CancellationToken cancellationToken = default)
        {
            try
            {
                await using var connection = new NpgsqlConnection(_connectionString);
                await connection.OpenAsync(cancellationToken);

                var parameters = new DynamicParameters();
                parameters.Add("p_pos_machine_sno", paymentRequest.edcsNo);
                parameters.Add("p_transaction_no", paymentRequest.transactionNo);
                parameters.Add("p_reference_no", paymentRequest.refernceId);
                parameters.Add("p_cancel_request_json", requestjsonPayload, dbType: DbType.String);
                parameters.Add("p_cancel_response_json", responsePayload, dbType: DbType.String);
                parameters.Add("o_rows_updated", dbType: DbType.Int32, direction: ParameterDirection.Output);

                await connection.ExecuteAsync(
                    "pr_cancel_status_paylink_pos_edc_transaction_log",
                    parameters,
                    commandType: CommandType.StoredProcedure);

                int rowsAffected = parameters.Get<int>("o_rows_updated");

                return new OperationResult
                {
                    code = "200",
                    status = rowsAffected > 0 ? "true" : "false",
                    message = rowsAffected > 0 ? "Pos Cancel Transaction Log Created successfully." : "Pos Cancel Transaction Log failed."
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "UpdateCancelPaylinkPosEdcMachineTransactionLogAsync failed for {@PaymentRequest}", paymentRequest);
                return new OperationResult
                {
                    code = "200",
                    status = "false",
                    message = $"Unexpected error: {ex.Message}"
                };
            }
        }

        public async Task<List<PosEdcMachineTransactionLogDetails>> GetPaylinkPosEdcTransactioninfoAsync(DateTime from_date, DateTime to_date, string company_id, CancellationToken cancellationToken = default)
        {
            var posedcinfo = new List<PosEdcMachineTransactionLogDetails>();

            if (!int.TryParse(company_id, out var CompanyId))
                CompanyId = 0;

            try
            {
                await using var conn = new NpgsqlConnection(_connectionString);
                await conn.OpenAsync(cancellationToken);

                await using var cmd = new NpgsqlCommand(
                    "SELECT * FROM fn_get_paylink_transaction_log(@p_from_date, @p_to_date, @p_company_id);",
                    conn);

                DateTime fromDate = from_date.Date;
                DateTime toDate = to_date.Date.AddDays(1); // use exclusive upper bound convention

                cmd.Parameters.AddWithValue("p_from_date", NpgsqlDbType.Timestamp, fromDate);
                cmd.Parameters.AddWithValue("p_to_date", NpgsqlDbType.Timestamp, toDate);
                cmd.Parameters.AddWithValue("p_company_id", CompanyId != 0 ? CompanyId : (object)DBNull.Value);

                await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
                while (await reader.ReadAsync(cancellationToken))
                {
                    posedcinfo.Add(new PosEdcMachineTransactionLogDetails
                    {
                        CompanyName = reader.IsDBNull(reader.GetOrdinal("company_name")) ? null : reader.GetString(reader.GetOrdinal("company_name")),
                        LocationName = reader.IsDBNull(reader.GetOrdinal("location_name")) ? null : reader.GetString(reader.GetOrdinal("location_name")),
                        ProviderName = reader.IsDBNull(reader.GetOrdinal("provider_name")) ? null : reader.GetString(reader.GetOrdinal("provider_name")),
                        AccountName = reader.IsDBNull(reader.GetOrdinal("account_name")) ? null : reader.GetString(reader.GetOrdinal("account_name")),
                        PosMachineSno = reader.IsDBNull(reader.GetOrdinal("pos_machine_sno")) ? null : reader.GetString(reader.GetOrdinal("pos_machine_sno")),
                        PosEdcTerminalId = reader.IsDBNull(reader.GetOrdinal("pos_edc_terminal_id")) ? null : reader.GetString(reader.GetOrdinal("pos_edc_terminal_id")),
                        TransactionNo = reader.IsDBNull(reader.GetOrdinal("transaction_no")) ? null : reader.GetString(reader.GetOrdinal("transaction_no")),
                        Amount = reader.IsDBNull(reader.GetOrdinal("amount")) ? null : reader.GetString(reader.GetOrdinal("amount")),
                        InitiateCustomerName = reader.IsDBNull(reader.GetOrdinal("customer_name")) ? null : reader.GetString(reader.GetOrdinal("customer_name")),
                        InitiateMobileNumber = reader.IsDBNull(reader.GetOrdinal("mobile_number")) ? null : reader.GetString(reader.GetOrdinal("mobile_number")),
                        TransactionType = reader.IsDBNull(reader.GetOrdinal("transaction_type")) ? null : reader.GetString(reader.GetOrdinal("transaction_type")),
                        PosTransactionRefNo = reader.IsDBNull(reader.GetOrdinal("pos_transaction_ref_no")) ? null : reader.GetString(reader.GetOrdinal("pos_transaction_ref_no")),
                        RequestName = reader.IsDBNull(reader.GetOrdinal("request_name")) ? null : reader.GetString(reader.GetOrdinal("request_name")),
                        CreatedBy = reader.IsDBNull(reader.GetOrdinal("create_by")) ? null : reader.GetString(reader.GetOrdinal("create_by")),
                        CreatedDate = reader.IsDBNull(reader.GetOrdinal("create_dt")) ? (DateTime?)null : reader.GetDateTime(reader.GetOrdinal("create_dt")),
                        TransactionLogId = reader.IsDBNull(reader.GetOrdinal("transaction_log_id")) ? (int?)null : reader.GetInt32(reader.GetOrdinal("transaction_log_id"))
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "GetPaylinkPosEdcTransactioninfoAsync failed");
                throw;
            }

            return posedcinfo;
        }

        public async Task<List<PosEdcMachineTransactionLogJsonDtls>> GetPaylinkPosEdcTransactionJsonLoginfoAsync(int transaction_log_id, CancellationToken cancellationToken = default)
        {
            var posedcinfo = new List<PosEdcMachineTransactionLogJsonDtls>();

            try
            {
                await using var conn = new NpgsqlConnection(_connectionString);
                await conn.OpenAsync(cancellationToken);

                await using var cmd = new NpgsqlCommand(
                    "SELECT * FROM fn_paylink_transaction_json_log(@p_transaction_log_id);",
                    conn);

                cmd.Parameters.AddWithValue("p_transaction_log_id", NpgsqlDbType.Integer, transaction_log_id);

                await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
                while (await reader.ReadAsync(cancellationToken))
                {
                    posedcinfo.Add(new PosEdcMachineTransactionLogJsonDtls
                    {
                        TransactionLogId = reader.GetInt32(reader.GetOrdinal("transactionlogid")),
                        JsonRequest = reader.IsDBNull(reader.GetOrdinal("requestjson")) ? null : reader.GetString(reader.GetOrdinal("requestjson")),
                        JsonResponse = reader.IsDBNull(reader.GetOrdinal("responsejson")) ? null : reader.GetString(reader.GetOrdinal("responsejson")),
                        StatusJsonRequest = reader.IsDBNull(reader.GetOrdinal("statusreqjson")) ? null : reader.GetString(reader.GetOrdinal("statusreqjson")),
                        StatusJsonResponse = reader.IsDBNull(reader.GetOrdinal("statusrespjson")) ? null : reader.GetString(reader.GetOrdinal("statusrespjson")),
                        CancelJsonRequest = reader.IsDBNull(reader.GetOrdinal("cancelreqjson")) ? null : reader.GetString(reader.GetOrdinal("cancelreqjson")),
                        CancelJsonResponse = reader.IsDBNull(reader.GetOrdinal("cancelrespjson")) ? null : reader.GetString(reader.GetOrdinal("cancelrespjson"))
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "GetPaylinkPosEdcTransactionJsonLoginfoAsync failed");
                throw;
            }

            return posedcinfo;
        }

        #endregion Pos Machine Master

        #region Login
        public async Task<LoginResponse> ValidateUserAsync(LoginRequest login, CancellationToken cancellationToken = default)
        {
            try
            {
                await using var conn = new NpgsqlConnection(_connectionString);
                await conn.OpenAsync(cancellationToken);

                using var cmd = new NpgsqlCommand(
                    "SELECT * FROM fn_paylink_login_user(@p_username, @p_password);", conn);
                cmd.Parameters.AddWithValue("p_username", login.UserName);
                cmd.Parameters.AddWithValue("p_password", login.Password);

                await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);

                if (await reader.ReadAsync(cancellationToken))
                {
                    var status = reader.GetString(reader.GetOrdinal("status"));
                    var response = new LoginResponse
                    {
                        code = "200",
                        status = status,
                        message = reader.GetString(reader.GetOrdinal("message"))
                    };

                    if (status == "true")
                    {
                        response.UserId = reader.IsDBNull(reader.GetOrdinal("user_id")) ? (int?)null : reader.GetInt32(reader.GetOrdinal("user_id"));
                        response.USERNAME = reader.IsDBNull(reader.GetOrdinal("username")) ? null : reader.GetString(reader.GetOrdinal("username"));
                        response.USERCODE = reader.IsDBNull(reader.GetOrdinal("user_code")) ? null : reader.GetString(reader.GetOrdinal("user_code"));
                        response.DISPLAYNAME = reader.IsDBNull(reader.GetOrdinal("display_name")) ? null : reader.GetString(reader.GetOrdinal("display_name"));
                        response.HOSPITALCD = reader.IsDBNull(reader.GetOrdinal("hospital_cd")) ? null : reader.GetString(reader.GetOrdinal("hospital_cd"));
                        response.HOSPITALNAME = reader.IsDBNull(reader.GetOrdinal("hospital_name")) ? null : reader.GetString(reader.GetOrdinal("hospital_name"));
                        response.USERTYPE = reader.IsDBNull(reader.GetOrdinal("user_type")) ? null : reader.GetString(reader.GetOrdinal("user_type"));
                        response.COSTCENTERCD = reader.IsDBNull(reader.GetOrdinal("costcenter_cd")) ? null : reader.GetString(reader.GetOrdinal("costcenter_cd"));
                        response.COMPANYID = reader.IsDBNull(reader.GetOrdinal("company_id")) ? (int?)null : reader.GetInt32(reader.GetOrdinal("company_id"));
                    }

                    return response;
                }

                return new LoginResponse
                {
                    code = "200",
                    status = "false",
                    message = "Login failed"
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ValidateUserAsync failed for username {Username}", login?.UserName);
                return new LoginResponse
                {
                    code = "200",
                    status = "false",
                    message = $"Error: {ex.Message}"
                };
            }
        }

        #endregion Login
    }

}