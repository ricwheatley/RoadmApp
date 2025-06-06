using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using XeroNetStandardApp.Helpers;
using XeroNetStandardApp.Models;
using XeroNetStandardApp.Services;

namespace XeroNetStandardApp.Controllers;

public class PollingConfigController : Controller
{
    private readonly TokenService _tokenService;
    private readonly IPollingSettingsService _settings;

    public PollingConfigController(TokenService tokenService, IPollingSettingsService settings)
    {
        _tokenService = tokenService;
        _settings = settings;
    }

    // GET /PollingConfig
    public async Task<IActionResult> Index()
    {
        var token = _tokenService.RetrieveToken();
        if (token == null) return RedirectToAction("Index", "Authorization");

        var endpoints = new List<EndpointOption>
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
        };

        var tenants = token.Tenants.Select(t => new OrgTenant
            {
                TenantId = t.TenantId.ToString(),
                OrgName = t.TenantName,
                Scopes = token.GetScopes()
        }).ToList();

        var model = new PollingConfigViewModel
        {
            Tenants = tenants,
            Endpoints = endpoints
        };

        var ids = tenants
            .Select(t => Guid.TryParse(t.TenantId, out var g) ? g : Guid.Empty)
            .Where(g => g != Guid.Empty)
            .ToArray();
        var settings = await _settings.GetManyAsync(ids);
        model.Settings = settings.ToDictionary(kv => kv.Key.ToString(), kv => kv.Value);

        return View(model);
    }

    // POST /PollingConfig/SaveSchedule
    [HttpPost]
    public async Task<IActionResult> SaveSchedule(
        string tenantId,
        [FromForm] Dictionary<string, string[]> selected,
        [FromForm] Dictionary<string, string> freq,
        [FromForm] Dictionary<string, string> time)
    {
        if (!Guid.TryParse(tenantId, out var orgId))
        {
            TempData["Message"] = "Invalid organisation id.";
            return RedirectToAction("Index");
        }

        selected.TryGetValue(tenantId, out var endpoints);
        freq.TryGetValue(tenantId, out var sched);
        time.TryGetValue(tenantId, out var timeStr);

        TimeSpan? runTime = null;
        if (!string.IsNullOrWhiteSpace(timeStr) && TimeSpan.TryParse(timeStr, out var ts))
            runTime = ts;

        await _settings.UpsertAsync(orgId, sched ?? "Off", runTime, endpoints ?? Array.Empty<string>());

        TempData["Message"] = $"Saved schedule for {tenantId}.";
        return RedirectToAction("Index");
    }

}
