using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using Dapper;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Npgsql;
using Xero.NetStandard.OAuth2.Token;
using XeroNetStandardApp.Models;

namespace XeroNetStandardApp.Services
{
    public class PollingService : IPollingService
    {
        private readonly TokenService _tokenService;
        private readonly IXeroRawIngestService _ingestSvc;
        private readonly ILogger<PollingService> _log;
        private readonly string _connString;

        public PollingService(
            TokenService tokenService,
            IXeroRawIngestService ingestSvc,
            ILogger<PollingService> log,
            IConfiguration cfg)
        {
            _tokenService = tokenService;
            _ingestSvc = ingestSvc;
            _log = log;
            _connString = cfg.GetConnectionString("Postgres")
                         ?? throw new InvalidOperationException("Postgres conn string missing");
        }

        public async Task<int> RunEndpointAsync(string tenantId, string endpointKey, DateTimeOffset callTime)
        {
            XeroOAuth2Token? tok = _tokenService.RetrieveToken();
            if (tok == null || string.IsNullOrEmpty(tok.AccessToken))
                throw new InvalidOperationException("No valid Xero token on file.");

            int rows = 0;
            int? statusCode = null;
            bool success;
            string? errorMessage = null;

            try
            {
                rows = await _ingestSvc.RunOnceAsync(tenantId, endpointKey);
                statusCode = 200;
                success = true;
            }
            catch (Exception ex)
            {
                success = false;
                statusCode = 500;
                errorMessage = ex.Message;
                _log.LogError(ex, "Polling failed for {Endpoint} and tenant {Tenant}", endpointKey, tenantId);
            }

            await LogResultAsync(tenantId, endpointKey, rows, callTime, statusCode, success, errorMessage);

            _log.LogInformation("Polled {Endpoint} for tenant {Tenant}", endpointKey, tenantId);
            return rows;
        }

        public async Task<IReadOnlyList<PollingStats>> GetPollingStatsAsync()
        {
            const string sql = @"SELECT
    organisation_id, 
    max(call_time) AS last_call,
    SUM(CASE WHEN status_code = 200 THEN 1 ELSE 0 END) AS endpoints_success,
    SUM(CASE WHEN status_code <> 200 THEN 1 ELSE 0 END) AS endpoints_fail,
    SUM(rows_inserted) AS records_inserted
FROM 
    utils.api_call_log as clog
WHERE
	call_time = (select max(call_time) from utils.api_call_log as xclog where xclog.organisation_id = clog.organisation_id)
GROUP BY 
    organisation_id;";

            await using var conn = new NpgsqlConnection(_connString);
            var result = await conn.QueryAsync<PollingStats>(sql);
            return result.AsList();
        }

        public async Task<IReadOnlyList<PollingStats>> GetPollingStatsForRunAsync(DateTimeOffset callTime)
        {
            const string sql = @"SELECT
    organisation_id, 
    max(call_time) AS last_call,
    SUM(CASE WHEN status_code = 200 THEN 1 ELSE 0 END) AS endpoints_success,
    SUM(CASE WHEN status_code <> 200 THEN 1 ELSE 0 END) AS endpoints_fail,
    SUM(rows_inserted) AS records_inserted
FROM 
    utils.api_call_log as clog
WHERE
	call_time = (select max(call_time) from utils.api_call_log as xclog where xclog.organisation_id = clog.organisation_id)
    and call_time = @CallTime
GROUP BY 
    organisation_id;";

            await using var conn = new NpgsqlConnection(_connString);
            var result = await conn.QueryAsync<PollingStats>(sql, new { CallTime = callTime });
            return result.AsList();
        }

        private async Task LogResultAsync(string tenantId, string endpointKey, int rows, DateTimeOffset callTime, int? statusCode, bool success, string? errorMessage)
        {
            try
            {
                if (!Guid.TryParse(tenantId, out var orgGuid) || orgGuid == Guid.Empty)
                {
                    _log.LogWarning("Attempted to log polling result with invalid or empty tenantId: '{TenantId}' for endpoint {EndpointKey}", tenantId, endpointKey);
                    return;
                }

                await using var conn = new NpgsqlConnection(_connString);
                const string sql = @"INSERT INTO utils.api_call_log (organisation_id, endpoint, rows_inserted, call_time, status_code, success, error_message)
                              VALUES (@OrgId, @Endpoint, @Rows, @CallTime, @StatusCode, @Success, @Error);";

                await conn.ExecuteAsync(sql, new
                {
                    OrgId = orgGuid,
                    Endpoint = endpointKey,
                    Rows = rows,
                    CallTime = callTime,
                    StatusCode = statusCode,
                    Success = success,
                    Error = errorMessage
                });
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "Failed to record api_call_log for {Endpoint}", endpointKey);
            }
        }

    }
}
