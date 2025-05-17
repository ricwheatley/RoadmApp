namespace XeroNetStandardApp.Services
{
    /// <summary>
    /// Pushes raw JSON from the Xero Assets API into Postgres.
    /// </summary>
    public interface IXeroAssetsIngestService
    {
        /// Run every configured endpoint for the tenant.
        Task<int> RunOnceAsync(string tenantId);

        /// Run just one endpoint for the tenant.
        Task<int> RunOnceAsync(string tenantId, string endpointKey);
    }
}
