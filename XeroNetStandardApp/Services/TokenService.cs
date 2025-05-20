using Microsoft.AspNetCore.DataProtection;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.IO;
using Xero.NetStandard.OAuth2.Token;

namespace XeroNetStandardApp.Services
{
    public class TokenService
    {
        private readonly IDataProtector _protector;
        private readonly string _tokenFilePath = Path.Combine(Directory.GetCurrentDirectory(), "token.dat");

        public TokenService(IDataProtectionProvider provider)
        {
            _protector = provider.CreateProtector("XeroTokenProtection");
        }

        public void StoreToken(XeroOAuth2Token token)
        {
            var serializedToken = JsonConvert.SerializeObject(token);
            var encryptedToken = _protector.Protect(serializedToken);
            File.WriteAllText(_tokenFilePath, encryptedToken);
        }

        public virtual XeroOAuth2Token? RetrieveToken()
        {
            if (!File.Exists(_tokenFilePath))
                return null;

            var encryptedToken = File.ReadAllText(_tokenFilePath);
            var decryptedToken = _protector.Unprotect(encryptedToken);
            var token = JsonConvert.DeserializeObject<XeroOAuth2Token>(decryptedToken);
            var json = JObject.Parse(decryptedToken); // ← using Newtonsoft.Json.Linq
            //Console.WriteLine(json);
            return token;
        }

        public void DestroyToken()
        {
            if (File.Exists(_tokenFilePath))
            {
                File.Delete(_tokenFilePath);
            }
        }
    }
}
