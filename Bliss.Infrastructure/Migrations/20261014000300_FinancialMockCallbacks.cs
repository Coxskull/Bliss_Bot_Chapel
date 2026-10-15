using Bliss.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
namespace Bliss.Infrastructure.Migrations;
[DbContext(typeof(BlissDbContext))]
[Migration("20261014000300_FinancialMockCallbacks")]
public sealed partial class FinancialMockCallbacks : Migration
{
 protected override void Up(MigrationBuilder migrationBuilder)
 {
  using var stream=typeof(FinancialMockCallbacks).Assembly.GetManifestResourceStream("Bliss.FinancialMockCallbacksV1.sql") ?? throw new InvalidOperationException("Mock callback migration resource missing.");
  using var reader=new StreamReader(stream);migrationBuilder.Sql(reader.ReadToEnd());
 }
 protected override void Down(MigrationBuilder migrationBuilder)=>throw new InvalidOperationException("Mock callback audit evidence cannot be dropped automatically; reviewed remediation is required.");
}
