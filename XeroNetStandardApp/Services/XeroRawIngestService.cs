// Services/XeroRawIngestService.cs
//
// Generic ingest pipeline that now:
//   • Detects an EndpointConfig.Status[] array
//   • Loops over each status value (REGISTERED, DRAFT, …)
//   • Adds  status=<value>  before any other query-string parts
//   • Keeps paging, modified-since and rate-limit logic exactly as before.
//
// Ric Wheatley – May 2025

using Dapper;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading.Tasks;
using Xero.NetStandard.OAuth2.Token;
using XeroNetStandardApp.Models;

namespace XeroNetStandardApp.Services
{
    /// <summary>
    /// Pulls data from Xero endpoints (Accounting, Assets, etc.) and stores each page
    /// as individual JSON payloads in the trunk schema.
    /// </summary>
    public sealed class XeroRawIngestService : IXeroRawIngestService
    {
        private readonly string _connString;
        private readonly XeroSyncOptions _opt;
        private readonly IHttpClientFactory _factory;
        private readonly ILogger<XeroRawIngestService> _log;
        private readonly IConfiguration _cfg;
        private readonly TokenService _tokenService;
        private static readonly TimeSpan _expiryBuffer = TimeSpan.FromMinutes(2);

        public XeroRawIngestService(
            IConfiguration cfg,
            IOptions<XeroSyncOptions> opt,
            IHttpClientFactory factory,
            ILogger<XeroRawIngestService> log,
            TokenService tokenService)
        {
            _cfg = cfg;
            _opt = opt.Value;
            _factory = factory;
            _log = log;
            _tokenService = tokenService;
            _connString = cfg.GetConnectionString("Postgres")
                           ?? Environment.GetEnvironmentVariable("POSTGRES_CONN_STRING")
                           ?? throw new InvalidOperationException("Postgres conn string missing");
        }

        // ─────────────────────────────────────────────
        //  PUBLIC ENTRY-POINTS
        // ─────────────────────────────────────────────

        public Task<int> RunOnceAsync(string tenantId)
            => RunCoreAsync(tenantId, _opt.Endpoints);

        public Task<int> RunOnceAsync(string tenantId, string endpointKey)
        {
            var ep = _opt.Endpoints
                         .Where(e => string.Equals(
                                        e.Name.Replace(" ", string.Empty, StringComparison.OrdinalIgnoreCase),
                                        endpointKey,
                                        StringComparison.OrdinalIgnoreCase))
                         .ToArray();

            if (ep.Length == 0)
            {
                _log.LogWarning("Unknown endpoint key {Key} – falling back to full ingest", endpointKey);
                return RunOnceAsync(tenantId);
            }

            return RunCoreAsync(tenantId, ep);
        }

        // ─────────────────────────────────────────────
        //  CORE PIPELINE
        // ─────────────────────────────────────────────
        private async Task<int> RunCoreAsync(string tenantId, IEnumerable<EndpointConfig> endpointsToRun)
        {
            var token = _tokenService.RetrieveToken()
                        ?? throw new InvalidOperationException("No saved Xero token on disk.");

            if (TokenNeedsRefresh(token))
                token = await RefreshAsync(token);

            var http = _factory.CreateClient(nameof(XeroRawIngestService));
            http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token.AccessToken);
            http.DefaultRequestHeaders.Add("xero-tenant-id", tenantId);

            var totalRows = 0;

            await using var conn = new NpgsqlConnection(_connString);
            await conn.OpenAsync();

            foreach (var ep in endpointsToRun)
                totalRows += await IngestEndpointAsync(http, conn, ep, Guid.Parse(tenantId));

            return totalRows;
        }

        // ─────────────────────────────────────────────
        //  TOKEN HELPERS
        // ─────────────────────────────────────────────
        private static bool TokenNeedsRefresh(XeroOAuth2Token tok)
            => DateTime.UtcNow >= tok.ExpiresAtUtc - _expiryBuffer;

