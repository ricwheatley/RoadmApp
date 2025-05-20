using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using XeroNetStandardApp.Services;
using XeroNetStandardApp.Models;

namespace XeroNetStandardApp.Services
{
    public interface IPollingService
    {
        /// <summary>
        /// Runs the ingest pipeline for a specific endpoint and returns the
        /// number of rows inserted.
        /// </summary>
        Task<int> RunEndpointAsync(string tenantId, string endpointKey, DateTimeOffset callTime);

        /// <summary>
        /// Return aggregated statistics for each organisation based on the
        /// <c>utils.api_call_log</c> table.
        /// </summary>
        Task<IReadOnlyList<PollingStats>> GetPollingStatsAsync();

        /// <summary>
        /// Return aggregated statistics for the given run (identified by
        /// <paramref name="callTime"/>). This is useful for summarising a
        /// manual polling operation.
        /// </summary>
        Task<IReadOnlyList<PollingStats>> GetPollingStatsForRunAsync(DateTimeOffset callTime);
    }
}
