using Microsoft.AspNetCore.Mvc;
using System;
using System.Threading.Tasks;
using Xero.NetStandard.OAuth2.Api;
using Xero.NetStandard.OAuth2.Config;
using Microsoft.Extensions.Options;
using XeroNetStandardApp.Services;
using System.Collections.Generic;
using System.Linq;
using XeroNetStandardApp.Models;
using Microsoft.Extensions.Logging;

namespace XeroNetStandardApp.Controllers
{
    public class IdentityInfo : ApiAccessorController<IdentityApi>
    {
        private readonly IPollingService _pollingService;


        public IdentityInfo(
            IOptions<XeroConfiguration> xeroConfig,
            TokenService tokenService,
            IPollingService pollingService,
            ILogger<BaseXeroOAuth2Controller> logger)
            : base(xeroConfig, tokenService, logger)
        {
            _pollingService = pollingService;
        }



        // GET: /IdentityInfo/
        public async Task<IActionResult> Index()
        {
            var token = await GetXeroOAuth2TokenAsync();
            if (token == null || string.IsNullOrEmpty(token.AccessToken))
            {
                return RedirectToAction("Index", "Authorization");
            }

            // Get list of authorised tenant connections
            var connections = await Api.GetConnectionsAsync(token.AccessToken);

            var model = new EndpointControlPanelViewModel
            {
                Tenants = connections.Select(c => new OrgTenant
                {
                    TenantId = c.TenantId.ToString(),
                    OrgName = c.TenantName,
                    Schedules = new Dictionary<string, string>() // TODO: wire up later
                }).ToList(),

                Endpoints = new List<EndpointOption>
                {
                    new() { Key = "accounts", DisplayName = "Accounts" },
                    new() { Key = "banktransfers", DisplayName = "Bank Transfers" },
                    new() { Key = "batchpayments", DisplayName = "Batch Payments" },
                    new() { Key = "brandingthemes", DisplayName = "Branding Themes" },
                    new() { Key = "budgets", DisplayName = "Budgets" },
                    new() { Key = "contactgroups", DisplayName = "Contact Groups" },
                    new() { Key = "contacts", DisplayName = "Contacts" },
                    new() { Key = "creditnotes", DisplayName = "Credit Notes" },
                    new() { Key = "currencies", DisplayName = "Currencies" },
                    new() { Key = "employees", DisplayName = "Employees" },
                    new() { Key = "invoicereminders", DisplayName = "Invoice Reminders" },
                    new() { Key = "invoices", DisplayName = "Invoices" },
                    new() { Key = "items", DisplayName = "Items" },
                    new() { Key = "journals", DisplayName = "Journals" },
                    new() { Key = "linkedtransactions", DisplayName = "Linked Transactions" },
                    new() { Key = "manualjournals", DisplayName = "Manual Journals" },
                    new() { Key = "organisation", DisplayName = "Organisation" },
                    new() { Key = "overpayments", DisplayName = "Overpayments" },
                    new() { Key = "payments", DisplayName = "Payments" },
                    new() { Key = "paymentservices", DisplayName = "Payment Services" },
                    new() { Key = "prepayments", DisplayName = "Prepayments" },
                    new() { Key = "purchaseorders", DisplayName = "Purchase Orders" },
                    new() { Key = "quotes", DisplayName = "Quotes" },
                    new() { Key = "repeatinginvoices", DisplayName = "Repeating Invoices" },
                    new() { Key = "taxrates", DisplayName = "Tax Rates" },
                    new() { Key = "trackingcategories", DisplayName = "Tracking Categories" },
                    new() { Key = "users", DisplayName = "Users" }

                    //new() { Key = "payruns", DisplayName = "Pay Runs" }, // AU/UK/NZ payroll
                    //new() { Key = "assets", DisplayName = "Fixed Assets" }
                }
            };

            return View(model);
        }

        [HttpPost]
        public async Task<IActionResult> BulkTrigger(
        string tenantId,
        [FromForm] Dictionary<string, string[]> selected)
        {
            // nothing ticked?
            if (selected == null || selected.Count == 0)
            {
                TempData["Message"] = "No endpoints selected.";
                return RedirectToAction("Index");
            }

            // ── Run for ALL tenants ─────────────────────────────
            if (tenantId == "ALL")
            {
                foreach (var (tId, endpoints) in selected)
                {
                    foreach (var ep in endpoints)
                        await _pollingService.RunEndpointAsync(tId, ep);
                }
            }
            // ── Run for the single tenant whose button was pressed ─
            else if (selected.TryGetValue(tenantId, out var endpointsForTenant))
            {
                foreach (var ep in endpointsForTenant)
                    await _pollingService.RunEndpointAsync(tenantId, ep);
            }
            else
            {
                TempData["Message"] = "No endpoints selected.";
                return RedirectToAction("Index");
            }

            TempData["Message"] =
            tenantId == "ALL"
                ? $"Triggered polling for {selected.Sum(kv => kv.Value.Length)} endpoint(s) across {selected.Count} organisation(s)."
                : $"Triggered {selected[tenantId].Length} endpoint(s) for {tenantId}.";
            return RedirectToAction("Index");
        }



        // GET: /Identity#Delete
        [HttpGet]
        public async Task<IActionResult> Delete(string connectionId)
        {
            if (XeroToken == null || string.IsNullOrEmpty(XeroToken.AccessToken))
                return BadRequest("No valid Xero access-token on file.");

            if (string.IsNullOrEmpty(connectionId) || !Guid.TryParse(connectionId, out var connGuid))
                return BadRequest("Invalid connection id.");

            await Api.DeleteConnectionAsync(XeroToken.AccessToken, connGuid);

            _tokenService.DestroyToken();

            return RedirectToAction("Index", "Home");
        }

    }
}