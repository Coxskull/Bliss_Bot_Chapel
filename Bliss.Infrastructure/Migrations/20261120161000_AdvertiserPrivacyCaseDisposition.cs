using Bliss.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bliss.Infrastructure.Migrations;

// Append-only, non-destructive columns to the pre-existing private account case.
// Never changes registration receipts, Auth identities, or optional marketing choice.
[DbContext(typeof(BlissDbContext))]
[Migration("20261120161000_AdvertiserPrivacyCaseDisposition")]
public sealed class AdvertiserPrivacyCaseDisposition : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "DisposedBySubject", table: "AdvertiserPrivacyRequests",
            type: "character varying(255)", maxLength: 255, nullable: true);
        migrationBuilder.AddColumn<DateTime>(
            name: "DisposedAt", table: "AdvertiserPrivacyRequests",
            type: "timestamp with time zone", nullable: true);
        migrationBuilder.AddColumn<string>(
            name: "DispositionEvidenceSha256", table: "AdvertiserPrivacyRequests",
            type: "character varying(64)", maxLength: 64, nullable: true);
        migrationBuilder.AddCheckConstraint(
            name: "CK_AdvertiserPrivacyRequests_DispositionEvidence",
            table: "AdvertiserPrivacyRequests",
            sql: "\"Status\" IN ('RECEIVED', 'VERIFYING') OR ("
                + "\"DispositionEvidenceSha256\" IS NOT NULL AND length(\"DispositionEvidenceSha256\") = 64 "
                + "AND \"DisposedBySubject\" IS NOT NULL AND \"DisposedAt\" IS NOT NULL)");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropCheckConstraint(
            name: "CK_AdvertiserPrivacyRequests_DispositionEvidence",
            table: "AdvertiserPrivacyRequests");
        migrationBuilder.DropColumn(name: "DisposedBySubject", table: "AdvertiserPrivacyRequests");
        migrationBuilder.DropColumn(name: "DisposedAt", table: "AdvertiserPrivacyRequests");
        migrationBuilder.DropColumn(name: "DispositionEvidenceSha256", table: "AdvertiserPrivacyRequests");
    }
}
