using System.Threading.Tasks;

namespace XeroNetStandardApp.Services
{
    /// <summary>
    /// Pushes raw JSON from the Xero API into Postgres.
    /// </summary>
    public interface IXeroRawIngestService
    {
        /// Run every configured endpoint for the tenant (existing behaviour).
        Task<int> RunOnceAsync(string accessToken, string tenantId);

        /// Run just one endpoint for the tenant (NEW).
        Task<int> RunOnceAsync(string accessToken, string tenantId, string endpointKey);
    }
}
