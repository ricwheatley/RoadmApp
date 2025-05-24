// XeroNetStandardApp.Tests/Helpers/TestApiFactory.cs
using System.Linq;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using XeroNetStandardApp.Services;
using XeroNetStandardApp.Tests.Helpers;
using XeroNetStandardApp.Models;


namespace XeroNetStandardApp.Tests.Helpers
{
    /// <summary>
    /// Creates an in-memory test host with stubbed services,
    /// including a fake ICallLogService so the test suite never
    /// asks for a Postgres connection string.
    /// </summary>
    public sealed class TestApiFactory : WebApplicationFactory<Program>
    {
        public StubPollingService StubPolling { get; } = new();

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.ConfigureServices(services =>
            {
                /* ---------- Replace IPollingService with stub ---------- */
                var pollingDescriptor = services.SingleOrDefault(
                    d => d.ServiceType == typeof(IPollingService));

                if (pollingDescriptor is not null)
                {
                    services.Remove(pollingDescriptor);
                }

                services.AddSingleton<IPollingService>(StubPolling);

                /* ---------- Replace TokenService with fake ---------- */
                var tokenDescriptor = services.SingleOrDefault(
                    d => d.ServiceType == typeof(TokenService));

                if (tokenDescriptor is not null)
                {
                    services.Remove(tokenDescriptor);
                }

                services.AddSingleton<TokenService, FakeTokenService>();

                /* ---------- Replace ICallLogService with fake ---------- */
                var callLogDescriptor = services.SingleOrDefault(
                    d => d.ServiceType == typeof(ICallLogService));

                if (callLogDescriptor is not null)
                {
                    services.Remove(callLogDescriptor);
                }

                services.AddSingleton<ICallLogService, FakeCallLogService>();

                /* ---------- Stub IAntiforgery so ValidateAntiForgeryToken passes ---------- */
                var antiDescriptor = services.SingleOrDefault(
                    d => d.ServiceType == typeof(IAntiforgery));

                if (antiDescriptor is not null)
                {
                    services.Remove(antiDescriptor);
                }

                services.AddSingleton<IAntiforgery, TestAntiforgery>();
            });
        }
    }
}
