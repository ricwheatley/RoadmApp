using System.Collections.Generic;

namespace XeroNetStandardApp.Models;

public class PollingConfigViewModel
{
    public List<OrgTenant> Tenants { get; set; } = new();
    public List<EndpointOption> AccountingEndpoints { get; set; } = new();
    public List<EndpointOption> AssetsEndpoints { get; set; } = new();
}
