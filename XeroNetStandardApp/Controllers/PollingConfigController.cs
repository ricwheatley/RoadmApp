using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using XeroNetStandardApp.Models;
using XeroNetStandardApp.Services;

namespace XeroNetStandardApp.Controllers;

public class PollingConfigController : Controller
{
    private readonly TokenService _tokenService;

    public PollingConfigController(TokenService tokenService)
    {
        _tokenService = tokenService;
    }

    // GET /PollingConfig
    public IActionResult Index()
    {
        var token = _tokenService.RetrieveToken();
        if (token == null) return RedirectToAction("Index", "Authorization");

        var endpoints = new List<EndpointOption>
        {
            new() { Key = "contacts",   DisplayName = "Accounting – Contacts" },
            new() { Key = "invoices",   DisplayName = "Accounting – Invoices" },
            new() { Key = "payments",   DisplayName = "Accounting – Payments" },
            new() { Key = "assets",     DisplayName = "Assets – Assets" },
            new() { Key = "assettypes", DisplayName = "Assets – Asset Types" }
        };

        var model = new PollingConfigViewModel
        {
            Tenants = token.Tenants.Select(t => new OrgTenant
            {
                TenantId = t.TenantId.ToString(),
                OrgName = t.TenantName
            }).ToList(),
            Endpoints = endpoints
        };

        return View(model);
    }

    // POST /PollingConfig/SaveSchedule
    [HttpPost]
    public IActionResult SaveSchedule(
        string tenantId,
        [FromForm] Dictionary<string, string[]> selected,
        [FromForm] Dictionary<string, string> freq,
        [FromForm] Dictionary<string, string> time)
    {
        // TODO: Persist the schedule to a real data store.
        // For now we simply acknowledge the request.
        TempData["Message"] = $"Saved schedule for {tenantId}.";
        return RedirectToAction("Index");
    }
}
