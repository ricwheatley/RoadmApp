using System.Collections.Generic;

namespace XeroNetStandardApp.Models;

public sealed class XeroSyncOptions
{
    public string Schema { get; set; } = "raw";
    public List<EndpointConfig> Endpoints { get; set; } = new();
}

public sealed class EndpointConfig
{
    public string Name { get; set; } = string.Empty;
    public string ApiUrl { get; set; } = string.Empty;
    public string? ResponseKey { get; set; }
    public bool SupportsPagination { get; set; } = true;
    public bool SupportsModifiedSince { get; set; } = true;
    public string? Scopes { get; set; }
}