namespace XeroNetStandardApp.Models
{
    public class PollingStats
    {
        public System.Guid OrganisationId { get; set; }
        public System.DateTimeOffset LastCall { get; set; }
        public bool EndpointsSuccess { get; set; }
        public int EndpointsFail { get; set; }
        public int RecordsInserted { get; set; }
    }
}
