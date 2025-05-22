using System.Threading.Tasks;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Http;

namespace XeroNetStandardApp.Tests.Helpers
{
    /// <summary>
    /// Dummy anti-forgery service that always succeeds.
    /// Injected by TestApiFactory during integration tests.
    /// </summary>
    internal sealed class TestAntiforgery : IAntiforgery
    {
        private static readonly AntiforgeryTokenSet Dummy =
            // .NET 8 constructor: AntiforgeryTokenSet(string requestToken,
            //                                         string cookieToken,
            //                                         string formFieldName,
            //                                         string headerName)
            new("__dummy__", "__dummy__", "__RequestVerificationToken", "RequestVerificationToken");

        public AntiforgeryTokenSet GetAndStoreTokens(HttpContext context) => Dummy;

        public AntiforgeryTokenSet GetTokens(HttpContext context) => Dummy;

        /* ----- Interface methods introduced/changed in .NET 8 ----- */

        public Task<bool> IsRequestValidAsync(HttpContext context) =>
            Task.FromResult(true);

        public Task<bool> IsRequestValidAsync(HttpContext context,
                                              AntiforgeryTokenSet? tokens) =>
            Task.FromResult(true);

        public Task<bool> IsRequestValidAsync(HttpContext context,
                                              AntiforgeryTokenSet? tokens,
                                              bool ignoreAdditionalContent) =>
            Task.FromResult(true);

        public Task ValidateRequestAsync(HttpContext context) =>
            Task.CompletedTask;

        public void SetCookieTokenAndHeader(HttpContext context)
        {
            // No-op: tests do not care about the cookie/header.
        }
    }
}
