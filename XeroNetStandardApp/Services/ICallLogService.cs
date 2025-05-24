using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using XeroNetStandardApp.Models;

namespace XeroNetStandardApp.Services
{
    public interface ICallLogService
    {
        Task<IReadOnlyList<ApiCallLogEntry>> GetLogsAsync(Guid organisationId);

        Task<IDictionary<Guid, CallStats>> GetLatestStatsAsync(IEnumerable<Guid> tenantIds);
    }
}
