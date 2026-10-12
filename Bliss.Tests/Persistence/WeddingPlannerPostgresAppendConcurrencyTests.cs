using Bliss.Domain.Entities;
using Bliss.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Bliss.Tests.Persistence;

/// <summary>
/// PostgreSQL integration coverage for concurrent Wedding Planner message appends.
/// CI requires BLISS_TEST_POSTGRES_CONNECTION; local runs without that opt-in
/// return without exercising PostgreSQL.
/// </summary>
public sealed class WeddingPlannerPostgresAppendConcurrencyTests
{
    [Fact]
    public async Task Concurrent_appends_serialize_sequences_and_replay_same_idempotency_key()
    {
        var baseConnectionString = Environment.GetEnvironmentVariable("BLISS_TEST_POSTGRES_CONNECTION");
        if (string.IsNullOrWhiteSpace(baseConnectionString))
        {
            // Never report an unexecuted PostgreSQL integration test as passed in CI.
            Assert.False(string.Equals(Environment.GetEnvironmentVariable("CI"), "true", StringComparison.OrdinalIgnoreCase),
                "BLISS_TEST_POSTGRES_CONNECTION must be configured for CI PostgreSQL concurrency coverage.");
            return; // Local developer runs may omit the optional PostgreSQL fixture.
        }

        var schema = $"wp_append_ci_{Guid.NewGuid():N}";
        var advertiserId = Guid.NewGuid();
        var schemaBuilder = new NpgsqlConnectionStringBuilder(baseConnectionString)
        {
            SearchPath = schema
        };
        var options = new DbContextOptionsBuilder<BlissDbContext>()
            .UseNpgsql(schemaBuilder.ConnectionString)
            .Options;

        await using (var admin = new NpgsqlConnection(baseConnectionString))
        {
            await admin.OpenAsync();
            await using var createSchema = admin.CreateCommand();
            createSchema.CommandText = $"CREATE SCHEMA \"{schema}\";";
            await createSchema.ExecuteNonQueryAsync();
        }

        try
        {
            Guid sessionId;
            await using (var setup = new BlissDbContext(options))
            {
                await setup.Database.EnsureCreatedAsync();
                setup.Advertisers.Add(new Advertiser
                {
                    Id = advertiserId,
                    Name = "Wedding Planner append concurrency test",
                    CreatedAt = DateTime.UtcNow
                });
                await setup.SaveChangesAsync();

                var service = new WeddingPlannerService(setup);
                var workspace = await service.OpenPrimaryWorkspaceAsync(
                    advertiserId,
                    "POSTGRES_APPEND_TEST",
                    $"workspace-{Guid.NewGuid():N}",
                    true,
                    null,
                    "OPERATOR",
                    "CI integration test",
                    "workspace-request");
                var session = await service.CreateSessionAsync(
                    workspace.WorkspaceId,
                    "POSTGRES_APPEND_TEST",
                    $"session-{Guid.NewGuid():N}",
                    true,
                    null,
                    "OPERATOR",
                    "CI integration test",
                    "session-request");
                sessionId = session.SessionId;
            }

            async Task<WeddingPlannerMessageResult> Append(string key, string body)
            {
                await using var db = new BlissDbContext(options);
                var service = new WeddingPlannerService(db);
                return await service.AppendMessageAsync(
                    sessionId,
                    "SYSTEM",
                    body,
                    "POSTGRES_APPEND_TEST",
                    key,
                    true,
                    null,
                    "OPERATOR",
                    "CI integration test",
                    $"append-{Guid.NewGuid():N}");
            }

            const string sharedKey = "same-request-key";
            var sameKeyResults = await Task.WhenAll(
                Append(sharedKey, "same logical request"),
                Append(sharedKey, "same logical request"));

            Assert.Equal(sameKeyResults[0].MessageId, sameKeyResults[1].MessageId);
            Assert.Single(sameKeyResults, result => result.IsReplay);
            Assert.Single(sameKeyResults, result => !result.IsReplay);

            var distinctKeyResults = await Task.WhenAll(
                Append("distinct-request-a", "first distinct request"),
                Append("distinct-request-b", "second distinct request"));

            Assert.NotEqual(distinctKeyResults[0].MessageId, distinctKeyResults[1].MessageId);

            await using var verify = new BlissDbContext(options);
            var messages = await verify.WeddingPlannerConversationMessages
                .Where(message => message.SessionId == sessionId)
                .OrderBy(message => message.SequenceNumber)
                .ToListAsync();
            Assert.Equal(new[] { 1, 2, 3 }, messages.Select(message => message.SequenceNumber));
            Assert.Equal(3, await verify.WeddingPlannerAuditEvents.CountAsync(
                audit => audit.SessionId == sessionId && audit.Action == "MESSAGE_APPENDED"));

            var persistedSession = await verify.WeddingPlannerPlanningSessions
                .SingleAsync(session => session.Id == sessionId);
            Assert.True(persistedSession.UpdatedAt >= messages.Max(message => message.CreatedAt));
        }
        finally
        {
            await using var admin = new NpgsqlConnection(baseConnectionString);
            await admin.OpenAsync();
            await using var dropSchema = admin.CreateCommand();
            dropSchema.CommandText = $"DROP SCHEMA IF EXISTS \"{schema}\" CASCADE;";
            await dropSchema.ExecuteNonQueryAsync();
        }
    }
}
