// ApiAccessorController.cs
// Replaces the previous file in XeroNetStandardApp.Controllers
// Ric Wheatley – May 2025

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Xero.NetStandard.OAuth2.Client;
using Xero.NetStandard.OAuth2.Config;
using XeroNetStandardApp.Services;

namespace XeroNetStandardApp.Controllers
{
    /// <summary>
    /// Provides shared token management plus a ready-made <typeparamref name="T"/> API accessor.
    /// </summary>
    /// <typeparam name="T">A concrete Xero <c>IApiAccessor</c> (e.g. <c>AccountingApi</c>).</typeparam>
    public abstract class ApiAccessorController<T> : BaseXeroOAuth2Controller
        where T : IApiAccessor, new()
    {
        /// <summary>The Xero SDK API client that derived controllers will use.</summary>
        protected readonly T Api;

        protected ApiAccessorController(
            IOptions<XeroConfiguration> xeroConfig,
            TokenService tokenService,
            ILogger<BaseXeroOAuth2Controller> logger)
            : base(xeroConfig, tokenService, logger)
        {
            Api = new T();
        }
    }
}
