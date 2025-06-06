using System.Net;

namespace XeroNetStandardApp.Models;

/// <summary>
/// Per-endpoint, per-status ingest outcome, suitable for serialising to the UI.
/// </summary>
public sealed record EndpointIngestReport(
    string EndpointName,
    string? Status,
    int RowsInserted,
    bool WasUpToDate,           // ← note the capital W
    HttpStatusCode ResponseCode,
    string? ErrorDetail);
