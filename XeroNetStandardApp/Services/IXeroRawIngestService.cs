using System.Collections.Generic;
using System.Threading.Tasks;
using XeroNetStandardApp.Models;

namespace XeroNetStandardApp.Services
{
    public interface IXeroRawIngestService
    {
        /// <summary>Ingests all configured endpoints for the tenant.</summary>
        Task<IReadOnlyList<EndpointIngestReport>> RunOnceAsync(string tenantId);

        /// <summary>Ingests a single endpoint (identified by <paramref name="endpointKey"/>) for the tenant.</summary>
        Task<IReadOnlyList<EndpointIngestReport>> RunOnceAsync(string tenantId, string endpointKey);
    }
}
