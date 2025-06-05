using System;
using System.Collections.Generic;
using System.Linq;
using Xero.NetStandard.OAuth2.Token;

namespace XeroNetStandardApp.Helpers;

public static class TokenExtensions
{
    /// <summary>
    /// Returns the authorised scopes from a <see cref="XeroOAuth2Token"/>.
    /// Handles different library versions where scopes may be stored
    /// as either a list or a space separated string.
    /// </summary>
    public static List<string> GetScopes(this XeroOAuth2Token token)
    {
        if (token == null) return new List<string>();

        // Check for a property named "Scopes" first
        var scopesProp = token.GetType().GetProperty("Scopes");
        if (scopesProp != null)
        {
            var value = scopesProp.GetValue(token);
            if (value is IEnumerable<string> list)
                return list.ToList();
            if (value is string str && !string.IsNullOrWhiteSpace(str))
                return str.Split(' ', StringSplitOptions.RemoveEmptyEntries).ToList();
        }

        // Fall back to a single "Scope" string property
        var scopeProp = token.GetType().GetProperty("Scope");
        if (scopeProp != null)
        {
            var str = scopeProp.GetValue(token) as string;
            if (!string.IsNullOrWhiteSpace(str))
                return str.Split(' ', StringSplitOptions.RemoveEmptyEntries).ToList();
        }

        return new List<string>();
    }
}