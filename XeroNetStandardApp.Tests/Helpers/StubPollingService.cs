using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using XeroNetStandardApp.Services;

namespace XeroNetStandardApp.Tests.Helpers
{
    public class StubPollingService : IPollingService
    {
        public List<(string Tenant, string Endpoint)> Calls { get; } = new();

        public Task<int> RunEndpointAsync(string tenantId, string endpointKey, DateTimeOffset callTime)
        {
            Calls.Add((tenantId, endpointKey));
            return Task.FromResult(0);
        }
    }
}
