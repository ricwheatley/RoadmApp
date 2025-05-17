using System.Diagnostics;
using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using XeroNetStandardApp.Services;
using XeroNetStandardApp.Models;

namespace XeroNetStandardApp.Controllers
{
    public class HomeController : Controller
    {
        private readonly TokenService _tokenService;

        public HomeController(TokenService tokenService)
        {
            _tokenService = tokenService;
        }

        public IActionResult Index()
        {
            var token = _tokenService.RetrieveToken();
            var model = new HomeIndexViewModel
            {
                IsConnected = token != null
            };

            if (token?.Tenants != null)
            {
                foreach (var t in token.Tenants)
                {
                    model.Tenants.Add(new OrgTenant
                    {
                        TenantId = t.TenantId.ToString(),
                        OrgName = t.TenantName
                    });
                }
            }

            return View(model);
        }

        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
