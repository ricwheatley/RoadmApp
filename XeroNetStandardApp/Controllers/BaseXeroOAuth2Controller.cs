using System;
using System.Threading.Tasks;
using Xero.NetStandard.OAuth2.Token;
using Microsoft.AspNetCore.Mvc;
using Xero.NetStandard.OAuth2.Client;
using Microsoft.Extensions.Options;
using Xero.NetStandard.OAuth2.Config;
using XeroNetStandardApp.IO;
using XeroNetStandardApp.Services;
using Microsoft.Extensions.Logging;

namespace XeroNetStandardApp.Controllers
{
    /// <summary>
    /// Base controller that manages validation of xero tokens and tenant ids
    /// </summary>
    public abstract class BaseXeroOAuth2Controller : Controller
    {
        protected readonly ITokenIO tokenIO;
        protected readonly IOptions<XeroConfiguration> xeroConfig;

        protected XeroOAuth2Token? XeroToken => GetXeroOAuth2TokenAsync().Result;
        protected string? TenantId
        {
            get
            {
                var id = tokenIO.GetTenantId();
                if (string.IsNullOrEmpty(id))
                {
                    // no token yet – redirect caller to Connect page
                    HttpContext.Response.Redirect("/Authorization");
                    return null;
                }
                return id;
            }
        }

        protected readonly TokenService _tokenService;
        protected readonly ILogger<BaseXeroOAuth2Controller> _logger;

        protected BaseXeroOAuth2Controller(IOptions<XeroConfiguration> xeroConfig, TokenService tokenService, ILogger<BaseXeroOAuth2Controller> logger)
        {
            this.xeroConfig = xeroConfig;
            _tokenService = tokenService;
            tokenIO = LocalStorageTokenIO.Instance; // can eventually delete this once all replaced
            _logger = logger; 
        }

        
        /// <summary>
        /// Retrieve a valid Xero OAuth2 token
        /// </summary>
        /// <returns>Returns a valid Xero OAuth2 token</returns>
        protected async Task<XeroOAuth2Token?> GetXeroOAuth2TokenAsync()
        {

            // 1.  Load the last-saved token (your TokenService, not tokenIO, if you’ve migrated)
            var xeroToken = _tokenService.RetrieveToken();
            if (xeroToken == null) return null;

            // 2.  Refresh only if the access token is past its expiry
            if (DateTime.UtcNow <= xeroToken.ExpiresAtUtc)
                return xeroToken;

            try
            {
                var client = new XeroClient(xeroConfig.Value);
                xeroToken = (XeroOAuth2Token)await client.RefreshAccessTokenAsync(xeroToken);
                _tokenService.StoreToken(xeroToken);
                _logger.LogInformation("Tokem refreshed. Token stored.");
                return xeroToken;
            }
            catch (ApiException apiEx) when (apiEx.Message.Contains("invalid_grant"))
            {
                // refresh-token is dead → wipe local copy and force re-auth
                _tokenService.DestroyToken();
                _logger.LogWarning("Refresh failed (invalid_grant). Token cleared.");
                return null;
            }
        }
    }
}
