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
        private readonly ILogger<RawSyncController> _log;

        public RawSyncController(IPollingService pollingService,
                                 ILogger<RawSyncController> log)
        {
            _pollingService = pollingService;
            _log = log;
        }

        // ────────────────────────────────────────────────────────────────
        // POST  /raw-sync/run    (called from the grid “Run” buttons)
        // ────────────────────────────────────────────────────────────────
        [HttpPost("run"), HttpGet("run")]
        public async Task<IActionResult> Run(string tenantId,
                                     List<string> selectedEndpoints)
        {
            if (selectedEndpoints == null || selectedEndpoints.Count == 0)
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
        // OPTIONAL: If someone types /raw-sync in the browser, just send
        // them back to the control panel rather than a 404.
        // ────────────────────────────────────────────────────────────────
        [HttpGet("")]
        public IActionResult Index()
        {
            return RedirectToAction("Index", "IdentityInfo");
        }
    }
}
