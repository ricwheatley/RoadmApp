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
                var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(IPollingService));
                if (descriptor != null)
                {
                    services.Remove(descriptor);
                }
                services.AddSingleton<IPollingService>(StubPolling);
            });
        }
    }
}
