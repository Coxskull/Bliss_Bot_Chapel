using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bliss.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class SubscriptionLedger : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "FactoryBudgetAudits",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Scope = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    CeilingAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    CeilingCurrency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    RecordedSpend = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    Reason = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    Notice = table.Column<string>(type: "character varying(800)", maxLength: 800, nullable: false),
                    RecordedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FactoryBudgetAudits", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "FactoryBudgetStates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Scope = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    CeilingAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    CeilingCurrency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: true),
                    RecordedSpend = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    Notice = table.Column<string>(type: "character varying(800)", maxLength: 800, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FactoryBudgetStates", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SubscriptionLedgerAudits",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Action = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    ServiceKey = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Classification = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    MonthlyAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: true),
                    EstimatedAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    EstimatedCurrency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: true),
                    EngineeringContract = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Provider = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    Capability = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    UsageCharges = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    Alternatives = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    BuildAlternative = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    WhyAlphaIsInsufficient = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    RequiredDate = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    RequiredOrOptional = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                    Reason = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    Notice = table.Column<string>(type: "character varying(800)", maxLength: 800, nullable: false),
                    RecordedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SubscriptionLedgerAudits", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SubscriptionRegisterRows",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ServiceKey = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    DisplayName = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Provider = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Capability = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    Classification = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    AccountOwner = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Plan = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    MonthlyAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: true),
                    Notice = table.Column<string>(type: "character varying(800)", maxLength: 800, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SubscriptionRegisterRows", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FactoryBudgetAudits_RecordedAt",
                table: "FactoryBudgetAudits",
                column: "RecordedAt");

            migrationBuilder.CreateIndex(
                name: "IX_FactoryBudgetStates_Scope",
                table: "FactoryBudgetStates",
                column: "Scope",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SubscriptionLedgerAudits_RecordedAt",
                table: "SubscriptionLedgerAudits",
                column: "RecordedAt");

            migrationBuilder.CreateIndex(
                name: "IX_SubscriptionRegisterRows_ServiceKey",
                table: "SubscriptionRegisterRows",
                column: "ServiceKey",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FactoryBudgetAudits");

            migrationBuilder.DropTable(
                name: "FactoryBudgetStates");

            migrationBuilder.DropTable(
                name: "SubscriptionLedgerAudits");

            migrationBuilder.DropTable(
                name: "SubscriptionRegisterRows");
        }
    }
}
