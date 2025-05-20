using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;
using XeroNetStandardApp.Tests.Helpers;

namespace XeroNetStandardApp.Tests
{
    public class IdentityInfoTests : IClassFixture<TestApiFactory>
    {
        private readonly HttpClient _client;
        private readonly TestApiFactory _factory;

        public IdentityInfoTests(TestApiFactory factory)
        {
            _factory = factory;
            _client = factory.CreateClient(new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false
            });
        }

        [Fact]
        public async Task BulkTrigger_TriggersEndpointsAndRedirects()
        {
            var content = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string,string>("tenantId", "123"),
                new KeyValuePair<string,string>("selected[123][]", "accounts"),
                new KeyValuePair<string,string>("selected[123][]", "invoices")
            });

            var response = await _client.PostAsync("/IdentityInfo/BulkTrigger", content);
            var response = await _client.PostAsync("/IdentityInfo/BulkTrigger", content);

            if (response.StatusCode != HttpStatusCode.Redirect)
            {
                // Get the full response content
                var body = await response.Content.ReadAsStringAsync();
                // Throw with details, which will show up in CI logs
                throw new Exception(
                    $"Failed with status: {response.StatusCode}\nResponse body:\n{body}"
                );
            }
            
            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
            Assert.Contains(_factory.StubPolling.Calls, c => c.Tenant == "123" && c.Endpoint == "accounts");
            Assert.Contains(_factory.StubPolling.Calls, c => c.Tenant == "123" && c.Endpoint == "invoices");
        }
    }
}
