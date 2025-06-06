using System.Collections.Generic;
using System;

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
    public int? PageSize { get; set; }
    public bool SupportsOffset { get; set; } = false; 
    public bool SupportsModifiedSince { get; set; } = true;
    public string? Scopes { get; set; }

    /// <summary>
    /// Optional list like ["REGISTERED","DRAFT","DISPOSED"].
    /// Leave null/empty if the endpoint doesn’t need a status parameter.
    /// </summary>
    public string[]? Status { get; set; } = Array.Empty<string>();
}