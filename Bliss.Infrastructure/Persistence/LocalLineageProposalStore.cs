using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Bliss.Infrastructure.Persistence;

// Trusted local policy only; deliberately not registered in DI or exposed as a route.
public sealed record LocalLineageProposalGrant(LocalLineageGrant Reader, bool ProposalWriteGranted);
public sealed record LocalLineageProposalRequest(Guid RequestId, string EvidenceId, LocalLineageSelection Selection, string ExpectedSha256);
public sealed record LocalLineageProposal(Guid Id, string EvidenceId, string Json, string Sha256, DateTime CreatedAt,
    string Status = "PENDING_REVIEW");

/// <summary>Append-only frozen proposals in an explicitly initialized disposable local fixture.</summary>
public sealed class LocalLineageProposalStore(BlissDbContext db)
{
    // Explicit fixture setup only. No production migration or automatic schema installation.
    public const string FixtureSchemaSql = """
        CREATE TABLE local_lineage_proposals (
            id uuid PRIMARY KEY, advertiser_id uuid NOT NULL, actor_subject varchar(128) NOT NULL,
            request_id uuid NOT NULL, evidence_id varchar(43) NOT NULL,
            request_fingerprint char(64) NOT NULL, report_json text NOT NULL CHECK (octet_length(report_json) <= 262144),
            report_sha256 char(64) NOT NULL, created_at timestamptz NOT NULL DEFAULT clock_timestamp(),
            status text NOT NULL DEFAULT 'PENDING_REVIEW' CHECK (status = 'PENDING_REVIEW'),
            UNIQUE (advertiser_id, actor_subject, request_id),
            CHECK ((report_json::jsonb->>'classification' = 'LOCAL_SYNTHETIC') IS TRUE),
            CHECK ((report_json::jsonb->>'paymentAuthorized' = 'false') IS TRUE),
            CHECK ((report_json::jsonb->>'activationAuthorized' = 'false') IS TRUE),
            CHECK ((report_json::jsonb->>'independentQaAccepted' = 'false') IS TRUE),
            CHECK ((report_json::jsonb->>'consentVerified' = 'false') IS TRUE),
            CHECK ((report_json::jsonb->>'creativeRightsVerified' = 'false') IS TRUE)
        );
        CREATE FUNCTION reject_local_lineage_mutation() RETURNS trigger LANGUAGE plpgsql AS $$
        BEGIN RAISE EXCEPTION 'Local lineage proposals are append-only'; END $$;
        CREATE TRIGGER local_lineage_immutable BEFORE UPDATE OR DELETE OR TRUNCATE ON local_lineage_proposals
            FOR EACH STATEMENT EXECUTE FUNCTION reject_local_lineage_mutation();
        """;

    private static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();
    private NpgsqlConnection Connection(LocalLineageGrant grant)
    {
        if (!grant.Authenticated || !grant.EvidenceReadGranted || string.IsNullOrWhiteSpace(grant.Subject)
            || grant.Subject.Length > 128 || grant.AdvertiserId == Guid.Empty)
            throw new InvalidOperationException("Trusted advertiser-scoped evidence grant required.");
        if (db.Database.ProviderName != "Npgsql.EntityFrameworkCore.PostgreSQL" || db.ChangeTracker.HasChanges()
            || db.Database.CurrentTransaction is not null)
            throw new InvalidOperationException("Dedicated clean PostgreSQL context required.");
        var target = new NpgsqlConnectionStringBuilder(db.Database.GetConnectionString());
        if (target.Host is not ("localhost" or "127.0.0.1" or "::1") || target.Database is not ("campaign_test" or "bliss_ci"))
            throw new InvalidOperationException("Only a named loopback disposable fixture is supported.");
        return new(target.ConnectionString);
    }

