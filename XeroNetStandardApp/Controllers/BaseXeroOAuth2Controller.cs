// BaseXeroOAuth2Controller.cs
// Replaces the previous file in XeroNetStandardApp.Controllers
// Ric Wheatley – May 2025

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Xero.NetStandard.OAuth2.Client;
using Xero.NetStandard.OAuth2.Config;
using Xero.NetStandard.OAuth2.Token;
using XeroNetStandardApp.Services;


namespace XeroNetStandardApp.Controllers
{
    /// <summary>
    /// Base controller that manages validation of Xero tokens and tenant IDs.
    /// Ensures access tokens are silently refreshed and persisted before they expire.
    /// </summary>
    public abstract class BaseXeroOAuth2Controller : Controller
    {
        private static readonly SemaphoreSlim _tokenRefreshLock = new(1, 1);   // serialises refresh attempts
        private static readonly TimeSpan _expiryBuffer = TimeSpan.FromMinutes(2); // refresh 2 min early

        protected readonly IOptions<XeroConfiguration> _xeroConfig;
        protected readonly TokenService _tokenService;
        protected readonly ILogger<BaseXeroOAuth2Controller> _logger;

        #region ctor

        protected BaseXeroOAuth2Controller(
            IOptions<XeroConfiguration> xeroConfig,
            TokenService tokenService,
            ILogger<BaseXeroOAuth2Controller> logger)
        {
            _xeroConfig = xeroConfig ?? throw new ArgumentNullException(nameof(xeroConfig));
            _tokenService = tokenService ?? throw new ArgumentNullException(nameof(tokenService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        #endregion

        #region Convenience helpers

        /// <summary>
        /// The caller’s tenant ID, or null if the user has never connected.
        /// Redirects to /Authorization when absent.
        /// </summary>
        protected string? TenantId
        {
            get
            {
                var token = _tokenService.RetrieveToken();
                var id = token?.Tenants?.Count > 0
                         ? token.Tenants[0].TenantId.ToString()
                         : null;

                if (string.IsNullOrWhiteSpace(id))
                {
                    Response.Redirect("/Authorization");
                    return null;
                }
                return id;
            }
        }

        /// <summary>
        /// Synchronously exposes a token for legacy code paths.
        /// </summary>
        [Obsolete("Prefer awaiting GetValidXeroTokenAsync instead.")]
        protected XeroOAuth2Token? XeroToken => GetValidXeroTokenAsync().GetAwaiter().GetResult();

        #endregion

        #region Core – obtain or refresh a token

        /// <summary>
        /// Returns a guaranteed-valid Xero token, refreshing (and persisting) it if necessary.
        /// If the refresh token itself is invalid or revoked, clears the local copy and returns null,
        /// so the caller can reroute the user through the OAuth connect flow.
        /// </summary>
        protected async Task<XeroOAuth2Token?> GetValidXeroTokenAsync()
        {
            // 1. Pull the last-saved token
            var token = _tokenService.RetrieveToken();
            if (token == null)
            {
                _logger.LogDebug("No stored Xero token – user must authorise first.");
                return null;
            }

            // 2. Check whether we’re safely within the expiry buffer
            var expiryCutoffUtc = token.ExpiresAtUtc - _expiryBuffer;
            if (DateTime.UtcNow < expiryCutoffUtc)
            {
                return token; // still fresh enough
            }

            // 3. Serialise refresh attempts – avoids multiple concurrent refreshes in multi-thread scenarios
            await _tokenRefreshLock.WaitAsync(HttpContext.RequestAborted);
            try
            {
                // Double-check in case another request refreshed while we waited
                token = _tokenService.RetrieveToken();
                if (token == null)
                {
                    _logger.LogWarning("Token disappeared during refresh attempt.");
                    return null;
                }
                expiryCutoffUtc = token.ExpiresAtUtc - _expiryBuffer;
                if (DateTime.UtcNow < expiryCutoffUtc)
                {
                    return token;
                }

                // 4. Refresh
                var client = new XeroClient(_xeroConfig.Value); // uses default HttpClient internally
                var newToken = (XeroOAuth2Token)await client.RefreshAccessTokenAsync(token);

                // Xero's refresh endpoint does not return tenant or ID token
                // information, so preserve these from the previous token.
                newToken.Tenants = token.Tenants;
                newToken.IdToken = token.IdToken;

                _tokenService.StoreToken(newToken);
                _logger.LogInformation("Xero token refreshed successfully (expires {Expiry}).", newToken.ExpiresAtUtc);

                return newToken;
            }
            catch (ApiException ex) when (ex.Message.Contains("invalid_grant", StringComparison.OrdinalIgnoreCase))
            {
                // The refresh token is dead – wipe local token and force re-auth
                _tokenService.DestroyToken();
                _logger.LogWarning("Refresh failed with invalid_grant – token destroyed, user must reconnect.");
                return null;
            }
            finally
            {
                _tokenRefreshLock.Release();
            }
        }

        #endregion
    }
}
