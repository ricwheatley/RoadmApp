using Microsoft.AspNetCore.Mvc;
using System;
using System.Threading.Tasks;
using XeroNetStandardApp.Services;
using System.Linq;
using System.Net;

[ApiController]
[Route("api/poll")]
public sealed class PollController : ControllerBase
{
    private readonly IPollingService _polling;
    private readonly IXeroRawIngestService _raw;

    public PollController(IPollingService polling,
                          IXeroRawIngestService raw)
    {
        _polling = polling;
        _raw = raw;
    }

    /// <summary>
    /// Run a single endpoint (manual trigger) and persist to api_call_log.
    /// Returns the same rich payload as /api/ingest/run/{endpointKey}.
    /// </summary>
    [HttpPost("run/{endpointKey}")]
    public async Task<IActionResult> RunOne(string endpointKey,
                                            [FromQuery] string tenantId)
    {
        var callTime = DateTimeOffset.UtcNow;

        // this updates utils.api_call_log via PollingService
        await _polling.RunEndpointAsync(tenantId, endpointKey, callTime);

        // get the detailed reports back (for the modal)
        var reports = await _raw.RunOnceAsync(tenantId, endpointKey);

        return Ok(new
        {
            totalInserted = reports.Sum(r => r.RowsInserted),
            reports,
            errors = reports.Where(r =>
                       r.ResponseCode != HttpStatusCode.OK &&
                       r.ResponseCode != HttpStatusCode.NotModified)
        });
    }
}