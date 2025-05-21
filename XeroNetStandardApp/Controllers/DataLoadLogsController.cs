using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using XeroNetStandardApp.Services;
using XeroNetStandardApp.Models;

namespace XeroNetStandardApp.Controllers
{
    public class DataLoadLogsController : Controller
    {
        private readonly ICallLogService _logs;
        private readonly ILogger<DataLoadLogsController> _logger;

        public DataLoadLogsController(ICallLogService logs, ILogger<DataLoadLogsController> logger)
        {
            _logs = logs;
            _logger = logger;
        }

        [HttpGet]
        public async Task<IActionResult> Index(string tenantId, string? orgName)
        {
            if (string.IsNullOrWhiteSpace(tenantId) || !Guid.TryParse(tenantId, out var orgGuid))
            {
                _logger.LogWarning("Invalid tenantId '{TenantId}' supplied to logs page", tenantId);
                return BadRequest("Invalid organisation id.");
            }

            var logs = await _logs.GetLogsAsync(orgGuid);
            var model = new ApiCallLogViewModel
            {
                TenantId = tenantId,
                OrgName = orgName ?? tenantId,
                Logs = logs.ToList()
            };
            return View(model);
        }
    }
}
