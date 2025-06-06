using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using XeroNetStandardApp.Models;
using XeroNetStandardApp.Services;

namespace XeroNetStandardApp.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class IngestController : ControllerBase
    {
        private readonly IXeroRawIngestService _ingest;
        private readonly ILogger<IngestController> _log;

        public IngestController(IXeroRawIngestService ingest,
                                ILogger<IngestController> log)
        {
            _ingest = ingest;
            _log = log;
        }

        /// <summary>
        /// Ingest all configured endpoints for a tenant.
        /// </summary>
        [HttpPost("run")]
        public async Task<IActionResult> Run([FromQuery] string tenantId)
        {
            if (string.IsNullOrWhiteSpace(tenantId))
                return BadRequest("tenantId is required.");

            IReadOnlyList<EndpointIngestReport> reports =
                await _ingest.RunOnceAsync(tenantId);

            var payload = BuildPayload(reports);

            _log.LogInformation(
                "Ingest completed for tenant {Tenant}: {Inserted} rows, {Errors} errors",
                tenantId,
                payload.TotalInserted,
                payload.Errors.Count);

            return Ok(payload);
        }

        /// <summary>
        /// Ingest a single endpoint for a tenant (e.g. Contacts, Invoices).
        /// </summary>
        [HttpPost("run/{endpointKey}")]
        public async Task<IActionResult> RunSingle(string endpointKey,
                                                   [FromQuery] string tenantId)
        {
            if (string.IsNullOrWhiteSpace(tenantId))
                return BadRequest("tenantId is required.");

            IReadOnlyList<EndpointIngestReport> reports =
                await _ingest.RunOnceAsync(tenantId, endpointKey);

            var payload = BuildPayload(reports);

            _log.LogInformation(
                "Ingest completed for tenant {Tenant}: {Inserted} rows, {Errors} errors",
                tenantId,
                payload.TotalInserted,
                payload.Errors.Count);

            return Ok(payload);
        }

        // ──────────────────────────────────────────────────────────
        //  PRIVATE HELPERS
        // ──────────────────────────────────────────────────────────
        private static IngestApiPayload BuildPayload(IReadOnlyList<EndpointIngestReport> reports)
        {
            int totalInserted = reports.Sum(r => r.RowsInserted);

            var errors = reports
                .Where(r => r.ResponseCode != HttpStatusCode.OK &&
                            r.ResponseCode != HttpStatusCode.NotModified)
                .Select(r => new ErrorSummary
                {
                    EndpointName = r.EndpointName,
                    Status = r.Status,
                    Code = (int)r.ResponseCode,
                    ErrorDetail = r.ErrorDetail
                })
                .ToList();

            return new IngestApiPayload
            {
                TotalInserted = totalInserted,
                Reports = reports,
                Errors = errors
            };
        }
    }
}
