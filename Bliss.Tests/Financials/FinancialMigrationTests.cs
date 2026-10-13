using Bliss.Domain.Entities;
using Bliss.Domain.Financials;
using Bliss.Infrastructure.Financials;
using System.Security.Claims;
using Bliss.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;
namespace Bliss.Tests.Financials;
public sealed class FinancialMigrationTests
{
 [FinancialPostgresFact]public async Task Upgrade_preserves_existing_quote_replays_and_refuses_destructive_down()
 {
  var connection=Environment.GetEnvironmentVariable("BLISS_FINANCIAL_TEST_POSTGRES")!;var fixture=new NpgsqlConnectionStringBuilder(connection);Assert.Equal("127.0.0.1",fixture.Host);Assert.Equal("financial_test",fixture.Database);await using var admin=new NpgsqlConnection(connection);await admin.OpenAsync();var name="financial_migration_"+Guid.NewGuid().ToString("N");await using(var create=new NpgsqlCommand("CREATE DATABASE \""+name+"\"",admin))await create.ExecuteNonQueryAsync();
  try
  {
   fixture.Database=name;await using var db=new BlissDbContext(new DbContextOptionsBuilder<BlissDbContext>().UseNpgsql(fixture.ConnectionString).Options);var migrations=db.Database.GetMigrations().ToArray();Assert.Equal("20261014000200_FinancialDispatch",migrations[^1]);await db.GetService<IMigrator>().MigrateAsync("20261014000100_FinancialFoundation");var quote=new Quote{Id=Guid.NewGuid(),Status="ACCEPTED",CurrencyCode="USD",CurrentVersionNumber=1,RequestedBy="TEST",SourceSystem="TEST",IdempotencyKey=Guid.NewGuid().ToString("N"),CreatedAt=DateTime.UtcNow,UpdatedAt=DateTime.UtcNow};db.Add(quote);await db.SaveChangesAsync();
   await using var financialData=NpgsqlDataSource.Create(fixture.ConnectionString);var financialStore=new PaymentOperationStore(financialData);var subject="migration-"+Guid.NewGuid();var user=new ClaimsPrincipal(new ClaimsIdentity([new Claim("sub",subject)],"fixture"));var frozen=new AuthorizedQuoteSnapshot(Guid.NewGuid(),Guid.NewGuid(),new(Guid.NewGuid(),FinancialModule.CreatorAcademy,Guid.NewGuid()),subject,Guid.NewGuid(),1250,"USD");await financialStore.FreezeQuote(frozen);var operation=await financialStore.Prepare(user,frozen.Id,"preserved");
   await db.Database.MigrateAsync();await db.Database.MigrateAsync();Assert.Empty(await db.Database.GetPendingMigrationsAsync());Assert.Equal(operation,await financialStore.Prepare(user,frozen.Id,"preserved"));var dispatch=new PaymentDispatchStore(financialData);var claim=await dispatch.Claim(user,operation.Id);Assert.Equal(operation,claim.Operation);Assert.True(await dispatch.Complete(claim,new("pi_upgrade_"+operation.Id.ToString("N"),"requires_payment_method",1250,"USD")));Assert.Equal("BOUND",(await dispatch.Claim(user,operation.Id)).State);Assert.Equal("ACCEPTED",(await db.Quotes.AsNoTracking().SingleAsync(x=>x.Id==quote.Id)).Status);
   await Assert.ThrowsAsync<InvalidOperationException>(()=>db.GetService<IMigrator>().MigrateAsync(migrations[^2]));Assert.Equal("ACCEPTED",(await db.Quotes.AsNoTracking().SingleAsync(x=>x.Id==quote.Id)).Status);
  }
  finally{await using var drop=new NpgsqlCommand("DROP DATABASE \""+name+"\" WITH (FORCE)",admin);await drop.ExecuteNonQueryAsync();}
 }
}
