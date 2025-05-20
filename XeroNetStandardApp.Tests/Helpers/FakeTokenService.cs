using XeroNetStandardApp.Services;
using Xero.NetStandard.OAuth2.Token;
using System;

namespace XeroNetStandardApp.Tests.Helpers
{
    public class FakeTokenService : TokenService
    {
        public FakeTokenService() : base(null, null) { }

        public override XeroOAuth2Token RetrieveToken()
        {
            // Return a dummy token with the shape your app expects
            return new XeroOAuth2Token
            {
                AccessToken = "fake-access-token",
                ExpiresAtUtc = DateTime.UtcNow.AddHours(1),
                RefreshToken = "fake-refresh-token"
                // Add any other required fields if your code expects them
            };
        }
    }
}
