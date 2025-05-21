using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Dapper;
using Microsoft.Extensions.Configuration;
using Npgsql;
using XeroNetStandardApp.Models;

namespace XeroNetStandardApp.Services
{
    public class CallLogService : ICallLogService
    {
        private readonly string _connString;

        public CallLogService(IConfiguration configuration)
        {
            _connString = configuration.GetConnectionString("Postgres")
                         ?? Environment.GetEnvironmentVariable("POSTGRES_CONN_STRING")
                         ?? throw new InvalidOperationException("Postgres conn string missing");
        }

        public async Task<IReadOnlyList<ApiCallLogEntry>> GetLogsAsync(Guid organisationId)
        {
            const string sql = @"SELECT call_time, endpoint, rows_inserted, status_code, success, error_message
                                  FROM utils.api_call_log
                                 WHERE organisation_id = @OrgId
                                 ORDER BY call_time desc, endpoint asc;";
            await using var conn = new NpgsqlConnection(_connString);
            var result = await conn.QueryAsync<ApiCallLogEntry>(sql, new { OrgId = organisationId });
            return result.AsList();
        }
    }
}