    public async Task<LocalLineageProposal> ProposeAsync(LocalLineageProposalGrant grant, LocalLineageProposalRequest request, CancellationToken ct = default)
    {
        await using var connection = Connection(grant.Reader);
        if (!grant.ProposalWriteGranted) throw new InvalidOperationException("Separate trusted proposal-write permission required.");
        if (request.RequestId == Guid.Empty || request.Selection.IngestionRunId == Guid.Empty
            || request.Selection.FormationRunId == Guid.Empty || request.Selection.PlacementRunId == Guid.Empty
            || request.EvidenceId.Length != 43 || !request.EvidenceId.StartsWith("AGM-EV-", StringComparison.Ordinal)
            || !Guid.TryParseExact(request.EvidenceId[7..], "D", out var evidenceUuid)
            || evidenceUuid.ToString("D") != request.EvidenceId[7..] || request.EvidenceId[21] != '4'
            || !"89ab".Contains(request.EvidenceId[26]) || request.ExpectedSha256.Length != 64
            || request.ExpectedSha256.Any(c => !"0123456789abcdef".Contains(c)))
            throw new InvalidOperationException("Explicit selections, canonical UUIDv4 evidence ID and SHA256 required.");
        var fingerprint = Hash(JsonSerializer.Serialize(request));
        await connection.OpenAsync(ct);
        // Replay returns the originally captured proposal; never silently refresh its evidence snapshot.
        var existing = await Find(connection, grant.Reader, request.RequestId, fingerprint, ct);
        if (existing is not null) return existing;
        var report = await new LocalCampaignLineageEvidence(db).ReadAsync(grant.Reader, request.Selection, ct);
        if (report.Sha256 != request.ExpectedSha256) throw new InvalidOperationException("Selected evidence changed; obtain and review a new snapshot.");
        await using var insert = new NpgsqlCommand("""
            INSERT INTO local_lineage_proposals(id,advertiser_id,actor_subject,request_id,evidence_id,request_fingerprint,report_json,report_sha256)
            VALUES(@id,@advertiser,@actor,@request,@evidence,@fingerprint,@json,@sha)
            ON CONFLICT(advertiser_id,actor_subject,request_id) DO NOTHING
            """, connection);
        insert.Parameters.AddWithValue("id", Guid.NewGuid()); insert.Parameters.AddWithValue("advertiser", grant.Reader.AdvertiserId);
        insert.Parameters.AddWithValue("actor", grant.Reader.Subject); insert.Parameters.AddWithValue("request", request.RequestId);
        insert.Parameters.AddWithValue("evidence", request.EvidenceId); insert.Parameters.AddWithValue("fingerprint", fingerprint);
        insert.Parameters.AddWithValue("json", report.Json); insert.Parameters.AddWithValue("sha", report.Sha256);
        await insert.ExecuteNonQueryAsync(ct);
        return await Find(connection, grant.Reader, request.RequestId, fingerprint, ct)
            ?? throw new InvalidOperationException("Proposal insert did not produce a durable record.");
    }

    private static async Task<LocalLineageProposal?> Find(NpgsqlConnection connection, LocalLineageGrant grant, Guid requestId, string fingerprint, CancellationToken ct)
    {
        await using var cmd = new NpgsqlCommand("""
            SELECT id,evidence_id,report_json,report_sha256,created_at,request_fingerprint,status FROM local_lineage_proposals
            WHERE advertiser_id=@advertiser AND actor_subject=@actor AND request_id=@request
            """, connection);
        cmd.Parameters.AddWithValue("advertiser", grant.AdvertiserId); cmd.Parameters.AddWithValue("actor", grant.Subject);
        cmd.Parameters.AddWithValue("request", requestId);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct)) return null;
        if (reader.GetString(5) != fingerprint) throw new InvalidOperationException("Idempotency key already binds another proposal intent.");
        var json = reader.GetString(2); var sha = reader.GetString(3);
        if (Hash(json) != sha) throw new InvalidOperationException("Stored proposal integrity check failed.");
        return new(reader.GetGuid(0), reader.GetString(1), json, sha, reader.GetDateTime(4), reader.GetString(6));
    }
}
