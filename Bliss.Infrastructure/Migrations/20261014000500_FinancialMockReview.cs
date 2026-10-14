using Bliss.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
namespace Bliss.Infrastructure.Migrations;
[DbContext(typeof(BlissDbContext))]
[Migration("20261014000500_FinancialMockReview")]
public sealed partial class FinancialMockReview:Migration
{
 protected override void Up(MigrationBuilder migrationBuilder)
 {
  using var stream=typeof(FinancialMockReview).Assembly.GetManifestResourceStream("Bliss.FinancialMockReviewV1.sql")??throw new InvalidOperationException("Mock review migration resource missing.");
  using var reader=new StreamReader(stream);migrationBuilder.Sql(reader.ReadToEnd());
 }
 protected override void Down(MigrationBuilder migrationBuilder)=>throw new InvalidOperationException("Mock review evidence cannot be dropped automatically; reviewed remediation is required.");
}
