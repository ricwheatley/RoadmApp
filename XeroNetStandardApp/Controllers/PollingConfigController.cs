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

        var model = new PollingConfigViewModel
        {
            Tenants = token.Tenants.Select(t => new OrgTenant
            {
                TenantId = t.TenantId.ToString(),
                OrgName = t.TenantName
            }).ToList(),
            AccountingEndpoints = new()
            {
                new EndpointOption { Key = "contacts", DisplayName = "Contacts" },
                new EndpointOption { Key = "invoices", DisplayName = "Invoices" },
                new EndpointOption { Key = "payments", DisplayName = "Payments" }
            },
            AssetsEndpoints = new()
            {
                new EndpointOption { Key = "assets", DisplayName = "Assets" },
                new EndpointOption { Key = "assettypes", DisplayName = "Asset Types" }
            }
        };

        return View(model);
    }
}
