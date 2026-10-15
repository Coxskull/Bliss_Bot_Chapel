using Bliss.Domain.Entities;
using Bliss.Domain.WeddingPlanner;
using Bliss.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Bliss.Tests.Persistence;

public sealed class WeddingPlannerPhase1PersistenceTests
{
    [Theory]
    [InlineData("OPERATOR", "Changed note")]
    [InlineData("SYSTEM", "Original note")]
    public async Task Conflicting_retry_preserves_message_session_and_append_audit(string requestedActor, string body)
    {
        await using var db = TestDb.CreateContext();
        var advertiser = new Advertiser { Id = Guid.NewGuid(), Name = "TEST Retry", CreatedAt = DateTime.UtcNow };
        db.Advertisers.Add(advertiser);
        await db.SaveChangesAsync();
        var service = new WeddingPlannerService(db);
        var workspace = await service.OpenPrimaryWorkspaceAsync(
            advertiser.Id, "TEST", "workspace", true, null, "OPERATOR", "tester", "request");
        var session = await service.CreateSessionAsync(
            workspace.WorkspaceId, "TEST", "session", true, null, "OPERATOR", "tester", "request");
        var original = await service.AppendMessageAsync(
            session.SessionId, "OPERATOR", "Original note", "TEST", "message", true, null, "OPERATOR", "tester", "request");
        var timestamp = (await db.WeddingPlannerPlanningSessions.SingleAsync()).UpdatedAt;
        var auditCount = await db.WeddingPlannerAuditEvents.CountAsync();

        await Assert.ThrowsAsync<WeddingPlannerConflictException>(() => service.AppendMessageAsync(
            session.SessionId, requestedActor, body, "TEST", "message", true, null, "OPERATOR", "tester", "retry"));

        db.ChangeTracker.Clear();
        var saved = await db.WeddingPlannerConversationMessages.SingleAsync();
        Assert.Equal(original.MessageId, saved.Id);
        Assert.Equal("Original note", saved.Body);
        Assert.Equal("OPERATOR", saved.ActorType);
        Assert.Equal(timestamp, (await db.WeddingPlannerPlanningSessions.SingleAsync()).UpdatedAt);
        Assert.Equal(auditCount, await db.WeddingPlannerAuditEvents.CountAsync());
    }

    [Fact]
    public async Task Advertiser_keeps_one_primary_workspace_and_append_only_messages()
    {
        await using var db = TestDb.CreateContext();
        var now = DateTime.UtcNow;
        var advertiser = new Advertiser { Id = Guid.NewGuid(), Name = "TEST Dental Manila", CreatedAt = now };
        db.Advertisers.Add(advertiser);
        await db.SaveChangesAsync();

        var service = new WeddingPlannerService(db);
        var first = await service.OpenPrimaryWorkspaceAsync(
            advertiser.Id, "TEST", "open-1", true, null, "OPERATOR", "tester", "req-1");
        var second = await service.OpenPrimaryWorkspaceAsync(
            advertiser.Id, "TEST", "open-2", true, null, "OPERATOR", "tester", "req-2");

        Assert.False(first.IsReplay);
        Assert.True(second.IsReplay);
        Assert.Equal(first.WorkspaceId, second.WorkspaceId);
        Assert.Equal(1, await db.WeddingPlannerWorkspaces.CountAsync(x => x.AdvertiserId == advertiser.Id));

        var session = await service.CreateSessionAsync(
            first.WorkspaceId, "TEST", "session-1", true, null, "OPERATOR", "tester", "req-3");
        var message = await service.AppendMessageAsync(
            session.SessionId, "OPERATOR", "First durable note", "TEST", "msg-1", true, null, "OPERATOR", "tester", "req-4");
        var replay = await service.AppendMessageAsync(
            session.SessionId, "OPERATOR", "First durable note", "TEST", "msg-1", true, null, "OPERATOR", "tester", "req-5");

        Assert.Equal(1, message.SequenceNumber);
        Assert.True(replay.IsReplay);
        Assert.Equal(message.MessageId, replay.MessageId);
        Assert.Equal(1, await db.WeddingPlannerConversationMessages.CountAsync());
        Assert.True(await db.WeddingPlannerAuditEvents.AnyAsync(x => x.Action == "MESSAGE_APPENDED"));
    }
}
