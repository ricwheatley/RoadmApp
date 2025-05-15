using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using XeroNetStandardApp.Services;
using Xero.NetStandard.OAuth2.Models;
using Microsoft.AspNetCore.Http;
using System.Threading.Tasks;
using Xero.NetStandard.OAuth2.Token;
using XeroNetStandardApp.Helpers;
using XeroNetStandardApp.IO;
using System.Linq;

namespace XeroNetStandardApp.Controllers;

[Route("raw-sync")]
public class RawSyncController : Controller
{
    private readonly IXeroRawIngestService _svc;
    private readonly ILogger<RawSyncController> _log;
    private readonly TokenService _tokenService;

    public RawSyncController(
        IXeroRawIngestService svc,
        ILogger<RawSyncController> log,
        TokenService tokenService)
    {
        _svc = svc;
        _log = log;
        _tokenService = tokenService;
    }

    [HttpGet("run")]
    public async Task<IActionResult> Run()
    {
        // Retrieve and decrypt the token using TokenService
        var token = _tokenService.RetrieveToken();

        if (token == null || string.IsNullOrEmpty(token.AccessToken))
            return Content("No saved Xero token. Click Connect to Xero first.");

        var tenantId = token.Tenants?.FirstOrDefault()?.TenantId.ToString();

        if (string.IsNullOrEmpty(tenantId))
            return Content("No tenant ID found in the token.");

        var rows = await _svc.RunOnceAsync(token.AccessToken, tenantId);
        _log.LogInformation("Total rows inserted: {Rows}", rows);

        return Content($"Inserted {rows} rows into Lucent.raw.*");
    }
}