        private async Task<XeroOAuth2Token> RefreshAsync(XeroOAuth2Token current)
        {
            if (string.IsNullOrWhiteSpace(current.RefreshToken))
                throw new InvalidOperationException("No refresh-token available.");

            var clientId = _cfg["XeroConfiguration:ClientId"] ?? string.Empty;
            var clientSecret = _cfg["XeroConfiguration:ClientSecret"] ?? string.Empty;

            var http = _factory.CreateClient("XeroIdentity");
            var content = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string,string>("grant_type",    "refresh_token"),
                new KeyValuePair<string,string>("refresh_token", current.RefreshToken),
                new KeyValuePair<string,string>("client_id",     clientId),
                new KeyValuePair<string,string>("client_secret", clientSecret)
            });

            var resp = await http.PostAsync("https://identity.xero.com/connect/token", content);
            var raw = await resp.Content.ReadAsStringAsync();

            if (!resp.IsSuccessStatusCode)
            {
                _log.LogError("Token refresh failed: {Status} {Body}", resp.StatusCode, raw);
                resp.EnsureSuccessStatusCode();
            }

            var refreshed = JsonSerializer.Deserialize<XeroOAuth2Token>(raw)!;
            var expiresIn = JsonDocument.Parse(raw).RootElement.GetProperty("expires_in").GetInt32();
            refreshed.ExpiresAtUtc = DateTime.UtcNow.AddSeconds(expiresIn);

            refreshed.Tenants = current.Tenants;
            refreshed.IdToken = current.IdToken;

            _tokenService.StoreToken(refreshed);
            _log.LogInformation("Token refreshed, now expires {Expiry:u}", refreshed.ExpiresAtUtc);
            return refreshed;
        }

        // ─────────────────────────────────────────────
        //  ENDPOINT INGEST
        // ─────────────────────────────────────────────
        private async Task<int> IngestEndpointAsync(HttpClient http,
                                                    NpgsqlConnection conn,
                                                    EndpointConfig endpoint,
                                                    Guid tenantId)
        {
            var table = $"{_opt.Schema}.{endpoint.Name.ToLower()}";
            var rows = 0;

            try
            {
                var since = await GetLastFetchedAsync(conn, table);
                if (endpoint.SupportsModifiedSince && since != null)
                    http.DefaultRequestHeaders.IfModifiedSince = since;

                // ── loop over each Status value (or a single null value if none provided)
                var statusList = (endpoint.Status?.Length ?? 0) > 0
                                 ? endpoint.Status
                                 : new[] { (string?)null };

                foreach (var status in statusList)
                    rows += await IngestForOneStatusAsync(http, conn, endpoint, tenantId, table, status, since);
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "{Endpoint}: aborted", endpoint.Name);
            }
            finally
            {
                http.DefaultRequestHeaders.Remove("If-Modified-Since");
            }

            return rows;
        }

        private async Task<int> IngestForOneStatusAsync(HttpClient http,
                                                        NpgsqlConnection conn,
                                                        EndpointConfig endpoint,
                                                        Guid tenantId,
                                                        string table,
                                                        string? status,
                                                        DateTimeOffset? since)
        {
            var rows = 0;
            var path = endpoint.Name.Replace(" ", string.Empty);
            var baseUrl = endpoint.ApiUrl.TrimEnd('/') + "/";
            var queryBase = status == null ? string.Empty : $"status={status}&";
            var pagePart = endpoint.SupportsPagination ? "page=1" : string.Empty;
            var url = $"{baseUrl}{path}?{queryBase}{pagePart}".TrimEnd('?');

            var resp = await http.GetAsync(url);
            var body = await resp.Content.ReadAsStringAsync();

            if (resp.StatusCode == HttpStatusCode.NotModified)
            {
                _log.LogInformation("{Endpoint} {Status}: up-to-date (since {Since})",
                                    endpoint.Name, status ?? "-", since?.ToString("u"));
                return 0;
            }

            if (!resp.IsSuccessStatusCode)
            {
                _log.LogWarning("{Endpoint} {Status}: {Code} – {Body}",
                                endpoint.Name, status ?? "-", resp.StatusCode, body);
                return 0;
            }

            var totalPages = GetTotalPages(resp);
            rows += await InsertPageAsync(conn, table, endpoint, 1, body, tenantId);

            for (var page = 2; page <= totalPages; page++)
            {
                await Task.Delay(1100); // stay within Xero’s 60-calls-per-minute ceiling
                resp = await http.GetAsync($"{baseUrl}{path}?{queryBase}page={page}");
                body = await resp.Content.ReadAsStringAsync();

                if (!resp.IsSuccessStatusCode)
                {
                    _log.LogWarning("{Endpoint} {Status} page {Page}: {Code} – {Body}",
                                    endpoint.Name, status ?? "-", page, resp.StatusCode, body);
                    break;
                }

                rows += await InsertPageAsync(conn, table, endpoint, page, body, tenantId);
            }

            _log.LogInformation("{Endpoint} {Status}: {Rows} rows inserted over {Pages} pages",
                                endpoint.Name, status ?? "-", rows, totalPages);

            return rows;
        }

        // ─────────────────────────────────────────────
        //  SMALL HELPERS
        // ─────────────────────────────────────────────
        private static async Task<DateTimeOffset?> GetLastFetchedAsync(NpgsqlConnection conn, string table)
        {
            var last = await conn.ExecuteScalarAsync<DateTime?>(
                           $"SELECT MAX(fetched_at) FROM {table};");

            return last == null ? null : new DateTimeOffset(last.Value, TimeSpan.Zero);
        }

        private static int GetTotalPages(HttpResponseMessage resp)
        {
            foreach (var k in new[] { "xero-pagination-page-count", "xero-total-pages" })
                if (resp.Headers.TryGetValues(k, out var v) &&
                    int.TryParse(v.FirstOrDefault(), out var pages) && pages > 0)
                    return pages;
            return 1;
        }

        private async Task<int> InsertPageAsync(NpgsqlConnection conn,
                                        string table,
                                        EndpointConfig endpoint,
                                        int page,
                                        string bodyJson,
                                        Guid tenantId)
        {
            var root = JsonDocument.Parse(bodyJson).RootElement;

            JsonElement arrayElement;
            if (string.IsNullOrEmpty(endpoint.ResponseKey) && root.ValueKind == JsonValueKind.Array)
            {
                // Response is a bare JSON array – use it as-is
                arrayElement = root;
            }
            else
            {
                var key = endpoint.ResponseKey
                          ?? (endpoint.Name.EndsWith("s", StringComparison.Ordinal)
                                 ? endpoint.Name
                                 : endpoint.Name + "s");

                if (!root.TryGetProperty(key, out arrayElement))
                    return 0;                       // nothing we recognise – skip safely
            }

            const string sql = @"INSERT INTO {0} (page_number, payload_json, tenant_id)
                         VALUES (@Page, @Payload::jsonb, @TenantId);";

            foreach (var el in arrayElement.EnumerateArray())        // ✔ now arrayElement
                await conn.ExecuteAsync(string.Format(sql, table),
                                        new { Page = page, Payload = el.GetRawText(), TenantId = tenantId });

            return arrayElement.GetArrayLength();                    // ✔ now arrayElement
        }
    }
}
