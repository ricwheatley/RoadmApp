using System;
using System.Diagnostics;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using XeroNetStandardApp.Models;
using XeroNetStandardApp.Services;
using XeroNetStandardApp.Helpers;

namespace XeroNetStandardApp.Controllers
{
    public class HomeController : Controller
    {
        private readonly TokenService _tokenService;
        private readonly ICallLogService _callLogs;
        private readonly ILogger<HomeController> _log;

        public HomeController(
            TokenService tokenService,
            ICallLogService callLogs,
            ILogger<HomeController> log)
        {
            _tokenService = tokenService;
            _callLogs = callLogs;
            _log = log;
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var token = _tokenService.RetrieveToken();
            var model = new HomeIndexViewModel { IsConnected = token != null };

            // -------------------------------------------------------
            // 1. Build the basic tenant list from the Xero token
            // -------------------------------------------------------
            if (token?.Tenants != null)
            {
                foreach (var t in token.Tenants)
                {
                    model.Tenants.Add(new OrgTenant
                    {
                        TenantId = t.TenantId.ToString(),
                        OrgName = t.TenantName,
                        Scopes = token.GetScopes()
                    });
                }
            }

            // -------------------------------------------------------
            // 2. Pull latest call-stats from utils.api_call_log
            // -------------------------------------------------------
            if (model.Tenants.Count != 0)
            {
                var tenantGuids = model.Tenants
                                       .Select(t => Guid.Parse(t.TenantId!))
                                       .ToList();

                var latest = await _callLogs.GetLatestStatsAsync(tenantGuids);
                // latest : IDictionary<Guid, CallStats>

                foreach (var t in model.Tenants)
                {
                    if (Guid.TryParse(t.TenantId, out var id) &&
                        latest.TryGetValue(id, out var s))
                    {
                        t.LastCallUtc = s.LastCallUtc;
                        t.LastRowsInserted = s.RowsInserted;
                    }
                }
            }

            return View(model);
        }

        public IActionResult Privacy() => View();

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
            => View(new ErrorViewModel
            {
                RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier
            });
    }
}
