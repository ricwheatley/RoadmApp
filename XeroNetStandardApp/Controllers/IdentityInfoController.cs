// IdentityInfo.cs
// Ric Wheatley – May 2025
#nullable enable

using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xero.NetStandard.OAuth2.Api;
using Xero.NetStandard.OAuth2.Config;
using XeroNetStandardApp.Models;
using XeroNetStandardApp.Services;

namespace XeroNetStandardApp.Controllers
{
    /// <summary>Endpoint-control panel for IdentityInfo.</summary>
    public class IdentityInfo : ApiAccessorController<IdentityApi>
    {
        private readonly IPollingService _pollingService;
        private readonly ICallLogService _callLogs;
        private readonly ILogger<IdentityInfo> _log;

        public IdentityInfo(
            IOptions<XeroConfiguration> xeroConfig,
            TokenService tokenService,
            IPollingService pollingService,
            ICallLogService callLogs,
            ILogger<BaseXeroOAuth2Controller> baseLogger,
            ILogger<IdentityInfo> log)
            : base(xeroConfig, tokenService, baseLogger)
        {
            _pollingService = pollingService;
            _callLogs = callLogs;
            _log = log;
        }

        // GET /IdentityInfo
        public async Task<IActionResult> Index()
        {
            var token = await GetValidXeroTokenAsync();
            if (token == null) return RedirectToAction("Index", "Authorization");

            var connections = await Api.GetConnectionsAsync(token.AccessToken);

            var model = new EndpointControlPanelViewModel
            {
                Tenants = connections.Select(c => new OrgTenant
                {
                    TenantId = c.TenantId.ToString(),
                    OrgName = c.TenantName,
                    Schedules = new Dictionary<string, string>(), // populated later
                    Scopes = token.Scopes?.ToList() ?? new List<string>()
                }).ToList(),

                Endpoints = new List<EndpointOption>
                {
                    new() { Key = "accounts",            DisplayName = "Accounts" },
                    new() { Key = "banktransfers",       DisplayName = "Bank Transfers" },
                    new() { Key = "batchpayments",       DisplayName = "Batch Payments" },
                    new() { Key = "brandingthemes",      DisplayName = "Branding Themes" },
                    new() { Key = "budgets",             DisplayName = "Budgets" },
                    new() { Key = "contactgroups",       DisplayName = "Contact Groups" },
                    new() { Key = "contacts",            DisplayName = "Contacts" },
                    new() { Key = "creditnotes",         DisplayName = "Credit Notes" },
                    new() { Key = "currencies",          DisplayName = "Currencies" },
                    new() { Key = "employees",           DisplayName = "Employees" },
                    new() { Key = "invoicereminders",    DisplayName = "Invoice Reminders" },
                    new() { Key = "invoices",            DisplayName = "Invoices" },
                    new() { Key = "items",               DisplayName = "Items" },
                    new() { Key = "journals",            DisplayName = "Journals" },
                    new() { Key = "linkedtransactions",  DisplayName = "Linked Transactions" },
                    new() { Key = "manualjournals",      DisplayName = "Manual Journals" },
                    new() { Key = "organisation",        DisplayName = "Organisation" },
                    new() { Key = "overpayments",        DisplayName = "Overpayments" },
                    new() { Key = "payments",            DisplayName = "Payments" },
                    new() { Key = "paymentservices",     DisplayName = "Payment Services" },
                    new() { Key = "prepayments",         DisplayName = "Prepayments" },
                    new() { Key = "purchaseorders",      DisplayName = "Purchase Orders" },
                    new() { Key = "quotes",              DisplayName = "Quotes" },
                    new() { Key = "repeatinginvoices",   DisplayName = "Repeating Invoices" },
                    new() { Key = "taxrates",            DisplayName = "Tax Rates" },
                    new() { Key = "trackingcategories",  DisplayName = "Tracking Categories" },
                    new() { Key = "users",               DisplayName = "Users" },
                    new() { Key = "assets",              DisplayName = "Assets" },
                    new() { Key = "assettypes",          DisplayName = "Asset Types" },
                    new() { Key = "settings",            DisplayName = "Asset Settings" }
                }
            };

            var stats = await _pollingService.GetPollingStatsAsync();

            // Kill duplicate / empty org stats then convert to dictionary keyed by TenantId
            var filteredStats = stats
                .Where(s => s.OrganisationId != Guid.Empty)
                .GroupBy(s => s.OrganisationId)
                .Select(g => g.First())
                .ToDictionary(s => s.OrganisationId.ToString());

            model.Stats = filteredStats;

            // Pull the latest call time for all tenants so the summary table
            // matches the data shown on other pages.
            var allTenantIds = model.Tenants
                .Where(t => !string.IsNullOrWhiteSpace(t.TenantId))
                .Select(t => Guid.Parse(t.TenantId!))
                .ToList();

            if (allTenantIds.Count > 0)
            {
                var latest = await _callLogs.GetLatestStatsAsync(allTenantIds);
                foreach (var kv in latest)
                {
                    var tid = kv.Key.ToString();
                    if (model.Stats.TryGetValue(tid, out var s))
                    {
                        s.LastCall = kv.Value.LastCallUtc;
                        if (s.RecordsInserted == 0)
                            s.RecordsInserted = kv.Value.RowsInserted;
                    }
                    else
                    {
                        model.Stats[tid] = new PollingStats
                        {
                            OrganisationId   = kv.Key,
                            LastCall         = kv.Value.LastCallUtc,
                            EndpointsSuccess = 0,
                            EndpointsFail    = 0,
                            RecordsInserted  = kv.Value.RowsInserted
                        };
                    }
                }
            }

            foreach (var stat in model.Stats)
            {
                _log.LogInformation(
                    "Model has org {org} last run {dt} success {succ} fail {fail} rows {rows}",
                    stat.Key, stat.Value.LastCall, stat.Value.EndpointsSuccess,
                    stat.Value.EndpointsFail, stat.Value.RecordsInserted);
            }

            return View(model);
        }

