using Bliss.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bliss.Infrastructure.Migrations;

[DbContext(typeof(BlissDbContext))]
[Migration("20261107100000_SecureCreatorRegistration")]
public sealed class SecureCreatorRegistration : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "CreatorAccountMemberships",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                Subject = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                CreatorId = table.Column<Guid>(type: "uuid", nullable: false),
                SubmittedChannelUrl = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: false),
                Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false, defaultValue: "PENDING_REVIEW"),
                CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
            }, constraints: table =>
            {
                table.PrimaryKey("PK_CreatorAccountMemberships", x => x.Id);
                table.CheckConstraint("CK_CreatorAccountMemberships_Status", "\"Status\" IN ('PENDING_REVIEW', 'ACTIVE', 'REVOKED')");
                table.ForeignKey("FK_CreatorAccountMemberships_Creators_CreatorId", x => x.CreatorId, "Creators", "Id", onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "CreatorRegistrationReceipts",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                RequestKey = table.Column<Guid>(type: "uuid", nullable: false),
                Subject = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                CreatorId = table.Column<Guid>(type: "uuid", nullable: false),
                RequestSha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                PrivacyNoticeVersion = table.Column<string>(type: "character varying(96)", maxLength: 96, nullable: false),
                TermsVersion = table.Column<string>(type: "character varying(96)", maxLength: 96, nullable: false),
                ProcessingConsent = table.Column<bool>(type: "boolean", nullable: false),
                TermsAccepted = table.Column<bool>(type: "boolean", nullable: false),
                MarketingOptIn = table.Column<bool>(type: "boolean", nullable: false),
                CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
            }, constraints: table =>
            {
                table.PrimaryKey("PK_CreatorRegistrationReceipts", x => x.Id);
                table.CheckConstraint("CK_CreatorRegistrationReceipts_Consent", "\"ProcessingConsent\" AND \"TermsAccepted\"");
                table.CheckConstraint("CK_CreatorRegistrationReceipts_RequestSha256", "\"RequestSha256\" ~ '^[0-9a-f]{64}$'");
                table.ForeignKey("FK_CreatorRegistrationReceipts_Creators_CreatorId", x => x.CreatorId, "Creators", "Id", onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex("IX_CreatorAccountMemberships_CreatorId", "CreatorAccountMemberships", "CreatorId", unique: true);
        migrationBuilder.CreateIndex("IX_CreatorAccountMemberships_Subject", "CreatorAccountMemberships", "Subject", unique: true);
        migrationBuilder.CreateIndex("IX_CreatorRegistrationReceipts_CreatorId", "CreatorRegistrationReceipts", "CreatorId");
        migrationBuilder.CreateIndex("IX_CreatorRegistrationReceipts_RequestKey", "CreatorRegistrationReceipts", "RequestKey", unique: true);
        migrationBuilder.CreateIndex("IX_CreatorRegistrationReceipts_Subject_CreatorId", "CreatorRegistrationReceipts", new[] { "Subject", "CreatorId" }, unique: true);

        migrationBuilder.Sql("REVOKE ALL ON TABLE \"CreatorAccountMemberships\", \"CreatorRegistrationReceipts\" FROM PUBLIC;");
        migrationBuilder.Sql("DO $$ BEGIN IF EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'anon') THEN EXECUTE 'REVOKE ALL ON TABLE \"CreatorAccountMemberships\", \"CreatorRegistrationReceipts\" FROM anon'; END IF; IF EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'authenticated') THEN EXECUTE 'REVOKE ALL ON TABLE \"CreatorAccountMemberships\", \"CreatorRegistrationReceipts\" FROM authenticated'; END IF; END $$;");
        migrationBuilder.Sql("CREATE FUNCTION prevent_creator_registration_receipt_mutation() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN RAISE EXCEPTION 'creator registration receipts are append-only'; END; $$;");
        migrationBuilder.Sql("CREATE TRIGGER creator_registration_receipts_immutable BEFORE UPDATE OR DELETE ON \"CreatorRegistrationReceipts\" FOR EACH ROW EXECUTE FUNCTION prevent_creator_registration_receipt_mutation();");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("DROP TRIGGER IF EXISTS creator_registration_receipts_immutable ON \"CreatorRegistrationReceipts\";");
        migrationBuilder.Sql("DROP FUNCTION IF EXISTS prevent_creator_registration_receipt_mutation();");
        migrationBuilder.DropTable("CreatorRegistrationReceipts");
        migrationBuilder.DropTable("CreatorAccountMemberships");
    }
}
