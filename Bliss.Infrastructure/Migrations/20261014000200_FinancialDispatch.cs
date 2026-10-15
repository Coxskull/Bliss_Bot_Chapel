using Bliss.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
namespace Bliss.Infrastructure.Migrations;
[DbContext(typeof(BlissDbContext))]
[Migration("20261014000200_FinancialDispatch")]
public sealed partial class FinancialDispatch : Migration
{
 protected override void Up(MigrationBuilder migrationBuilder)
 {
  using var stream=typeof(FinancialDispatch).Assembly.GetManifestResourceStream("Bliss.FinancialDispatchV1.sql") ?? throw new InvalidOperationException("Dispatch migration resource missing.");
  using var reader=new StreamReader(stream);migrationBuilder.Sql(reader.ReadToEnd());
 }
 protected override void Down(MigrationBuilder migrationBuilder)=>throw new InvalidOperationException("Dispatch evidence cannot be dropped automatically; reviewed data-preserving remediation is required.");
}
