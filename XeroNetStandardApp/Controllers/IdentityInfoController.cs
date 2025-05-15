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

namespace XeroNetStandardApp.Controllers
{
    public class IdentityInfo : ApiAccessorController<IdentityApi>
    {

        public IdentityInfo(IOptions<XeroConfiguration> xeroConfig, TokenService tokenService)
            : base(xeroConfig, tokenService)
        {
        }

        // GET: /IdentityInfo/
        public async Task<IActionResult> Index()
        {
            var token = await GetXeroOAuth2Token();
            if (token == null || string.IsNullOrEmpty(token.AccessToken))
            {
                return RedirectToAction("Index", "Home");
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
        public IActionResult BulkTrigger(string tenantId, [FromForm] Dictionary<string, string[]> selected)
        {
            if (tenantId == "ALL")
            {
                // Handle logic for all tenants if needed
                // For now, redirecting to Index
                return RedirectToAction("Index");
            }
            else if (!string.IsNullOrEmpty(tenantId) && selected.TryGetValue(tenantId, out var endpointsForTenant))
            {
                // Redirect to RawSyncController's Run method with selected endpoints
                return RedirectToAction("Run", "RawSync", new { tenantId = tenantId, selectedEndpoints = endpointsForTenant });
            }
            else
            {
                TempData["Message"] = "No endpoints selected.";
                return RedirectToAction("Index");
            }
        }


        // GET: /Identity#Delete
        [HttpGet]
        public async Task<IActionResult> Delete(string connectionId)
        {
            await Api.DeleteConnectionAsync(XeroToken.AccessToken, Guid.Parse(connectionId));

            _tokenService.DestroyToken();

            return RedirectToAction("Index", "Home");
        }

    }
}