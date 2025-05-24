using System.Collections.Generic;

namespace XeroNetStandardApp.Models
{
    public class HomeIndexViewModel
    {
        public bool IsConnected { get; set; }
        public List<OrgTenant> Tenants { get; set; } = new();
        public Dictionary<string, CallStats> Stats { get; } = new();
    }
}
