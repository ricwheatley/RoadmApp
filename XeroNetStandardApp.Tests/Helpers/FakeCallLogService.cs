// XeroNetStandardApp.Tests/Helpers/FakeCallLogService.cs
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using XeroNetStandardApp.Models;
using XeroNetStandardApp.Services;

namespace XeroNetStandardApp.Tests.Helpers
{
    /// <summary>
    /// Test-only implementation of <see cref="ICallLogService"/>.
    /// Every member returns an empty result so no database or
    /// configuration is needed when the API runs under test.
    /// </summary>
    public sealed class FakeCallLogService : ICallLogService
    {
        /* ----------------------------------------------------------
         * 1.  LogAsync – used by the production code to write a row
         * ---------------------------------------------------------- */
        public Task LogAsync(ApiCallLogEntry _entry, CancellationToken _ = default)
            => Task.CompletedTask;

        /* ----------------------------------------------------------
         * 2.  GetLogsAsync – return an empty list for the call group
         * ---------------------------------------------------------- */
        public Task<IReadOnlyCollection<ApiCallLogEntry>> GetLogsAsync(
            Guid _callGroupId,
            CancellationToken _ = default)
            => Task.FromResult<IReadOnlyCollection<ApiCallLogEntry>>(Array.Empty<ApiCallLogEntry>());

        /* ----------------------------------------------------------
         * 3.  GetLatestStatsAsync – return an empty dictionary
         * ---------------------------------------------------------- */
        public Task<IDictionary<Guid, CallStats>> GetLatestStatsAsync(
            IEnumerable<Guid> _callGroupIds,
            CancellationToken _ = default)
            => Task.FromResult<IDictionary<Guid, CallStats>>(
                   new Dictionary<Guid, CallStats>());

        Task<IReadOnlyList<ApiCallLogEntry>> ICallLogService.GetLogsAsync(Guid organisationId)
        {
            throw new NotImplementedException();
        }

        Task<IDictionary<Guid, CallStats>> ICallLogService.GetLatestStatsAsync(IEnumerable<Guid> tenantIds)
        {
            throw new NotImplementedException();
        }
    }
}
