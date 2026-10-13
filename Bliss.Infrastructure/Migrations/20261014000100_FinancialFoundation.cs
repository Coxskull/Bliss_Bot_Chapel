using Bliss.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
namespace Bliss.Infrastructure.Migrations;
[DbContext(typeof(BlissDbContext))]
[Migration("20261014000100_FinancialFoundation")]
public sealed class FinancialFoundation:Migration
{
 protected override void Up(MigrationBuilder migrationBuilder)
 {
  using var stream=typeof(FinancialFoundation).Assembly.GetManifestResourceStream("Bliss.FinancialFoundationV1.sql") ?? throw new InvalidOperationException("Financial migration resource missing.");using var reader=new StreamReader(stream);migrationBuilder.Sql(reader.ReadToEnd());
 }
 protected override void Down(MigrationBuilder migrationBuilder)=>throw new InvalidOperationException("Financial audit schema cannot be dropped automatically; reviewed data-preserving remediation is required.");
}
