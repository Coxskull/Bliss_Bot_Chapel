using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bliss.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class CampaignAuthorityLineage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AuthorityStatus",
                table: "Creators",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "UNVERIFIED");

            migrationBuilder.AddColumn<string>(
                name: "AuthorityStatus",
                table: "Campaigns",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "UNVERIFIED");

            migrationBuilder.AddColumn<string>(
                name: "AuthorityStatus",
                table: "AdvertiserOpportunities",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "UNVERIFIED");

            migrationBuilder.Sql("""
                UPDATE "Creators"
                SET "AuthorityStatus" = 'SYNTHETIC_FIXTURE'
                WHERE "Id" IN (
                    '11111111-1111-1111-1111-111111111111',
                    '11111111-1111-1111-1111-111111111112',
                    '11111111-1111-1111-1111-111111111113');

                UPDATE "AdvertiserOpportunities"
                SET "AuthorityStatus" = 'SYNTHETIC_FIXTURE'
                WHERE "Id" IN (
                    '55555555-5555-5555-5555-555555555551',
                    '55555555-5555-5555-5555-555555555552',
                    '55555555-5555-5555-5555-555555555553',
                    '55555555-5555-5555-5555-555555555554',
                    '55555555-5555-5555-5555-555555555555');

                UPDATE "Campaigns"
                SET "AuthorityStatus" = 'SYNTHETIC_FIXTURE'
                WHERE "Id" IN (
                    'dddddddd-dddd-dddd-dddd-ddddddddddd1',
                    'dddddddd-dddd-dddd-dddd-ddddddddddd4');
                """);

            migrationBuilder.CreateIndex(
                name: "IX_Creators_AuthorityStatus",
                table: "Creators",
                column: "AuthorityStatus");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Creators_AuthorityStatus",
                table: "Creators",
                sql: "\"AuthorityStatus\" IN ('UNVERIFIED', 'VERIFIED', 'SYNTHETIC_FIXTURE')");

            migrationBuilder.CreateIndex(
                name: "IX_Campaigns_AuthorityStatus",
                table: "Campaigns",
                column: "AuthorityStatus");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Campaigns_AuthorityStatus",
                table: "Campaigns",
                sql: "\"AuthorityStatus\" IN ('UNVERIFIED', 'VERIFIED', 'SYNTHETIC_FIXTURE')");

            migrationBuilder.CreateIndex(
                name: "IX_AdvertiserOpportunities_AuthorityStatus",
                table: "AdvertiserOpportunities",
                column: "AuthorityStatus");

            migrationBuilder.AddCheckConstraint(
                name: "CK_AdvertiserOpportunities_AuthorityStatus",
                table: "AdvertiserOpportunities",
                sql: "\"AuthorityStatus\" IN ('UNVERIFIED', 'VERIFIED', 'SYNTHETIC_FIXTURE')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Creators_AuthorityStatus",
                table: "Creators");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Creators_AuthorityStatus",
                table: "Creators");

            migrationBuilder.DropIndex(
                name: "IX_Campaigns_AuthorityStatus",
                table: "Campaigns");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Campaigns_AuthorityStatus",
                table: "Campaigns");

            migrationBuilder.DropIndex(
                name: "IX_AdvertiserOpportunities_AuthorityStatus",
                table: "AdvertiserOpportunities");

            migrationBuilder.DropCheckConstraint(
                name: "CK_AdvertiserOpportunities_AuthorityStatus",
                table: "AdvertiserOpportunities");

            migrationBuilder.DropColumn(
                name: "AuthorityStatus",
                table: "Creators");

            migrationBuilder.DropColumn(
                name: "AuthorityStatus",
                table: "Campaigns");

            migrationBuilder.DropColumn(
                name: "AuthorityStatus",
                table: "AdvertiserOpportunities");
        }
    }
}
