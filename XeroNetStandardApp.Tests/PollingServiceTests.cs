using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using System;
using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;
using Xero.NetStandard.OAuth2.Token;
using XeroNetStandardApp.Models;
using XeroNetStandardApp.Services;
using Xunit;

namespace XeroNetStandardApp.Tests
{
    public class PollingServiceTests
    {
        private sealed class StubRaw : IXeroRawIngestService
        {
            // capture the most-recent call so the test can Assert on it
            public (string TenantId, string? EndpointKey) LastCall { get; private set; }

            public Task<IReadOnlyList<EndpointIngestReport>> RunOnceAsync(string tenantId)
            {
                LastCall = (tenantId, null);     // null means “all endpoints”
                return Task.FromResult<IReadOnlyList<EndpointIngestReport>>(
                    Array.Empty<EndpointIngestReport>());
            }

            public Task<IReadOnlyList<EndpointIngestReport>> RunOnceAsync(string tenantId,
                                                                          string endpointKey)
            {
                LastCall = (tenantId, endpointKey);

                // Provide a minimal, successful report so the code under test
                // can behave as if 5 rows were inserted.
                var reports = new[]
                {
            new EndpointIngestReport(
                endpointKey,
                Status: null,
                RowsInserted: 5,
                WasUpToDate: false,
                ResponseCode: HttpStatusCode.OK,
                ErrorDetail: null)
        };

                return Task.FromResult<IReadOnlyList<EndpointIngestReport>>(reports);
            }
        }

        [Fact]
        public async Task AssetsEndpoint_UsesIngestService()
        {
            var raw = new StubRaw();

            // Ephemeral provider stores keys only in memory – perfect for unit tests
            var dataProtectionProvider = new EphemeralDataProtectionProvider();
            var tokenService = new TokenService(dataProtectionProvider);

            tokenService.StoreToken(new XeroOAuth2Token
            {
                AccessToken = "t",
                ExpiresAtUtc = DateTime.UtcNow.AddHours(1)
            });

            var cfg = new Microsoft.Extensions.Configuration.ConfigurationBuilder()
                .AddInMemoryCollection(new[]
                {
                    new System.Collections.Generic.KeyValuePair<string, string?>(
                        "ConnectionStrings:Postgres",
                        "Host=localhost")
                })
                .Build();

            var svc = new PollingService(tokenService, raw, NullLogger<PollingService>.Instance, cfg);
            var rows = await svc.RunEndpointAsync("123", "assets", DateTimeOffset.UtcNow);

            Assert.Equal(("123", "assets"), raw.LastCall);
            Assert.Equal(0, rows);
        }
    }
}
