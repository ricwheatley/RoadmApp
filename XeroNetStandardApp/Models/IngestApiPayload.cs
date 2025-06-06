using System.Collections.Generic;

namespace XeroNetStandardApp.Models
{
    public sealed class IngestApiPayload
    {
        public int TotalInserted { get; init; }
        public IReadOnlyList<EndpointIngestReport> Reports { get; init; } = [];
        public List<ErrorSummary> Errors { get; init; } = [];
    }

    public sealed class ErrorSummary
    {
        public string EndpointName { get; init; } = "";
        public string? Status { get; init; }
        public int Code { get; init; }
        public string? ErrorDetail { get; init; }
    }
}