        // POST /IdentityInfo/BulkTrigger
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> BulkTrigger(
            [FromForm] string? tenantId,
            [FromForm] Dictionary<string, string[]>? selected)
        {
            // 1. No endpoints selected? Short-circuit and bounce.
            if (selected == null || selected.Count == 0)
            {
                TempData["Message"] = "No endpoints selected.";
                return RedirectToAction(nameof(Index));
            }

            // 2. Normalise tenantId: if null or blank then assume:
            //    * "ALL" when multiple tenants were posted
            //    * the single tenant key when only one was posted
            if (string.IsNullOrWhiteSpace(tenantId))
            {
                tenantId = selected.Count == 1
                    ? selected.Keys.First()
                    : "ALL";
            }

            var inserted = new Dictionary<string, int>();
            var callTime = DateTimeOffset.UtcNow;

            // 3. Execute endpoint polling
            if (tenantId == "ALL")
            {
                foreach (var (tId, epArray) in selected)
                {
                    if (string.IsNullOrWhiteSpace(tId) || epArray == null) continue;

                    var count = 0;
                    foreach (var ep in epArray.Where(e => !string.IsNullOrWhiteSpace(e)))
                    {
                        count += await _pollingService.RunEndpointAsync(tId, ep, callTime);
                    }
                    inserted[tId] = count;
                }
            }
            else if (selected.TryGetValue(tenantId, out var endpointsForTenant) &&
                     endpointsForTenant?.Length > 0)
            {
                var count = 0;
                foreach (var ep in endpointsForTenant.Where(e => !string.IsNullOrWhiteSpace(e)))
                {
                    count += await _pollingService.RunEndpointAsync(tenantId, ep, callTime);
                }
                inserted[tenantId] = count;
            }
            else
            {
                TempData["Message"] = "No endpoints selected.";
                return RedirectToAction(nameof(Index));
            }

            // 4. Store results in TempData so the view can flash them to the user
            foreach (var kv in inserted)
            {
                var nowEpoch = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString();
                TempData[$"PollLast_{kv.Key}"] = nowEpoch;
                TempData[$"PollRows_{kv.Key}"] = kv.Value.ToString();
            }

            // 5. Summarise the run for logging & UI status line
            var runStats = (await _pollingService.GetPollingStatsForRunAsync(callTime))
                .Where(s => s.OrganisationId != Guid.Empty)
                .GroupBy(s => s.OrganisationId)
                .Select(g => g.First())
                .ToDictionary(s => s.OrganisationId.ToString());

            var token = await GetValidXeroTokenAsync();
            var orgNames = token?.Tenants?
                               .ToDictionary(t => t.TenantId.ToString(), t => t.TenantName)
                           ?? new Dictionary<string, string>();

            foreach (var stat in runStats)
            {
                _log.LogInformation(
                    "Model has org {org} last run {dt} success {succ} fail {fail} rows {rows}",
                    stat.Key, stat.Value.LastCall, stat.Value.EndpointsSuccess,
                    stat.Value.EndpointsFail, stat.Value.RecordsInserted);
            }

            var summaries = inserted.Select(kv =>
            {
                runStats.TryGetValue(kv.Key, out var stats);
                var name = orgNames.TryGetValue(kv.Key, out var n) ? n : kv.Key;
                return $"{stats?.EndpointsSuccess ?? 0} successful endpoint(s) polled, " +
                       $"{stats?.EndpointsFail ?? 0} endpoint(s) failed, and " +
                       $"{stats?.RecordsInserted ?? 0} records inserted for {name}";
            });

            TempData["RunStatus"] = "Manual run completed with " + string.Join("; ", summaries);

            TempData["Message"] = tenantId == "ALL"
                ? $"Triggered {selected.Sum(kv => kv.Value?.Length ?? 0)} endpoint(s) across {selected.Count} organisation(s)."
                : $"Triggered {(selected.TryGetValue(tenantId, out var a) ? a.Length : 0)} endpoint(s) for {tenantId}.";

            return RedirectToAction(nameof(Index));
        }

        // GET /IdentityInfo/Delete
        [HttpGet]
        public async Task<IActionResult> Delete(string connectionId)
        {
            var token = await GetValidXeroTokenAsync();
            if (token == null) return BadRequest("No valid Xero access token on file.");

            if (string.IsNullOrWhiteSpace(connectionId) ||
                !Guid.TryParse(connectionId, out var connGuid))
            {
                return BadRequest("Invalid connection id.");
            }

            await Api.DeleteConnectionAsync(token.AccessToken, connGuid);
            _tokenService.DestroyToken();
            return RedirectToAction("Index", "Home");
        }
    }
}
