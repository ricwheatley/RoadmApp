// IdentityInfo.cs
// Ric Wheatley – May 2025

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
        private readonly ILogger<IdentityInfo> _log;

        public IdentityInfo(
            IOptions<XeroConfiguration> xeroConfig,
            TokenService tokenService,
            IPollingService pollingService,
            ILogger<BaseXeroOAuth2Controller> baseLogger,
            ILogger<IdentityInfo> log)
            : base(xeroConfig, tokenService, baseLogger)
        {
            _pollingService = pollingService;
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
                    Schedules = new Dictionary<string, string>() // later
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

            // Filter out stats with empty OrganisationId, then handle duplicates (just take first)
            var filteredStats = stats
                .Where(s => s.OrganisationId != Guid.Empty)
                .GroupBy(s => s.OrganisationId)
                .Select(g => g.First())
                .ToDictionary(s => s.OrganisationId.ToString());

            model.Stats = filteredStats;
            foreach (var stat in filteredStats)
                _log.LogInformation("Model has org {org} last run {dt} success {succ} fail {fail} rows {rows}",
                    stat.Key, stat.Value.LastCall, stat.Value.EndpointsSuccess, stat.Value.EndpointsFail, stat.Value.RecordsInserted);
            Console.WriteLine("Test message from Ric filteredStats");
            _log.LogError("Test message from Ric filteredStats");
            return View(model);
        }


        // POST /IdentityInfo/BulkTrigger
        [HttpPost]
        public async Task<IActionResult> BulkTrigger(
            string tenantId,
            [FromForm] Dictionary<string, string[]> selected)
        {
            if (selected is null || selected.Count == 0)
            {
                TempData["Message"] = "No endpoints selected.";
                return RedirectToAction("Index");
            }

            // Run requested polling and capture number of rows inserted
            var inserted = new Dictionary<string, int>();
            var callTime = DateTimeOffset.UtcNow;

            if (tenantId == "ALL")
            {
                foreach (var (tId, endpoints) in selected)
                {
                    var count = 0;
                    foreach (var ep in endpoints)
                        count += await _pollingService.RunEndpointAsync(tId, ep, callTime);
                    inserted[tId] = count;
                }
            }
            else if (selected.TryGetValue(tenantId, out var endpointsForTenant))
            {
                var count = 0;
                foreach (var ep in endpointsForTenant)
                    count += await _pollingService.RunEndpointAsync(tenantId, ep, callTime);
                inserted[tenantId] = count;
            }
            else
            {
                TempData["Message"] = "No endpoints selected.";
                return RedirectToAction("Index");
            }

            foreach (var kv in inserted)
            {
                Console.WriteLine($"Run time: {DateTime.UtcNow:o}");
                // use epoch ms for easier JS parsing
                TempData[$"PollLast_{kv.Key}"] =
                    DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString();
                Console.WriteLine($"Total rows: {kv.Value}");
                TempData[$"PollRows_{kv.Key}"] = kv.Value.ToString();
            }

            // Capture stats for this run
            var runStats = (await _pollingService.GetPollingStatsForRunAsync(callTime))
                .Where(s => s.OrganisationId != Guid.Empty)
                .GroupBy(s => s.OrganisationId)
                .Select(g => g.First())
                .ToDictionary(s => s.OrganisationId.ToString());

            var token = await GetValidXeroTokenAsync();
            var orgNames = token?.Tenants?.ToDictionary(t => t.TenantId.ToString(), t => t.TenantName)
                           ?? new Dictionary<string, string>();
            foreach (var stat in runStats)
                _log.LogInformation("Model has org {org} last run {dt} success {succ} fail {fail} rows {rows}",
                    stat.Key, stat.Value.LastCall, stat.Value.EndpointsSuccess, stat.Value.EndpointsFail, stat.Value.RecordsInserted);
            Console.WriteLine("Test message from Ric runStats");
            _log.LogError("Test message from Ric runStats");
            var summaries = new List<string>();
            foreach (var kv in inserted)
            {
                runStats.TryGetValue(kv.Key, out var stats);

                var name = orgNames.TryGetValue(kv.Key, out var n) ? n : kv.Key;
                summaries.Add($"{stats?.EndpointsSuccess ?? 0} successful endpoint(s) polled, {stats?.EndpointsFail ?? 0} endpoint(s) failed, and {stats?.RecordsInserted ?? 0} records inserted for {name}");
            }

            TempData["RunStatus"] = "Manual run completed with " + string.Join("; ", summaries);

            TempData["Message"] =
                tenantId == "ALL"
                    ? $"Triggered {selected.Sum(kv => kv.Value.Length)} endpoint(s) across {selected.Count} organisation(s)."
                    : $"Triggered {selected[tenantId].Length} endpoint(s) for {tenantId}.";
            return RedirectToAction("Index");
        }

        // GET /IdentityInfo/Delete
        [HttpGet]
        public async Task<IActionResult> Delete(string connectionId)
        {
            var token = await GetValidXeroTokenAsync();
            if (token == null) return BadRequest("No valid Xero access token on file.");

            if (string.IsNullOrWhiteSpace(connectionId) || !Guid.TryParse(connectionId, out var connGuid))
                return BadRequest("Invalid connection id.");

            await Api.DeleteConnectionAsync(token.AccessToken, connGuid);
            _tokenService.DestroyToken();
            return RedirectToAction("Index", "Home");
        }
    }
}
