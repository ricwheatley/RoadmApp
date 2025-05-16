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
        public OrganisationInfo(
            IOptions<XeroConfiguration> xeroConfig,
            TokenService tokenService,
            ILogger<OrganisationInfo> logger)
            : base(xeroConfig, tokenService, logger)   // your base class handles the logger
        {
        }

        // GET: /Organisation/
        [HttpGet]
        public async Task<IActionResult> Index()
        {
            // Guard clauses remove the CS8602 / CS8604 warnings
            if (XeroToken?.AccessToken is null || string.IsNullOrWhiteSpace(XeroToken.AccessToken))
            {
                // Use whatever shared error view or handler you already have
                return View("Error");
            }

            if (string.IsNullOrWhiteSpace(TenantId))
            {
                return View("Error");
            }

            // After the checks above, the compiler knows these cannot be null
            var response =
                await Api.GetOrganisationsAsync(XeroToken.AccessToken, TenantId);

            if (response?._Organisations is null || response._Organisations.Count == 0)
            {
                return View("NotFound");  // or your preferred “no data” view
            }

            return View(response._Organisations[0]);
        }
    }
}
