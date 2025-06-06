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
        private const int _maxPageSize = 1000;     // Xero’s hard limit

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

        public Task<IReadOnlyList<EndpointIngestReport>> RunOnceAsync(string tenantId)
            => RunCoreAsync(tenantId, _opt.Endpoints);

        public Task<IReadOnlyList<EndpointIngestReport>> RunOnceAsync(string tenantId, string endpointKey)
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
        private async Task<IReadOnlyList<EndpointIngestReport>> RunCoreAsync(
            string tenantId,
            IEnumerable<EndpointConfig> endpointsToRun)
        {
            var token = _tokenService.RetrieveToken()
                        ?? throw new InvalidOperationException("No saved Xero token on disk.");

            if (TokenNeedsRefresh(token))
                token = await RefreshAsync(token);

            var http = _factory.CreateClient(nameof(XeroRawIngestService));
            http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token.AccessToken);
            http.DefaultRequestHeaders.Add("xero-tenant-id", tenantId);

            var all = new List<EndpointIngestReport>();

            await using var conn = new NpgsqlConnection(_connString);
            await conn.OpenAsync();

            foreach (var ep in endpointsToRun)
                all.AddRange(await IngestEndpointAsync(http, conn, ep, Guid.Parse(tenantId)));

            return all;
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
        private async Task<IReadOnlyList<EndpointIngestReport>> IngestEndpointAsync(
            HttpClient http,
            NpgsqlConnection conn,
            EndpointConfig endpoint,
            Guid tenantId)
        {
            var table = $"{_opt.Schema}.{endpoint.Name.ToLower()}";
            var reports = new List<EndpointIngestReport>();

            try
            {
                var since = await GetLastFetchedAsync(conn, table);
                if (endpoint.SupportsModifiedSince && !endpoint.SupportsOffset && since != null)
                    http.DefaultRequestHeaders.IfModifiedSince = since;

                var statusList = (endpoint.Status?.Length ?? 0) > 0
                                 ? endpoint.Status
                                 : new[] { (string?)null };

                foreach (var status in statusList ?? Array.Empty<string?>())
                    reports.Add(await IngestForOneStatusAsync(http, conn, endpoint,
                                                              tenantId, table, status, since));
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "{Endpoint}: aborted", endpoint.Name);
            }
            finally
            {
                http.DefaultRequestHeaders.Remove("If-Modified-Since");
            }

            return reports;
        }

        // ─────────────────────────────────────────────
        //  ONE STATUS, ALL PAGES  *or*  OFFSET LOOP
        // ─────────────────────────────────────────────
        private async Task<EndpointIngestReport> IngestForOneStatusAsync(
            HttpClient http,
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
            var query = status == null ? string.Empty : $"status={status}&";
            var url = string.Empty;                  // declare once
            HttpResponseMessage resp = null!;
            string body = string.Empty;

            /* ──────────────────────────────────────────
               1. OFFSET-BASED LOOP  (Journals only)
               ────────────────────────────────────────── */
            if (endpoint.SupportsOffset)
            {
                // start from max(journal_number) already in ODS
                var offset = await GetLastJournalNumberAsync(conn, tenantId) ?? 0;
                
                do
                {
                    url = $"{baseUrl}{path}?{query}offset={offset}";
                    resp = await http.GetAsync(url);
                    body = await resp.Content.ReadAsStringAsync();

                    _log.LogInformation("{Endpoint} offset {Offset}: {Code}",
                                        endpoint.Name, offset, resp.StatusCode);

                    if (!resp.IsSuccessStatusCode)
                    {
                        _log.LogWarning("{Endpoint} offset {Offset}: {Code} – {Body}",
                                        endpoint.Name, offset, resp.StatusCode, body);
                        return new EndpointIngestReport(endpoint.Name, status, 0,
                                                         false, resp.StatusCode, body.Truncate(400));
                    }

                    var inserted = await InsertPageAsync(conn, table, endpoint, offset, body, tenantId);
                    rows += inserted;

                    offset = GetNextOffset(body);         // returns -1 when array empty
                }
                while (offset >= 0);

                _log.LogInformation("{Endpoint}: {Rows} rows inserted via offset loop.",
                                    endpoint.Name, rows);

                return new EndpointIngestReport(endpoint.Name, status, rows,
                                                 rows == 0, HttpStatusCode.OK, null);
            }

            /* ──────────────────────────────────────────
               2. PAGE-BASED LOOP  (all other endpoints)
               ────────────────────────────────────────── */
            var pageSize = endpoint.PageSize ?? _maxPageSize;
            var first = endpoint.SupportsPagination ? $"page=1&pageSize={pageSize}" : string.Empty;
            url = $"{baseUrl}{path}?{query}{first}".TrimEnd('?');

            resp = await http.GetAsync(url);
            body = await resp.Content.ReadAsStringAsync();

            _log.LogInformation("Headers for {Endpoint} {Status} page 1:",
                                endpoint.Name, status ?? "-");
            foreach (var h in resp.Headers)
                _log.LogInformation("  {Key}: {Value}", h.Key, string.Join(",", h.Value));

            // 304 = nothing new
            if (resp.StatusCode == HttpStatusCode.NotModified)
            {
                _log.LogInformation("{Endpoint} {Status}: up-to-date (since {Since})",
                                    endpoint.Name, status ?? "-", since?.ToString("u"));

                return new EndpointIngestReport(endpoint.Name, status, 0,
                                                 true, resp.StatusCode, null);
            }

            // 200 OK – page loop
            if (resp.IsSuccessStatusCode)
            {
                var totalPages = GetTotalPages(body);
                rows += await InsertPageAsync(conn, table, endpoint, 1, body, tenantId);

                for (var page = 2; page <= totalPages; page++)
                {
                    await Task.Delay(1100);   // stay below 60-calls/min
                    var nextUrl = $"{baseUrl}{path}?{query}page={page}&pageSize={pageSize}";
                    resp = await http.GetAsync(nextUrl);
                    body = await resp.Content.ReadAsStringAsync();

                    if (!resp.IsSuccessStatusCode)
                    {
                        _log.LogWarning("{Endpoint} {Status} page {Page}: {Code} – {Body}",
                                        endpoint.Name, status ?? "-", page, resp.StatusCode, body);
                        break;
                    }

                    rows += await InsertPageAsync(conn, table, endpoint, page, body, tenantId);
                }

                _log.LogInformation("{Endpoint} {Status}: {Rows} rows inserted.",
                                    endpoint.Name, status ?? "-", rows);

                return new EndpointIngestReport(endpoint.Name, status, rows,
                                                 false, HttpStatusCode.OK, null);
            }

            // any other status code (401, 403, 429, 5xx, …)
            _log.LogWarning("{Endpoint} {Status}: {Code} – {Body}",
                            endpoint.Name, status ?? "-", resp.StatusCode, body);

            return new EndpointIngestReport(endpoint.Name, status, 0,
                                             false, resp.StatusCode, body.Truncate(500));
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

        /// <summary>
        /// Highest JournalNumber already present in ods.journals for this tenant/organisation.
        /// Returns null the very first time (no rows yet).
        /// </summary>
        private static async Task<int?> GetLastJournalNumberAsync(
            NpgsqlConnection conn,
            Guid organisationId)
        {
            const string sql = @"
                SELECT MAX(journal_number)
                FROM   ods.journals
                WHERE  organisation_id = @OrgId;";

            return await conn.ExecuteScalarAsync<int?>(sql, new { OrgId = organisationId });
        }

        /// <returns>The JournalNumber of the last item, or -1 if the batch is empty.</returns>
        private static int GetNextOffset(string bodyJson)
        {
            var root = JsonDocument.Parse(bodyJson).RootElement;

            if (!root.TryGetProperty("Journals", out var list) || list.GetArrayLength() == 0)
                return -1;     // empty → stop looping

            var last = list[list.GetArrayLength() - 1];
            return last.TryGetProperty("JournalNumber", out var numElem) &&
                   numElem.TryGetInt32(out int num)
                   ? num
                   : -1;
        }

        private int GetTotalPages(string responseBody)
        {
            try
            {
                using var doc = JsonDocument.Parse(responseBody);
                var root = doc.RootElement;

                if (root.ValueKind == JsonValueKind.Object &&
                    root.TryGetProperty("pagination", out var pagination) &&
                    pagination.ValueKind == JsonValueKind.Object &&
                    pagination.TryGetProperty("pageCount", out var pageCountElem))
                {
                    int pages;
                    if (pageCountElem.ValueKind == JsonValueKind.Number &&
                        pageCountElem.TryGetInt32(out pages) && pages > 0)
                        return pages;

                    if (pageCountElem.ValueKind == JsonValueKind.String)
                    {
                        var text = pageCountElem.GetString();
                        if (!string.IsNullOrEmpty(text) &&
                            int.TryParse(text, out pages) && pages > 0)
                            return pages;
                    }

                    _log.LogWarning("Invalid pageCount value in response; defaulting to 1.");
                    return 1;
                }

                _log.LogWarning("Response body has no pagination info; defaulting to 1 page.");
                return 1;
            }
            catch (Exception ex)
            {
                _log.LogWarning(ex, "Failed to parse pagination; defaulting to 1 page.");
                return 1;
            }
        }

        private async Task<int> InsertPageAsync(
            NpgsqlConnection conn,
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
                arrayElement = root; // bare array
            }
            else
            {
                var key = endpoint.ResponseKey
                       ?? (endpoint.Name.EndsWith("s", StringComparison.Ordinal)
                           ? endpoint.Name
                           : endpoint.Name + "s");

                if (!root.TryGetProperty(key, out arrayElement))
                    return 0;   // nothing recognised – skip safely
            }

            const string sql =
                @"INSERT INTO {0} (page_number, payload_json, tenant_id)
                  VALUES (@Page, @Payload::jsonb, @TenantId);";

            foreach (var el in arrayElement.EnumerateArray())
                await conn.ExecuteAsync(string.Format(sql, table),
                                        new { Page = page, Payload = el.GetRawText(), TenantId = tenantId });

            return arrayElement.GetArrayLength();
        }
    }
}

// ─────────────────────────────────────────────
//  EXTENSION(S)
// ─────────────────────────────────────────────
internal static class StringExtensions
{
    /// <summary>Return at most <paramref name="max"/> characters, adding an ellipsis if truncated.</summary>
    public static string Truncate(this string value, int max)
        => value.Length <= max ? value : value[..max] + " …";
}
