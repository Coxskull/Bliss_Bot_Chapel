using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Bliss.Infrastructure.Persistence;

#nullable disable

namespace Bliss.Infrastructure.Migrations;

[DbContext(typeof(BlissDbContext))]
[Migration("20261107090000_SecureAdvertiserRegistration")]
public sealed class SecureAdvertiserRegistration : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "AdvertiserAccountMemberships",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                Subject = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                AdvertiserId = table.Column<Guid>(type: "uuid", nullable: false),
                Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false, defaultValue: "PENDING_REVIEW"),
                CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
            }, constraints: table =>
            {
                table.PrimaryKey("PK_AdvertiserAccountMemberships", x => x.Id);
                table.CheckConstraint("CK_AdvertiserAccountMemberships_Status", "\"Status\" IN ('PENDING_REVIEW', 'ACTIVE', 'REVOKED')");
                table.ForeignKey("FK_AdvertiserAccountMemberships_Advertisers_AdvertiserId", x => x.AdvertiserId, "Advertisers", "Id", onDelete: ReferentialAction.Restrict);
            });
        migrationBuilder.CreateTable(
            name: "AdvertiserRegistrationReceipts",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                RequestKey = table.Column<Guid>(type: "uuid", nullable: false),
                Subject = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                AdvertiserId = table.Column<Guid>(type: "uuid", nullable: false),
                RequestSha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                PrivacyNoticeVersion = table.Column<string>(type: "character varying(96)", maxLength: 96, nullable: false),
                TermsVersion = table.Column<string>(type: "character varying(96)", maxLength: 96, nullable: false),
                ProcessingConsent = table.Column<bool>(type: "boolean", nullable: false),
                TermsAccepted = table.Column<bool>(type: "boolean", nullable: false),
                MarketingOptIn = table.Column<bool>(type: "boolean", nullable: false),
                CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
            }, constraints: table =>
            {
                table.PrimaryKey("PK_AdvertiserRegistrationReceipts", x => x.Id);
                table.CheckConstraint("CK_AdvertiserRegistrationReceipts_Consent", "\"ProcessingConsent\" AND \"TermsAccepted\"");
                table.CheckConstraint("CK_AdvertiserRegistrationReceipts_RequestSha256", "\"RequestSha256\" ~ '^[0-9a-f]{64}$'");
                table.ForeignKey("FK_AdvertiserRegistrationReceipts_Advertisers_AdvertiserId", x => x.AdvertiserId, "Advertisers", "Id", onDelete: ReferentialAction.Restrict);
            });
        migrationBuilder.CreateIndex("IX_AdvertiserAccountMemberships_Subject", "AdvertiserAccountMemberships", "Subject", unique: true);
        migrationBuilder.CreateIndex("IX_AdvertiserAccountMemberships_AdvertiserId", "AdvertiserAccountMemberships", "AdvertiserId", unique: true);
        migrationBuilder.CreateIndex("IX_AdvertiserRegistrationReceipts_RequestKey", "AdvertiserRegistrationReceipts", "RequestKey", unique: true);
        migrationBuilder.CreateIndex("IX_AdvertiserRegistrationReceipts_Subject_AdvertiserId", "AdvertiserRegistrationReceipts", new[] { "Subject", "AdvertiserId" }, unique: true);

        migrationBuilder.Sql("REVOKE ALL ON TABLE \"AdvertiserAccountMemberships\", \"AdvertiserRegistrationReceipts\" FROM PUBLIC;");
        migrationBuilder.Sql("DO $$ BEGIN IF EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'anon') THEN EXECUTE 'REVOKE ALL ON TABLE \"AdvertiserAccountMemberships\", \"AdvertiserRegistrationReceipts\" FROM anon'; END IF; IF EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'authenticated') THEN EXECUTE 'REVOKE ALL ON TABLE \"AdvertiserAccountMemberships\", \"AdvertiserRegistrationReceipts\" FROM authenticated'; END IF; END $$;");
        migrationBuilder.Sql("CREATE FUNCTION prevent_advertiser_registration_receipt_mutation() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN RAISE EXCEPTION 'advertiser registration receipts are append-only'; END; $$;");
        migrationBuilder.Sql("CREATE TRIGGER advertiser_registration_receipts_immutable BEFORE UPDATE OR DELETE ON \"AdvertiserRegistrationReceipts\" FOR EACH ROW EXECUTE FUNCTION prevent_advertiser_registration_receipt_mutation();");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("DROP TRIGGER IF EXISTS advertiser_registration_receipts_immutable ON \"AdvertiserRegistrationReceipts\";");
        migrationBuilder.Sql("DROP FUNCTION IF EXISTS prevent_advertiser_registration_receipt_mutation();");
        migrationBuilder.DropTable("AdvertiserRegistrationReceipts");
        migrationBuilder.DropTable("AdvertiserAccountMemberships");
    }
}
