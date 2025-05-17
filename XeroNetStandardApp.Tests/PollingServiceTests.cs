using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Logging.Abstractions;
using Xero.NetStandard.OAuth2.Token;
using Xunit;
using XeroNetStandardApp.Services;

namespace XeroNetStandardApp.Tests
{
    public class PollingServiceTests
    {
        private class StubRaw : IXeroRawIngestService
        {
            public (string Tenant, string Endpoint)? LastCall { get; private set; }
            public Task<int> RunOnceAsync(string tenantId) => Task.FromResult(0);
            public Task<int> RunOnceAsync(string tenantId, string endpointKey)
            {
                LastCall = (tenantId, endpointKey);
                return Task.FromResult(0);
            }
        }

        private class StubAssets : IXeroAssetsIngestService
        {
            public (string Tenant, string Endpoint)? LastCall { get; private set; }
            public Task<int> RunOnceAsync(string tenantId) => Task.FromResult(0);
            public Task<int> RunOnceAsync(string tenantId, string endpointKey)
            {
                LastCall = (tenantId, endpointKey);
                return Task.FromResult(0);
            }
        }

        [Fact]
        public async Task AssetsEndpoint_UsesAssetsService()
        {
            var raw = new StubRaw();
            var assets = new StubAssets();

            // Ephemeral provider stores keys only in memory – perfect for unit tests
            var dataProtectionProvider = new EphemeralDataProtectionProvider();
            var tokenService = new TokenService(dataProtectionProvider);

            tokenService.StoreToken(new XeroOAuth2Token
            {
                AccessToken = "t",
                ExpiresAtUtc = DateTime.UtcNow.AddHours(1)
            });

            var svc = new PollingService(tokenService, raw, assets, NullLogger<PollingService>.Instance);
            await svc.RunEndpointAsync("123", "assets");

            Assert.Null(raw.LastCall);
            Assert.Equal(("123", "assets"), assets.LastCall);
        }
    }
}
