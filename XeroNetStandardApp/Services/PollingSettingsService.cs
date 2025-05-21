using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dapper;
using Microsoft.Extensions.Configuration;
using Npgsql;
using XeroNetStandardApp.Models;

namespace XeroNetStandardApp.Services
{
    public class PollingSettingsService : IPollingSettingsService
    {
        private readonly string _connString;

        public PollingSettingsService(IConfiguration cfg)
        {
            _connString = cfg.GetConnectionString("Postgres")
                         ?? Environment.GetEnvironmentVariable("POSTGRES_CONN_STRING")
                         ?? throw new InvalidOperationException("Postgres conn string missing");
        }

        public async Task<PollingSetting?> GetAsync(Guid organisationId)
        {
            const string sql = @"SELECT organisation_id AS OrganisationId,
                                         polling_schedule AS PollingSchedule,
                                         run_time AS RunTime,
                                         enabled_endpoints AS EnabledEndpoints
                                    FROM utils.polling_settings
                                   WHERE organisation_id = @OrgId;";
            await using var conn = new NpgsqlConnection(_connString);
            return await conn.QueryFirstOrDefaultAsync<PollingSetting>(sql, new { OrgId = organisationId });
        }

        public async Task<IReadOnlyDictionary<Guid, PollingSetting>> GetManyAsync(IEnumerable<Guid> organisationIds)
        {
            var ids = organisationIds.ToArray();
            if (ids.Length == 0) return new Dictionary<Guid, PollingSetting>();

            const string sql = @"SELECT organisation_id AS OrganisationId,
                                         polling_schedule AS PollingSchedule,
                                         run_time AS RunTime,
                                         enabled_endpoints AS EnabledEndpoints
                                    FROM utils.polling_settings
                                   WHERE organisation_id = ANY(@OrgIds);";
            await using var conn = new NpgsqlConnection(_connString);
            var list = await conn.QueryAsync<PollingSetting>(sql, new { OrgIds = ids });
            return list.ToDictionary(p => p.OrganisationId);
        }

        public async Task<IReadOnlyList<PollingSetting>> GetAllAsync()
        {
            const string sql = @"SELECT organisation_id AS OrganisationId,
                                         polling_schedule AS PollingSchedule,
                                         run_time AS RunTime,
                                         enabled_endpoints AS EnabledEndpoints
                                    FROM backendutils.polling_settings;";
            await using var conn = new NpgsqlConnection(_connString);
            var list = await conn.QueryAsync<PollingSetting>(sql);
            return list.AsList();
        }

        public async Task UpsertAsync(Guid organisationId, string schedule, TimeSpan? runTime, IEnumerable<string> endpoints)
        {
            const string sql = @"INSERT INTO utils.polling_settings
                                 (organisation_id, polling_schedule, run_time, enabled_endpoints)
                                 VALUES (@OrgId, @Sched, @RunTime, @Endpoints)
                                 ON CONFLICT (organisation_id)
                                 DO UPDATE SET polling_schedule = EXCLUDED.polling_schedule,
                                               run_time = EXCLUDED.run_time,
                                               enabled_endpoints = EXCLUDED.enabled_endpoints;";
            await using var conn = new NpgsqlConnection(_connString);
            await conn.ExecuteAsync(sql, new
            {
                OrgId = organisationId,
                Sched = schedule,
                RunTime = runTime,
                Endpoints = endpoints.ToArray()
            });
        }
    }
}

