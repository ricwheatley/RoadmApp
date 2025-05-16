// OrganisationInfo.cs
// Ric Wheatley – May 2025

using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Threading.Tasks;
using Xero.NetStandard.OAuth2.Api;
using Xero.NetStandard.OAuth2.Config;
using XeroNetStandardApp.Services;

namespace XeroNetStandardApp.Controllers
{
    public class OrganisationInfo : ApiAccessorController<AccountingApi>
    {
        private readonly ILogger<OrganisationInfo> _log;

        public OrganisationInfo(
            IOptions<XeroConfiguration> xeroConfig,
            TokenService tokenService,
            ILogger<BaseXeroOAuth2Controller> baseLogger,
            ILogger<OrganisationInfo> log)
            : base(xeroConfig, tokenService, baseLogger)
        {
            _log = log;
        }

        // GET /OrganisationInfo
        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var token = await GetValidXeroTokenAsync();
            if (token == null) return RedirectToAction("Index", "Authorization");

            var tenantId = TenantId;
            if (tenantId == null) return RedirectToAction("Index", "Authorization");

            var response = await Api.GetOrganisationsAsync(token.AccessToken, tenantId);

            if (response?._Organisations is null || response._Organisations.Count == 0)
                return View("NotFound");

            return View(response._Organisations[0]);
        }
    }
}
