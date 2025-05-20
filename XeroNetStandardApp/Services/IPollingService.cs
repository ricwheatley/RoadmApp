using System;
using System.Threading.Tasks;

namespace XeroNetStandardApp.Services
{
    public interface IPollingService
    {
        /// <summary>
        /// Runs the ingest pipeline for a specific endpoint and returns the
        /// number of rows inserted.
        /// </summary>
        Task<int> RunEndpointAsync(string tenantId, string endpointKey, DateTimeOffset callTime);
    }
}
