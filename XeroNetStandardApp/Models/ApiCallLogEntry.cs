using System;

namespace XeroNetStandardApp.Models
{
    public class ApiCallLogEntry
    {
        public DateTimeOffset CallTime { get; set; }
        public string? Endpoint { get; set; }
        public int RowsInserted { get; set; }
        public int? StatusCode { get; set; }
        public bool Success { get; set; }
        public string? ErrorMessage { get; set; }
    }
}
