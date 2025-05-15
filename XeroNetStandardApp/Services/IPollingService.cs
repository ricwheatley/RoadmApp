using System.Threading.Tasks;

namespace XeroNetStandardApp.Services
{
    public interface IPollingService
    {
        Task RunEndpointAsync(string tenantId, string endpointKey);
    }
}