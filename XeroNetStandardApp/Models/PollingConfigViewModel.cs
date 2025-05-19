using System.Collections.Generic;

namespace XeroNetStandardApp.Models;

public class PollingConfigViewModel
{
    public List<OrgTenant> Tenants { get; set; } = new();

    /// <summary>
    /// Combined list of Accounting and Assets endpoints that can be
    /// scheduled for polling.
    /// </summary>
    public List<EndpointOption> Endpoints { get; set; } = new();
}
