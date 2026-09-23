using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bliss.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class EconomicsPhase6Compensation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CompensationRuleVersions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Version = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    DocumentJson = table.Column<string>(type: "jsonb", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    EffectiveAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CompensationRuleVersions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CompensationIllustrations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    QuoteId = table.Column<Guid>(type: "uuid", nullable: false),
                    QuoteVersionId = table.Column<Guid>(type: "uuid", nullable: false),
                    QuoteOutcomeId = table.Column<Guid>(type: "uuid", nullable: false),
                    CompensationRuleVersionId = table.Column<Guid>(type: "uuid", nullable: false),
                    GrossAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    CurrencyCode = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    InputSnapshotJson = table.Column<string>(type: "jsonb", nullable: false),
                    SourceSystem = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    IdempotencyKey = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CompensationIllustrations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CompensationIllustrations_CompensationRuleVersions_Compensa~",
                        column: x => x.CompensationRuleVersionId,
                        principalTable: "CompensationRuleVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CompensationIllustrations_QuoteOutcomes_QuoteOutcomeId",
                        column: x => x.QuoteOutcomeId,
                        principalTable: "QuoteOutcomes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CompensationIllustrations_QuoteVersions_QuoteVersionId",
                        column: x => x.QuoteVersionId,
                        principalTable: "QuoteVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CompensationIllustrations_Quotes_QuoteId",
                        column: x => x.QuoteId,
                        principalTable: "Quotes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CompensationRuleAllocations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CompensationRuleVersionId = table.Column<Guid>(type: "uuid", nullable: false),
                    ParticipantRole = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ParticipantLabel = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Percentage = table.Column<decimal>(type: "numeric(9,6)", precision: 9, scale: 6, nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CompensationRuleAllocations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CompensationRuleAllocations_CompensationRuleVersions_Compen~",
                        column: x => x.CompensationRuleVersionId,
                        principalTable: "CompensationRuleVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CompensationIllustrationLines",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CompensationIllustrationId = table.Column<Guid>(type: "uuid", nullable: false),
                    CompensationRuleAllocationId = table.Column<Guid>(type: "uuid", nullable: false),
                    ParticipantRole = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ParticipantLabel = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Percentage = table.Column<decimal>(type: "numeric(9,6)", precision: 9, scale: 6, nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CompensationIllustrationLines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CompensationIllustrationLines_CompensationIllustrations_Com~",
                        column: x => x.CompensationIllustrationId,
                        principalTable: "CompensationIllustrations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CompensationIllustrationLines_CompensationRuleAllocations_C~",
                        column: x => x.CompensationRuleAllocationId,
                        principalTable: "CompensationRuleAllocations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CompensationIllustrationLines_CompensationIllustrationId_So~",
                table: "CompensationIllustrationLines",
                columns: new[] { "CompensationIllustrationId", "SortOrder" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CompensationIllustrationLines_CompensationRuleAllocationId",
                table: "CompensationIllustrationLines",
                column: "CompensationRuleAllocationId");

            migrationBuilder.CreateIndex(
                name: "IX_CompensationIllustrations_CompensationRuleVersionId",
                table: "CompensationIllustrations",
                column: "CompensationRuleVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_CompensationIllustrations_CreatedAt",
                table: "CompensationIllustrations",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_CompensationIllustrations_QuoteId",
                table: "CompensationIllustrations",
                column: "QuoteId");

            migrationBuilder.CreateIndex(
                name: "IX_CompensationIllustrations_QuoteOutcomeId",
                table: "CompensationIllustrations",
                column: "QuoteOutcomeId");

            migrationBuilder.CreateIndex(
                name: "IX_CompensationIllustrations_QuoteVersionId",
                table: "CompensationIllustrations",
                column: "QuoteVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_CompensationIllustrations_SourceSystem_IdempotencyKey",
                table: "CompensationIllustrations",
                columns: new[] { "SourceSystem", "IdempotencyKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CompensationRuleAllocations_CompensationRuleVersionId_SortO~",
                table: "CompensationRuleAllocations",
                columns: new[] { "CompensationRuleVersionId", "SortOrder" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CompensationRuleVersions_EffectiveAt",
                table: "CompensationRuleVersions",
                column: "EffectiveAt");

            migrationBuilder.CreateIndex(
                name: "IX_CompensationRuleVersions_Version",
                table: "CompensationRuleVersions",
                column: "Version",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CompensationIllustrationLines");

            migrationBuilder.DropTable(
                name: "CompensationIllustrations");

            migrationBuilder.DropTable(
                name: "CompensationRuleAllocations");

            migrationBuilder.DropTable(
                name: "CompensationRuleVersions");
        }
    }
}
