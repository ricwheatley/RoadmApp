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

        public async Task<IDictionary<Guid, CallStats>> GetLatestStatsAsync(IEnumerable<Guid> tenantIds)
        {
            const string sql = @"
        WITH latest_time AS (
            SELECT organisation_id,
                   MAX(call_time) AS call_time
            FROM   utils.api_call_log
            WHERE  organisation_id = ANY(@Ids)
            GROUP  BY organisation_id
        )
        SELECT l.organisation_id  AS OrganisationId,
               l.call_time        AS CallTime,
               COALESCE(SUM(a.rows_inserted),0) AS RowsInserted
        FROM   latest_time l
        JOIN   utils.api_call_log a
               ON a.organisation_id = l.organisation_id
              AND a.call_time       = l.call_time
        GROUP  BY l.organisation_id, l.call_time;";

            await using var conn = new NpgsqlConnection(_connString);
            var rows = await conn.QueryAsync(sql, new { Ids = tenantIds });

            var dict = new Dictionary<Guid, CallStats>();
            foreach (var r in rows)
            {
                dict[(Guid)r.organisationid] = new CallStats
                {
                    LastCallUtc = (DateTimeOffset)r.calltime,
                    RowsInserted = (int)r.rowsinserted
                };
            }
            return dict;
        }
    }
}
