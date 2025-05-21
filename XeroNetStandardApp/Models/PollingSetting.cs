namespace XeroNetStandardApp.Models;

public class PollingSetting
{
    public System.Guid OrganisationId { get; set; }
    public string PollingSchedule { get; set; } = string.Empty;
    public System.TimeSpan? RunTime { get; set; }
    public string[] EnabledEndpoints { get; set; } = System.Array.Empty<string>();
}
