//
// Helpers/TokenDebugExtensions.cs
// Ric Wheatley – June 2025
//
//  • TokenDebugExtensions.DumpToConsole()  – unchanged
//  • ScopeHelpers.GetScopes()             – now returns List<string>
//    so it drops straight into properties typed List<string>.
//
//  NOTE: remove or wrap the console output in a proper logger
//  before pushing to production.
//
using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;                 // ← needed for FirstOrDefault() and ToList()
using System.Text.Json;
using Xero.NetStandard.OAuth2.Token;

namespace XeroNetStandardApp.Helpers
{
    // ────────────────────────────────────────────────────────────────
    // Token dump helper
    // ────────────────────────────────────────────────────────────────
    public static class TokenDebugExtensions
    {
        /// <summary>
        /// Serialises the whole XeroOAuth2Token object to indented JSON and prints
        /// it to stdout (plus any ILogger you pass in).
        /// </summary>
        public static void DumpToConsole(this XeroOAuth2Token token,
                                         string label = "Token response",
                                         Action<string>? logger = null)
        {
            if (token == null)
            {
                Console.WriteLine($"{label}: (null)");
                logger?.Invoke($"{label}: (null)");
                return;
            }

            var json = JsonSerializer.Serialize(token,
                new JsonSerializerOptions { WriteIndented = true });

            Console.WriteLine($"{label}:\n{json}");
            logger?.Invoke($"{label}:\n{json}");
        }
    }

    // ────────────────────────────────────────────────────────────────
    // Scope helper – cracks open the access-token JWT and extracts the
    //              'scope' claim as a List<string>.
    // ────────────────────────────────────────────────────────────────
    public static class ScopeHelpers
    {
        public static List<string> GetScopes(this XeroOAuth2Token token)
        {
            if (token == null || string.IsNullOrWhiteSpace(token.AccessToken))
                return new List<string>();

            var handler = new JwtSecurityTokenHandler();
            var jwt = handler.ReadJwtToken(token.AccessToken);

            // Collect every "scope" or "scp" claim
            var scopeValues = jwt.Claims
                                 .Where(c => c.Type == "scope" || c.Type == "scp")
                                 .Select(c => c.Value);

            // Some libraries pack multiple scopes into one claim (space-delimited),
            // others emit one claim per scope – split & de-dup both patterns.
            var scopes = scopeValues
                        .SelectMany(v => v.Split(' ', StringSplitOptions.RemoveEmptyEntries))
                        .Distinct()
                        .ToList();

            return scopes;
        }
    }
}
