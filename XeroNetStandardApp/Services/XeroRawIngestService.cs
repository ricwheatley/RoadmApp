using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading.Tasks;
using Dapper;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql;
using Xero.NetStandard.OAuth2.Models;
using Xero.NetStandard.OAuth2.Token;
using XeroNetStandardApp.IO;
using XeroNetStandardApp.Models;

namespace XeroNetStandardApp.Services;

public interface IXeroRawIngestService
{
    Task<int> RunOnceAsync(string accessToken, string tenantId);
}

public sealed class XeroRawIngestService : IXeroRawIngestService
{
    private readonly string _connString;
    private readonly XeroSyncOptions _opt;
    private readonly IHttpClientFactory _factory;
    private readonly ILogger<XeroRawIngestService> _log;
    private readonly IConfiguration _cfg;

    public XeroRawIngestService(IConfiguration cfg,
                                IOptions<XeroSyncOptions> opt,
                                IHttpClientFactory factory,
                                ILogger<XeroRawIngestService> log)
    {
        _cfg = cfg;
        _opt = opt.Value;
        _factory = factory;
        _log = log;
        _connString = cfg.GetConnectionString("Postgres")
                     ?? throw new InvalidOperationException("Postgres conn string missing");
    }

    // ─────────────────────────────────────────────
    //  PUBLIC ENTRY-POINT
    // ─────────────────────────────────────────────
    public async Task<int> RunOnceAsync(string accessToken, string tenantId)
    {
        var store = LocalStorageTokenIO.Instance;
        var token = store.GetToken()
                    ?? throw new InvalidOperationException("No saved Xero token on disk.");

        if (TokenNeedsRefresh(token))
        {
            token = await RefreshAsync(token);
            store.StoreToken(token);
        }

        var http = _factory.CreateClient(nameof(XeroRawIngestService));
        http.BaseAddress = new Uri("https://api.xero.com/api.xro/2.0/");
        http.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token.AccessToken);
        http.DefaultRequestHeaders.Add("xero-tenant-id", tenantId);

        var totalRows = 0;

        await using var conn = new NpgsqlConnection(_connString);
        await conn.OpenAsync();

        var refreshedThisRun = false;

        foreach (var ep in _opt.Endpoints)
        {
            var rows = await IngestEndpointAsync(http, conn, ep, Guid.Parse(tenantId));

            if (rows == -401 && !refreshedThisRun)
            {
                token = await RefreshAsync(token);
                store.StoreToken(token);
                http.DefaultRequestHeaders.Authorization =
                    new AuthenticationHeaderValue("Bearer", token.AccessToken);

                refreshedThisRun = true;
                rows = await IngestEndpointAsync(http, conn, ep, Guid.Parse(tenantId)); // retry
            }

            totalRows += Math.Max(rows, 0);
        }

        return totalRows;
    }

    // ─────────────────────────────────────────────
    //  TOKEN HELPERS
    // ─────────────────────────────────────────────
    private static bool TokenNeedsRefresh(XeroOAuth2Token tok)
    {
        // refresh two minutes before expiry
        return DateTime.UtcNow > tok.ExpiresAtUtc.AddMinutes(-2);
    }

    private async Task<XeroOAuth2Token> RefreshAsync(XeroOAuth2Token current)
    {
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
        resp.EnsureSuccessStatusCode();

        var json = await resp.Content.ReadAsStringAsync();
        var refreshed = JsonSerializer.Deserialize<XeroOAuth2Token>(json)!;

        // work out absolute expiry (identity payload only gives you seconds)
        var expiresIn = JsonDocument.Parse(json).RootElement.GetProperty("expires_in").GetInt32();
        refreshed.ExpiresAtUtc = DateTime.UtcNow.AddSeconds(expiresIn);

        _log.LogInformation("Access token refreshed (expires at {Expiry})",
            refreshed.ExpiresAtUtc.ToString("u"));

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
           
            var path = endpoint.Name.Replace(" ", string.Empty);
            var url = endpoint.SupportsPagination ? $"{path}?page=1" : path;
            var resp1 = await http.GetAsync(url);

            if (resp1.StatusCode == HttpStatusCode.NotModified)
            {
                _log.LogInformation("{Endpoint}: up-to-date (since {Since})",
                    endpoint.Name,
                    since?.ToString("u"));
                return 0;
            }

            
            if (resp1.StatusCode == HttpStatusCode.Unauthorized)
            {
                _log.LogWarning("{Endpoint}: 401 – scope missing or feature disabled, skipped", endpoint.Name);
                return 0;         // ← DON’T return -401
            }

            resp1.EnsureSuccessStatusCode();

            var totalPages = GetTotalPages(resp1);
            rows += await InsertPageAsync(conn, table, endpoint.Name, 1, resp1, tenantId);

            for (var page = 2; page <= totalPages; page++)
            {
                await Task.Delay(1100); // stay inside Xero's 60-calls-per-minute limit
                var resp = await http.GetAsync($"{path}?page={page}");
                resp.EnsureSuccessStatusCode();
                rows += await InsertPageAsync(conn, table, endpoint.Name, page, resp, tenantId);
            }

            _log.LogInformation("{Endpoint}: {Rows} rows inserted over {Pages} pages",
                                endpoint.Name, rows, totalPages);
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

    // ─────────────────────────────────────────────
    //  SMALL HELPERS
    // ─────────────────────────────────────────────
    private static async Task<DateTimeOffset?> GetLastFetchedAsync(
        NpgsqlConnection conn, string table)
    {
        var last = await conn.ExecuteScalarAsync<DateTime?>(
                       $"SELECT MAX(fetched_at) FROM {table};");

        return last == null
            ? null
            : new DateTimeOffset(last.Value, TimeSpan.Zero); // stored as UTC
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
                                            string endpoint,
                                            int page,
                                            HttpResponseMessage resp,
                                            Guid tenantId)
    {
        var root = JsonDocument.Parse(await resp.Content.ReadAsStringAsync()).RootElement;
        var key = endpoint.EndsWith("s") ? endpoint : endpoint + "s";
        if (!root.TryGetProperty(key, out var arr)) return 0;

        const string sql = @"INSERT INTO {0} (page_number, payload_json, tenant_id)
                             VALUES (@Page, @Payload::jsonb, @TenantId);";

        foreach (var el in arr.EnumerateArray())
            await conn.ExecuteAsync(string.Format(sql, table),
                                    new { Page = page, Payload = el.GetRawText(), TenantId = tenantId });

        return arr.GetArrayLength();
    }
}
