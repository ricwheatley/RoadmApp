using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using XeroNetStandardApp.Services;
using Xero.NetStandard.OAuth2.Models;
using Microsoft.AspNetCore.Http;
using System.Threading.Tasks;
using Xero.NetStandard.OAuth2.Token;
using XeroNetStandardApp.Helpers;
using XeroNetStandardApp.IO;

namespace XeroNetStandardApp.Controllers;

[Route("raw-sync")]
public class RawSyncController : Controller
{
    private readonly IXeroRawIngestService _svc;
    private readonly ILogger<RawSyncController> _log;

    public RawSyncController(IXeroRawIngestService svc, ILogger<RawSyncController> log)
    {
        _svc = svc;
        _log = log;
    }

    [HttpGet("run")]
    public async Task<IActionResult> Run()
    {
        // pull token & tenant straight from the local file store
        var store = LocalStorageTokenIO.Instance;
        var token = store.GetToken();      // returns XeroOAuth2Token
        var tenantId = store.GetTenantId();   // returns string

        if (string.IsNullOrEmpty(token.AccessToken) || string.IsNullOrEmpty(tenantId))
            return Content("No saved Xero token or tenant ID. Click Connect to Xero first.");

        var rows = await _svc.RunOnceAsync(token.AccessToken, tenantId);
        _log.LogInformation("Total rows inserted: {Rows}", rows);

        return Content($"Inserted {rows} rows into Lucent.raw.*");
    }
}
