using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace XeroNetStandardApp.Services
{
    /// <summary>
    /// Background service that periodically checks the polling_settings table
    /// and triggers polling runs when due.
    /// </summary>
    public class PollingScheduler : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<PollingScheduler> _log;
        private readonly string _connString;

        public PollingScheduler(IServiceScopeFactory scopeFactory,
                                 ILogger<PollingScheduler> log,
                                 IConfiguration cfg)
        {
            _scopeFactory = scopeFactory;
            _log = log;
            _connString = cfg.GetConnectionString("Postgres")
                           ?? Environment.GetEnvironmentVariable("POSTGRES_CONN_STRING")
                           ?? throw new InvalidOperationException("Postgres conn string missing");
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await RunScheduledPollsAsync(stoppingToken);
                }
                catch (Exception ex)
                {
                    _log.LogError(ex, "Error running polling scheduler");
                }

                // Check once per minute
                await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
            }
        }

        private async Task RunScheduledPollsAsync(CancellationToken ct)
        {
            using var scope = _scopeFactory.CreateScope();
            var settingsSvc = scope.ServiceProvider.GetRequiredService<IPollingSettingsService>();
            var pollingSvc = scope.ServiceProvider.GetRequiredService<IPollingService>();
            var tokenSvc = scope.ServiceProvider.GetRequiredService<TokenService>();

            var token = tokenSvc.RetrieveToken();
            if (token?.Tenants == null || token.Tenants.Count == 0)
                return; // nothing to poll

            var activeTenants = token.Tenants
                                     .Select(t => t.TenantId.ToString())
                                     .ToHashSet(StringComparer.OrdinalIgnoreCase);

            var settings = await settingsSvc.GetAllAsync();
            var now = DateTimeOffset.UtcNow;

            foreach (var cfg in settings)
            {
                if (!activeTenants.Contains(cfg.OrganisationId.ToString()))
                    continue; // we don't have a token for this org

                if (string.Equals(cfg.PollingSchedule, "Off", StringComparison.OrdinalIgnoreCase))
                    continue;

                var dueTime = new DateTimeOffset(now.Date + (cfg.RunTime ?? TimeSpan.Zero), TimeSpan.Zero);
                if (string.Equals(cfg.PollingSchedule, "Weekly", StringComparison.OrdinalIgnoreCase) &&
                    now.DayOfWeek != DayOfWeek.Monday)
                    continue;

                var lastRun = await GetLastRunAsync(cfg.OrganisationId);
                if (now >= dueTime && (lastRun == null || lastRun < dueTime))
                {
                    foreach (var ep in cfg.EnabledEndpoints)
                    {
                        await pollingSvc.RunEndpointAsync(cfg.OrganisationId.ToString(), ep, now);
                    }
                }
            }
        }

        private async Task<DateTimeOffset?> GetLastRunAsync(Guid organisationId)
        {
            const string sql = "SELECT MAX(call_time) FROM utils.api_call_log WHERE organisation_id = @OrgId;";
            await using var conn = new NpgsqlConnection(_connString);
            var dt = await conn.ExecuteScalarAsync<DateTime?>(sql, new { OrgId = organisationId });
            return dt == null ? null : new DateTimeOffset(dt.Value, TimeSpan.Zero);
        }
    }
}
