using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using XeroNetStandardApp.Models;

namespace XeroNetStandardApp.Services
{
    public interface IPollingSettingsService
    {
        Task<PollingSetting?> GetAsync(Guid organisationId);
        Task<IReadOnlyDictionary<Guid, PollingSetting>> GetManyAsync(IEnumerable<Guid> organisationIds);
        Task UpsertAsync(Guid organisationId, string schedule, TimeSpan? runTime, IEnumerable<string> endpoints);
    }
}
