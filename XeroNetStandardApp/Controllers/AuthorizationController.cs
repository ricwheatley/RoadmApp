// AuthorizationController.cs
// Ric Wheatley – May 2025 – logs full authorisation URL and validates dynamic state

using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Xero.NetStandard.OAuth2.Client;
using Xero.NetStandard.OAuth2.Config;
using Xero.NetStandard.OAuth2.Token;
using XeroNetStandardApp.Services;

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

        // ─────────────────────────────────────────────────────────────────────────────
        // GET  /Authorization           → build consent URL and redirect to Xero
        // ─────────────────────────────────────────────────────────────────────────────
        [HttpGet]
        [Route("Authorization")]
        public IActionResult Index()
        {
            // 1️⃣  Generate per-request state and stash it in session
            var state = Guid.NewGuid().ToString("N");
            HttpContext.Session.SetString(SessionKeyState, state);

            // 2️⃣  Build login URI that includes scope, client_id, redirect_uri, state
            var loginUri = _client.BuildLoginUri(state);

            // 3️⃣  Log the full URL so you can see the exact scopes
            Console.WriteLine($"Authorisation URL: {loginUri}");
            _log.LogInformation("Authorisation URL: {LoginUri}", loginUri);

            return Redirect(loginUri);
        }

        // ─────────────────────────────────────────────────────────────────────────────
        // GET  /Authorization/Callback  ← Xero redirects back here
        // ─────────────────────────────────────────────────────────────────────────────
        [HttpGet]
        [Route("Authorization/Callback")]
        public async Task<IActionResult> Callback([FromQuery] string code,
                                                  [FromQuery] string state)
        {
            var expectedState = HttpContext.Session.GetString(SessionKeyState);
            if (state != expectedState)
            {
                _log.LogWarning("State mismatch – possible CSRF attack");
                return BadRequest("Invalid state");
            }

            var token = (XeroOAuth2Token)await _client.RequestAccessTokenAsync(code);
            _tokenService.StoreToken(token);

            _log.LogInformation("Xero token stored (expires {Expiry}).",
                                token.ExpiresAtUtc);

            return RedirectToAction("Index", "Home");
        }

        // ─────────────────────────────────────────────────────────────────────────────
        // GET  /Authorization/Disconnect?tenantId=<guid>
        // ─────────────────────────────────────────────────────────────────────────────
        [HttpGet]
        [Route("Authorization/Disconnect")]
        public async Task<IActionResult> Disconnect([FromQuery] string tenantId)
        {
            var token = await GetValidXeroTokenAsync();
            if (token == null || token.Tenants?.Count == 0)
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
