using Bliss.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
namespace Bliss.Infrastructure.Migrations;
[DbContext(typeof(BlissDbContext))]
[Migration("20261014000400_FinancialMockReconciliation")]
public sealed partial class FinancialMockReconciliation : Migration
{
 protected override void Up(MigrationBuilder migrationBuilder)
 {
  using var stream=typeof(FinancialMockReconciliation).Assembly.GetManifestResourceStream("Bliss.FinancialMockReconciliationV1.sql") ?? throw new InvalidOperationException("Mock reconciliation resource missing.");
  using var reader=new StreamReader(stream);migrationBuilder.Sql(reader.ReadToEnd());
 }
 protected override void Down(MigrationBuilder migrationBuilder)=>throw new InvalidOperationException("Mock reconciliation audit evidence cannot be dropped automatically; reviewed remediation is required.");
}
