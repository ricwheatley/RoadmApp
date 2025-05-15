using System.Threading.Tasks;
using XeroNetStandardApp.Services;     // for IXeroRawIngestService
using Xero.NetStandard.OAuth2.Token;
using System;

public class PollingService : IPollingService
{
    private readonly TokenService _tokenService;
    private readonly IXeroRawIngestService _ingestSvc;

    public PollingService(TokenService tokenService,
                          IXeroRawIngestService ingestSvc)
    {
        _tokenService = tokenService;
        _ingestSvc = ingestSvc;
    }

    public async Task RunEndpointAsync(string tenantId, string endpointKey)
    {
        XeroOAuth2Token? token = _tokenService.RetrieveToken();
        if (token == null || string.IsNullOrEmpty(token.AccessToken))
            throw new InvalidOperationException("No valid Xero token on file.");

        // Your ingest service currently takes only two args (token & tenant).
        // Call the existing overload so the project compiles.
        await _ingestSvc.RunOnceAsync(token.AccessToken, tenantId);

        // Later, when you extend IXeroRawIngestService to accept endpointKey,
        // switch to that new overload here.
    }
}
