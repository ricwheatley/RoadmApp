using System.Collections.Generic;

namespace XeroNetStandardApp.Models
{
    public class ApiCallLogViewModel
    {
        public string? TenantId { get; set; }
        public string? OrgName { get; set; }
        public List<ApiCallLogEntry> Logs { get; set; } = new();
    }
}
