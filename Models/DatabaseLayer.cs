using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Npgsql;
using System;
using System.Data;

namespace PaymentAPI.Models
{
    public class DatabaseLayer
    {
        private readonly string _connectionString;
        private readonly string _databaseProvider;

        public DatabaseLayer(IConfiguration configuration)
        {
            if (configuration is null)
                throw new ArgumentNullException(nameof(configuration));

            _databaseProvider = configuration["DatabaseProvider"] ?? string.Empty;

            if (string.Equals(_databaseProvider, "postgres", StringComparison.OrdinalIgnoreCase))
            {
                _connectionString = configuration.GetConnectionString("Postgres")
                    ?? throw new InvalidOperationException("Postgres connection string 'Postgres' is not configured.");
            }
            else if (string.Equals(_databaseProvider, "sql", StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(_databaseProvider, "sqlserver", StringComparison.OrdinalIgnoreCase))
            {
                _connectionString = configuration.GetConnectionString("SqlServer")
                    ?? throw new InvalidOperationException("SQL Server connection string 'SqlServer' is not configured.");
            }
            else
            {
                throw new InvalidOperationException("Unsupported or missing DatabaseProvider configuration. Expected 'postgres' or 'sql'.");
            }
        }

        public string DatabaseProvider => _databaseProvider;
        public string ConnectionString => _connectionString;

        /// <summary>
        /// Creates and returns a new open IDbConnection for the configured provider.
        /// Caller is responsible for disposing the returned connection.
        /// </summary>
        public IDbConnection CreateConnection()
        {
            IDbConnection conn = _databaseProvider.Equals("postgres", StringComparison.OrdinalIgnoreCase)
                ? new NpgsqlConnection(_connectionString)
                : new SqlConnection(_connectionString);

            return conn;
        }
    }
}