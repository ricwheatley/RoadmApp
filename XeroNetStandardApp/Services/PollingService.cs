using Dapper;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Npgsql;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
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
                         ?? Environment.GetEnvironmentVariable("POSTGRES_CONN_STRING")
                         ?? throw new InvalidOperationException("Postgres conn string missing");
        }

        public async Task<int> RunEndpointAsync(string tenantId,
                                        string endpointKey,
                                        DateTimeOffset callTime)
        {
            // Ensure we still have a token before doing any work
            XeroOAuth2Token? tok = _tokenService.RetrieveToken();
            if (tok == null || string.IsNullOrEmpty(tok.AccessToken))
                throw new InvalidOperationException("No valid Xero token on file.");

            int totalInserted = 0;
            HttpStatusCode? overallCode = null;
            bool success = false;
            string? errorMessage = null;

            try
            {
                // NEW: list of detailed reports rather than a raw int
                IReadOnlyList<EndpointIngestReport> reports =
                    await _ingestSvc.RunOnceAsync(tenantId, endpointKey);

                totalInserted = reports.Sum(r => r.RowsInserted);

                // Failures are anything not OK (200) or Not-Modified (304)
                var failures = reports.Where(r =>
                                  r.ResponseCode != HttpStatusCode.OK &&
                                  r.ResponseCode != HttpStatusCode.NotModified)
                               .ToList();

                success = failures.Count == 0;

                // Decide an overall status code for logging / persistence
                if (success)
                {
                    overallCode = totalInserted == 0
                                  ? HttpStatusCode.NotModified
                                  : HttpStatusCode.OK;
                }
                else
                {
                    overallCode = failures.First().ResponseCode; // first failure’s code
                    errorMessage = string.Join(" | ", failures.Select(f =>
                        $"{f.EndpointName} {f.Status ?? "-"} → {(int)f.ResponseCode} {f.ErrorDetail}"));
                }

                // ── Logging summary
                if (success)
                {
                    if (overallCode == HttpStatusCode.NotModified)
                        _log.LogInformation("{Endpoint}: no new records since last modified-date (tenant {Tenant})",
                                            endpointKey, tenantId);
                    else
                        _log.LogInformation("{Endpoint}: {Count} records inserted for tenant {Tenant}",
                                            endpointKey, totalInserted, tenantId);
                }
                else
                {
                    _log.LogWarning("{Endpoint}: {FailCount} failures, {Inserted} records inserted (tenant {Tenant})",
                                    endpointKey, failures.Count, totalInserted, tenantId);
                }
            }
            catch (Exception ex)
            {
                // Unexpected exception during ingest
                success = false;
                overallCode = HttpStatusCode.InternalServerError;
                errorMessage = ex.Message;
                _log.LogError(ex,
                              "Polling failed for {Endpoint} and tenant {Tenant}",
                              endpointKey, tenantId);
            }

            // Persist the outcome – retains original LogResultAsync signature
            await LogResultAsync(
                tenantId,
                endpointKey,
                totalInserted,
                callTime,
                overallCode.HasValue ? (int)overallCode.Value : null,
                success,
                errorMessage);

            _log.LogInformation("Polled {Endpoint} for tenant {Tenant}", endpointKey, tenantId);

            return totalInserted;          // unchanged public contract
        }


        public async Task<IReadOnlyList<PollingStats>> GetPollingStatsAsync()
        {
            const string sql = @"
                WITH last_run AS (
                    SELECT organisation_id, MAX(call_time) AS last_call
                    FROM utils.api_call_log
                    GROUP BY organisation_id
                )
                SELECT
                    c.organisation_id   AS OrganisationId,
                    l.last_call         AS LastCall,
                    SUM(CASE WHEN c.status_code = 200 THEN 1 ELSE 0 END) AS EndpointsSuccess,
                    SUM(CASE WHEN c.status_code <> 200 THEN 1 ELSE 0 END) AS EndpointsFail,
                    SUM(c.rows_inserted)                         AS RecordsInserted
                FROM utils.api_call_log c
                JOIN last_run l
                  ON c.organisation_id = l.organisation_id
                 AND c.call_time = l.last_call
                GROUP BY c.organisation_id, l.last_call;
            ";

            await using var conn = new NpgsqlConnection(_connString);
            var result = await conn.QueryAsync<PollingStats>(sql);
            return result.AsList();
        }

        public async Task<IReadOnlyList<PollingStats>> GetPollingStatsForRunAsync(DateTimeOffset callTime)
        {
            const string sql = @"
                WITH last_run AS (
                    SELECT organisation_id, MAX(call_time) AS last_call
                    FROM utils.api_call_log
                    GROUP BY organisation_id
                )
                SELECT
                    c.organisation_id   AS OrganisationId,
                    l.last_call         AS LastCall,
                    SUM(CASE WHEN c.status_code = 200 THEN 1 ELSE 0 END) AS EndpointsSuccess,
                    SUM(CASE WHEN c.status_code <> 200 THEN 1 ELSE 0 END) AS EndpointsFail,
                    SUM(c.rows_inserted)                         AS RecordsInserted
                FROM utils.api_call_log c
                JOIN last_run l
                  ON c.organisation_id = l.organisation_id
                 AND c.call_time = l.last_call
                WHERE c.call_time = @CallTime
                GROUP BY c.organisation_id, l.last_call;
            ";

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
