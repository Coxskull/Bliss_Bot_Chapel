using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bliss.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class WeddingPlannerPhase1Foundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "WeddingPlannerWorkspaces",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AdvertiserId = table.Column<Guid>(type: "uuid", nullable: false),
                    IsPrimary = table.Column<bool>(type: "boolean", nullable: false),
                    Status = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    SourceSystem = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    IdempotencyKey = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WeddingPlannerWorkspaces", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WeddingPlannerWorkspaces_Advertisers_AdvertiserId",
                        column: x => x.AdvertiserId,
                        principalTable: "Advertisers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "WeddingPlannerPlanningSessions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkspaceId = table.Column<Guid>(type: "uuid", nullable: false),
                    AdvertiserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    SourceSystem = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    IdempotencyKey = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WeddingPlannerPlanningSessions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WeddingPlannerPlanningSessions_Advertisers_AdvertiserId",
                        column: x => x.AdvertiserId,
                        principalTable: "Advertisers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WeddingPlannerPlanningSessions_WeddingPlannerWorkspaces_Wor~",
                        column: x => x.WorkspaceId,
                        principalTable: "WeddingPlannerWorkspaces",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "WeddingPlannerConversationMessages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SessionId = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkspaceId = table.Column<Guid>(type: "uuid", nullable: false),
                    AdvertiserId = table.Column<Guid>(type: "uuid", nullable: false),
                    SequenceNumber = table.Column<int>(type: "integer", nullable: false),
                    ActorType = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ActorLabel = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Body = table.Column<string>(type: "character varying(8000)", maxLength: 8000, nullable: false),
                    SourceSystem = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    IdempotencyKey = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WeddingPlannerConversationMessages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WeddingPlannerConversationMessages_Advertisers_AdvertiserId",
                        column: x => x.AdvertiserId,
                        principalTable: "Advertisers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WeddingPlannerConversationMessages_WeddingPlannerPlanningSe~",
                        column: x => x.SessionId,
                        principalTable: "WeddingPlannerPlanningSessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WeddingPlannerConversationMessages_WeddingPlannerWorkspaces~",
                        column: x => x.WorkspaceId,
                        principalTable: "WeddingPlannerWorkspaces",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "WeddingPlannerAuditEvents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AdvertiserId = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkspaceId = table.Column<Guid>(type: "uuid", nullable: true),
                    SessionId = table.Column<Guid>(type: "uuid", nullable: true),
                    MessageId = table.Column<Guid>(type: "uuid", nullable: true),
                    Action = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ActorType = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ActorLabel = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Outcome = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    RequestId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    Detail = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    OccurredAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WeddingPlannerAuditEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WeddingPlannerAuditEvents_Advertisers_AdvertiserId",
                        column: x => x.AdvertiserId,
                        principalTable: "Advertisers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WeddingPlannerAuditEvents_WeddingPlannerConversationMessage~",
                        column: x => x.MessageId,
                        principalTable: "WeddingPlannerConversationMessages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WeddingPlannerAuditEvents_WeddingPlannerPlanningSessions_Se~",
                        column: x => x.SessionId,
                        principalTable: "WeddingPlannerPlanningSessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WeddingPlannerAuditEvents_WeddingPlannerWorkspaces_Workspac~",
                        column: x => x.WorkspaceId,
                        principalTable: "WeddingPlannerWorkspaces",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_WeddingPlannerAuditEvents_AdvertiserId",
                table: "WeddingPlannerAuditEvents",
                column: "AdvertiserId");

            migrationBuilder.CreateIndex(
                name: "IX_WeddingPlannerAuditEvents_MessageId",
                table: "WeddingPlannerAuditEvents",
                column: "MessageId");

            migrationBuilder.CreateIndex(
                name: "IX_WeddingPlannerAuditEvents_OccurredAt",
                table: "WeddingPlannerAuditEvents",
                column: "OccurredAt");

            migrationBuilder.CreateIndex(
                name: "IX_WeddingPlannerAuditEvents_SessionId",
                table: "WeddingPlannerAuditEvents",
                column: "SessionId");

            migrationBuilder.CreateIndex(
                name: "IX_WeddingPlannerAuditEvents_WorkspaceId",
                table: "WeddingPlannerAuditEvents",
                column: "WorkspaceId");

            migrationBuilder.CreateIndex(
                name: "IX_WeddingPlannerConversationMessages_AdvertiserId",
                table: "WeddingPlannerConversationMessages",
                column: "AdvertiserId");

            migrationBuilder.CreateIndex(
                name: "IX_WeddingPlannerConversationMessages_CreatedAt",
                table: "WeddingPlannerConversationMessages",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_WeddingPlannerConversationMessages_SessionId",
                table: "WeddingPlannerConversationMessages",
                column: "SessionId");

            migrationBuilder.CreateIndex(
                name: "IX_WeddingPlannerConversationMessages_SessionId_SequenceNumber",
                table: "WeddingPlannerConversationMessages",
                columns: new[] { "SessionId", "SequenceNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WeddingPlannerConversationMessages_SourceSystem_Idempotency~",
                table: "WeddingPlannerConversationMessages",
                columns: new[] { "SourceSystem", "IdempotencyKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WeddingPlannerConversationMessages_WorkspaceId",
                table: "WeddingPlannerConversationMessages",
                column: "WorkspaceId");

            migrationBuilder.CreateIndex(
                name: "IX_WeddingPlannerPlanningSessions_AdvertiserId",
                table: "WeddingPlannerPlanningSessions",
                column: "AdvertiserId");

            migrationBuilder.CreateIndex(
                name: "IX_WeddingPlannerPlanningSessions_CreatedAt",
                table: "WeddingPlannerPlanningSessions",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_WeddingPlannerPlanningSessions_SourceSystem_IdempotencyKey",
                table: "WeddingPlannerPlanningSessions",
                columns: new[] { "SourceSystem", "IdempotencyKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WeddingPlannerPlanningSessions_WorkspaceId",
                table: "WeddingPlannerPlanningSessions",
                column: "WorkspaceId");

            migrationBuilder.CreateIndex(
                name: "IX_WeddingPlannerWorkspaces_AdvertiserId",
                table: "WeddingPlannerWorkspaces",
                column: "AdvertiserId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WeddingPlannerWorkspaces_CreatedAt",
                table: "WeddingPlannerWorkspaces",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_WeddingPlannerWorkspaces_SourceSystem_IdempotencyKey",
                table: "WeddingPlannerWorkspaces",
                columns: new[] { "SourceSystem", "IdempotencyKey" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "WeddingPlannerAuditEvents");

            migrationBuilder.DropTable(
                name: "WeddingPlannerConversationMessages");

            migrationBuilder.DropTable(
                name: "WeddingPlannerPlanningSessions");

            migrationBuilder.DropTable(
                name: "WeddingPlannerWorkspaces");
        }
    }
}
