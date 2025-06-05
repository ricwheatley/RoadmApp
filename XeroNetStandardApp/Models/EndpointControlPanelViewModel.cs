using System;
using System.Collections.Generic;

namespace XeroNetStandardApp.Models
{
    public class EndpointControlPanelViewModel
    {
        public List<OrgTenant> Tenants { get; set; } = new();
        public List<EndpointOption> Endpoints { get; set; } = new();
        public Dictionary<string, PollingStats> Stats { get; set; } = new();
    }

    public class OrgTenant
    {
        public string? TenantId { get; set; }
        public string? OrgName { get; set; }
        public Dictionary<string, string> Schedules { get; set; } = new();

        public string GetScheduleFor(string endpointKey) =>
            Schedules.TryGetValue(endpointKey, out var val) ? val : "—";
        public DateTimeOffset? LastCallUtc { get; set; }
        public int? LastRowsInserted { get; set; }

        /// <summary>
        /// Scopes authorised for this tenant.
        /// </summary>
        public List<string> Scopes { get; set; } = new();
    }

    public class EndpointOption
    {
        public string? Key { get; set; }
        public string? DisplayName { get; set; }
    }
}
