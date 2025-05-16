// AuthorizationController.cs
// Ric Wheatley – May 2025 – final, compiles with SDK ≥5.x

using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Threading.Tasks;
using Xero.NetStandard.OAuth2.Client;
using Xero.NetStandard.OAuth2.Config;
using Xero.NetStandard.OAuth2.Token;
using XeroNetStandardApp.Services;

namespace XeroNetStandardApp.Controllers
{
    public class AuthorizationController : BaseXeroOAuth2Controller
    {
        private readonly XeroClient _client;
        private readonly ILogger<AuthorizationController> _log;
        private readonly XeroConfiguration _cfg;

        public AuthorizationController(
            IOptions<XeroConfiguration> xeroConfig,
            TokenService tokenService,
            ILogger<BaseXeroOAuth2Controller> baseLogger,
            ILogger<AuthorizationController> log)
            : base(xeroConfig, tokenService, baseLogger)
        {
            _cfg = xeroConfig.Value;
            _client = new XeroClient(_cfg);               // <- single-arg ctor
            _log = log;
        }

        // GET /Authorization  → Xero login / consent
        public IActionResult Index()
        {
            return Redirect(_client.BuildLoginUri());
        }

        // GET /Authorization/Callback ← Xero redirects back here
        public async Task<IActionResult> Callback(string code, string state)
        {
            // Simple CSRF check: config.State must match return state
            if (state != _cfg.State)
                return BadRequest("State mismatch.");

            var token = (XeroOAuth2Token)await _client.RequestAccessTokenAsync(code);

            _tokenService.StoreToken(token);
            _log.LogInformation("Xero token stored (expires {Expiry}).", token.ExpiresAtUtc);

            return RedirectToAction("Index", "OrganisationInfo");
        }

        // GET /Authorization/Disconnect
        public async Task<IActionResult> Disconnect()
        {
            var token = await GetValidXeroTokenAsync();
            if (token == null || token.Tenants?.Count == 0)
                return BadRequest("No connected Xero organisation to disconnect.");

            await _client.DeleteConnectionAsync(token, token.Tenants[0]);
            _tokenService.DestroyToken();
            _log.LogInformation("Xero connection revoked and local token destroyed.");

            return RedirectToAction("Index", "Home");
        }
    }
}
