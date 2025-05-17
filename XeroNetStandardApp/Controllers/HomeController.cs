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
            var isConnected = _tokenService.RetrieveToken() != null;
            ViewBag.IsConnected = isConnected;
            return View(isConnected);
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
