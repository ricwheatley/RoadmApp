using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Xero.NetStandard.OAuth2.Token;

namespace XeroNetStandardApp.Services
{
    public class PollingService : IPollingService
    {
        private readonly TokenService _tokenService;
        private readonly IXeroRawIngestService _ingestSvc;
        private readonly IXeroAssetsIngestService _assetsSvc;
        private readonly ILogger<PollingService> _log;

        public PollingService(
            TokenService tokenService,
            IXeroRawIngestService ingestSvc,
            IXeroAssetsIngestService assetsSvc,
            ILogger<PollingService> log)
        {
            _tokenService = tokenService;
            _ingestSvc = ingestSvc;
            _assetsSvc = assetsSvc;
            _log = log;
        }

        public async Task RunEndpointAsync(string tenantId, string endpointKey)
        {
            XeroOAuth2Token? tok = _tokenService.RetrieveToken();
            if (tok == null || string.IsNullOrEmpty(tok.AccessToken))
                throw new InvalidOperationException("No valid Xero token on file.");

            if (endpointKey.Equals("assets", StringComparison.OrdinalIgnoreCase))
            {
                await _assetsSvc.RunOnceAsync(tenantId, endpointKey);
            }
            else
            {
                await _ingestSvc.RunOnceAsync(tenantId, endpointKey);
            }

            _log.LogInformation("Polled {Endpoint} for tenant {Tenant}", endpointKey, tenantId);
        }
    }
}
