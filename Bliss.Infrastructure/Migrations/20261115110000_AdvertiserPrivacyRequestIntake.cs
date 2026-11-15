using Bliss.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bliss.Infrastructure.Migrations;

[DbContext(typeof(BlissDbContext))]
[Migration("20261115110000_AdvertiserPrivacyRequestIntake")]
public sealed class AdvertiserPrivacyRequestIntake : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "AdvertiserPrivacyRequests",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                RequestKey = table.Column<Guid>(type: "uuid", nullable: false),
                Subject = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                AdvertiserId = table.Column<Guid>(type: "uuid", nullable: false),
                RequestType = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                Status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false, defaultValue: "RECEIVED"),
                CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
            }, constraints: table =>
            {
                table.PrimaryKey("PK_AdvertiserPrivacyRequests", x => x.Id);
                table.CheckConstraint("CK_AdvertiserPrivacyRequests_Type",
                    "\"RequestType\" IN ('ACCESS', 'CORRECTION', 'ERASURE', 'MARKETING_WITHDRAWAL', 'OBJECTION', 'ACCOUNT_CLOSURE')");
                table.CheckConstraint("CK_AdvertiserPrivacyRequests_Status",
                    "\"Status\" IN ('RECEIVED', 'VERIFYING', 'RESOLVED', 'DECLINED')");
                table.ForeignKey("FK_AdvertiserPrivacyRequests_Advertisers_AdvertiserId",
                    x => x.AdvertiserId, "Advertisers", "Id", onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex("IX_AdvertiserPrivacyRequests_RequestKey",
            "AdvertiserPrivacyRequests", "RequestKey", unique: true);
        migrationBuilder.CreateIndex("IX_AdvertiserPrivacyRequests_Subject_CreatedAt",
            "AdvertiserPrivacyRequests", new[] { "Subject", "CreatedAt" });

        // The existing server-side EF database user owns case access.
        // Neither public REST role nor Supabase client role may browse case records.
        migrationBuilder.Sql("REVOKE ALL ON TABLE \"AdvertiserPrivacyRequests\" FROM PUBLIC;");
        migrationBuilder.Sql("DO $$ BEGIN IF EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'anon') THEN EXECUTE 'REVOKE ALL ON TABLE \"AdvertiserPrivacyRequests\" FROM anon'; END IF; IF EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'authenticated') THEN EXECUTE 'REVOKE ALL ON TABLE \"AdvertiserPrivacyRequests\" FROM authenticated'; END IF; END $$;");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable("AdvertiserPrivacyRequests");
    }
}
