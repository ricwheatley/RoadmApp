// AuthorizationController.cs
// Ric Wheatley – June 2025
//
// Builds the consent URL, exchanges the code for a token,
// logs the full token (including authorised scopes) via DumpToConsole(),
// and stores it for later API calls.

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Linq;
using System.Threading.Tasks;
using Xero.NetStandard.OAuth2.Client;
using Xero.NetStandard.OAuth2.Config;
using Xero.NetStandard.OAuth2.Token;
using XeroNetStandardApp.Services;
using XeroNetStandardApp.Helpers;          // ← DumpToConsole lives here

namespace XeroNetStandardApp.Controllers
{
    public class AuthorizationController : BaseXeroOAuth2Controller
    {
        private const string SessionKeyState = "XeroAuthState";

        private readonly XeroClient _client;
        private readonly ILogger<AuthorizationController> _log;

        public AuthorizationController(
            IOptions<XeroConfiguration> xeroConfig,
            TokenService tokenService,
            ILogger<BaseXeroOAuth2Controller> baseLogger,
            ILogger<AuthorizationController> log)
            : base(xeroConfig, tokenService, baseLogger)
        {
            _client = new XeroClient(xeroConfig.Value);
            _log = log;
        }

        // ───────────────────────────────────────────────────────────────
        // GET /Authorization  → build consent URL and redirect to Xero
        // ───────────────────────────────────────────────────────────────
        [HttpGet]
        [Route("Authorization")]
        public IActionResult Index()
        {
            var state = Guid.NewGuid().ToString("N");
            HttpContext.Session.SetString(SessionKeyState, state);

            // Build the login URI; BuildLoginUri() already includes your
            // scope list from appsettings.json or XeroConfiguration.
            var loginUri = _client.BuildLoginUri(state);

            _log.LogInformation("Authorisation URL: {LoginUri}", loginUri);
            Console.WriteLine($"Authorisation URL: {loginUri}");

            return Redirect(loginUri);
        }

        // ───────────────────────────────────────────────────────────────
        // GET /Authorization/Callback  ← Xero redirects back here
        // ───────────────────────────────────────────────────────────────
        [HttpGet]
        [Route("Authorization/Callback")]
        public async Task<IActionResult> Callback(
            [FromQuery] string code,
            [FromQuery] string state)
        {
            // 1  Validate state for CSRF protection
            var expectedState = HttpContext.Session.GetString(SessionKeyState);
            if (state != expectedState)
            {
                _log.LogWarning("State mismatch – possible CSRF attack");
                return BadRequest("Invalid state value.");
            }

            // 2  Exchange code for token
            var token = (XeroOAuth2Token)await _client.RequestAccessTokenAsync(code);

            // 3  << NEW – dump the whole token to console + ILogger >>
            token.DumpToConsole("Xero OAuth2 token", txt => _log.LogInformation(txt));
            var scopes = token.GetScopes();
            Console.WriteLine("Authorised scopes: " + string.Join(", ", scopes));
            _log.LogInformation("Authorised scopes: {Scopes}", string.Join(", ", scopes));
            // 4  Persist for future API calls
            _tokenService.StoreToken(token);
            _log.LogInformation("Xero token stored (expires {Expiry}).", token.ExpiresAtUtc);

            return RedirectToAction("Index", "Home");
        }

        // ───────────────────────────────────────────────────────────────
        // GET /Authorization/Disconnect?tenantId=<guid>
        // ───────────────────────────────────────────────────────────────
        [HttpGet]
        [Route("Authorization/Disconnect")]
        public async Task<IActionResult> Disconnect([FromQuery] string tenantId)
        {
            var token = await GetValidXeroTokenAsync();
            if (token?.Tenants == null || token.Tenants.Count == 0)
                return BadRequest("No connected Xero organisation to disconnect.");

            var tenant = token.Tenants.FirstOrDefault(t =>
                          t.TenantId.ToString() == tenantId);
            if (tenant == null)
                return BadRequest("Invalid tenant id.");

            await _client.DeleteConnectionAsync(token, tenant);
            token.Tenants.Remove(tenant);
            _tokenService.StoreToken(token);

            _log.LogInformation("Xero connection revoked for {TenantId}.", tenantId);
            return RedirectToAction("Index", "Home");
        }
    }
}
