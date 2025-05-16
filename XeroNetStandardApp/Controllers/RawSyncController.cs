// RawSyncController.cs
// Replaces the previous file in XeroNetStandardApp.Controllers
// Ric Wheatley – May 2025

using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;
using System.Threading.Tasks;
using XeroNetStandardApp.Services;

namespace XeroNetStandardApp.Controllers
{
    // All routes here start with /raw-sync
    [Route("raw-sync")]
    public class RawSyncController : Controller
    {
        private readonly IPollingService _pollingService;
        private readonly ILogger<RawSyncController> _logger;

        public RawSyncController(
            IPollingService pollingService,
            ILogger<RawSyncController> logger)
        {
            _pollingService = pollingService;
            _logger = logger;
        }

        // ────────────────────────────────────────────────────────────────
        // POST or GET  /raw-sync/run    (triggered from “Run” buttons)
        // ────────────────────────────────────────────────────────────────
        [HttpPost("run"), HttpGet("run")]
        public async Task<IActionResult> Run(
            string tenantId,
            List<string>? selectedEndpoints)
        {
            if (selectedEndpoints is null || selectedEndpoints.Count == 0)
            {
                TempData["Message"] = "No endpoints selected.";
                return RedirectToAction("Index", "IdentityInfo");
            }

            foreach (var ep in selectedEndpoints)
                await _pollingService.RunEndpointAsync(tenantId, ep);

            TempData["Message"] = "Polling triggered.";
            return RedirectToAction("Index", "IdentityInfo");
        }

        // ────────────────────────────────────────────────────────────────
        // GET /raw-sync   → bounce to control panel instead of 404
        // ────────────────────────────────────────────────────────────────
        [HttpGet("")]
        public IActionResult Index() => RedirectToAction("Index", "IdentityInfo");
    }
}
