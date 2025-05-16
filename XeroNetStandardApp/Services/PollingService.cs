using System.Threading.Tasks;
using Microsoft.Extensions.Logging;              // ← add
using XeroNetStandardApp.Services;               // IXeroRawIngestService
using Xero.NetStandard.OAuth2.Token;
using System;

namespace XeroNetStandardApp.Services
{
    public class PollingService : IPollingService
    {
        private readonly TokenService _tokenService;
        private readonly IXeroRawIngestService _ingestSvc;
        private readonly ILogger<PollingService> _log;            // ← add

        public PollingService(TokenService tokenService,
                              IXeroRawIngestService ingestSvc,
                              ILogger<PollingService> log)        // ← add
        {
            _tokenService = tokenService;
            _ingestSvc = ingestSvc;
            _log = log;                                   // ← store
        }

        public async Task RunEndpointAsync(string tenantId, string endpointKey)
        {
            // 1. get token
            XeroOAuth2Token? token = _tokenService.RetrieveToken();
            if (token == null || string.IsNullOrEmpty(token.AccessToken))
                throw new InvalidOperationException("No valid Xero token on file.");

            // 2. run ingest
            await _ingestSvc.RunOnceAsync(token.AccessToken, tenantId);

            // 3. log
            _log.LogInformation("Polled {Endpoint} for tenant {Tenant}", endpointKey, tenantId);
        }
    }
}
