using System.Linq;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using XeroNetStandardApp.Services;

namespace XeroNetStandardApp.Tests.Helpers
{
    public class TestApiFactory : WebApplicationFactory<Program>
    {
        public StubPollingService StubPolling { get; } = new();

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.ConfigureServices(services =>
            {
                // Replace IPollingService with stub
                var pollingDescriptor = services.SingleOrDefault(d => d.ServiceType == typeof(IPollingService));
                if (pollingDescriptor != null)
                {
                    services.Remove(pollingDescriptor);
                }
                services.AddSingleton<IPollingService>(StubPolling);

                // Replace TokenService with fake
                var tokenDescriptor = services.SingleOrDefault(d => d.ServiceType == typeof(TokenService));
                if (tokenDescriptor != null)
                {
                    services.Remove(tokenDescriptor);
                }
                services.AddSingleton<TokenService, FakeTokenService>();
            });
        }
    }
}
