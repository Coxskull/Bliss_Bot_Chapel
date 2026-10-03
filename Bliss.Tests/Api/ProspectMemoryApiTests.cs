using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Bliss.Api.Demonstrations;
using Bliss.Domain.Entities;
using Bliss.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Bliss.Tests.Api;

public sealed class ProspectMemoryApiTests : IClassFixture<BlissApiFactory>
{
    private readonly BlissApiFactory _factory;

    public ProspectMemoryApiTests(BlissApiFactory factory) => _factory = factory;

    [Fact]
    public async Task A_prospect_is_stored_in_the_bliss_database()
    {
        var client = _factory.CreateClient();
        var library = await client.GetFromJsonAsync<JsonElement>("/api/demonstrations/library");
        Assert.Equal("IN_MEMORY", library!.GetProperty("memory").GetString());

        var created = await client.PostAsJsonAsync("/api/demonstrations/discover", new
        {
            niche = "restaurant",
            market = "Panama City",
            businessName = "Mesa Norte",
            publicSourceUrl = "https://example.com/mesa-norte"
        });
        Assert.Equal(HttpStatusCode.OK, created.StatusCode);
        var preserved = await client.PostAsJsonAsync("/api/demonstrations/discover", new
        {
            niche = "restaurant",
            market = "Quito",
            businessName = "Puerto Azul",
            publicSourceUrl = "https://example.com/puerto-azul"
        });
        Assert.Equal(HttpStatusCode.OK, preserved.StatusCode);

        using var scope = _factory.Services.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<BlissDbContext>();
        Assert.Equal(1, database.GetType().Assembly.GetTypes().Count(type =>
            type.IsClass && !type.IsAbstract && typeof(DbContext).IsAssignableFrom(type)));
        Assert.Equal("ProspectMemories", database.Model.FindEntityType(typeof(ProspectMemory))!.GetTableName());

        var mesa = database.ProspectMemories.Single(item => item.Slug == "mesa-norte");
        var azul = database.ProspectMemories.Single(item => item.Slug == "puerto-azul");
        Assert.Equal("Mesa Norte", mesa.BusinessName);
        Assert.Equal("OPPORTUNITY_SCORED", mesa.ProspectState);
        Assert.Equal(100, mesa.OpportunityScore);
        Assert.False(mesa.Suppressed);
        Assert.Contains("Mesa Norte", mesa.PayloadJson);
        Assert.DoesNotContain("Puerto Azul", mesa.PayloadJson);
        Assert.Equal("PRESERVED", azul.ProspectState);
        Assert.Equal(75, azul.OpportunityScore);
        Assert.DoesNotContain("Mesa Norte", azul.PayloadJson);

        var store = _factory.Services.GetRequiredService<ProspectDemonstrationStore>();
        Assert.False(File.Exists(Path.Combine(store.Root, "library.json")));

        var suppressed = await client.PostAsJsonAsync("/api/demonstrations/puerto-azul/suppression", new
        {
            reason = "The business asked Alpha to stop"
        });
        Assert.Equal(HttpStatusCode.OK, suppressed.StatusCode);
        database.ChangeTracker.Clear();
        var withheld = database.ProspectMemories.Single(item => item.Slug == "puerto-azul");
        Assert.True(withheld.Suppressed);
        Assert.Contains("SUPPRESSED", withheld.PayloadJson);
        Assert.Equal("NOT_SENT", (await suppressed.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("delivery").GetString());
    }
}

public sealed class ProspectMemoryImportTests : IClassFixture<BlissApiFactory>
{
    private readonly BlissApiFactory _factory;

    public ProspectMemoryImportTests(BlissApiFactory factory) => _factory = factory;

    [Fact]
    public async Task An_existing_file_is_copied_once_and_then_the_database_is_read()
    {
        var store = _factory.Services.GetRequiredService<ProspectDemonstrationStore>();
        var document = new LibraryDocument
        {
            Demonstrations =
            [
                new DemonstrationRecord
                {
                    Slug = "puerto-azul",
                    BusinessName = "Puerto Azul",
                    Market = "Quito",
                    ProspectState = "PRESERVED",
                    OpportunityScore = 75,
                    PublicSourceUrl = "https://example.com/puerto-azul",
                    CreatedAt = DateTime.UtcNow
                }
            ]
        };
        File.WriteAllText(
            Path.Combine(store.Root, "library.json"),
            JsonSerializer.Serialize(document));

        var client = _factory.CreateClient();
        var page = await client.GetFromJsonAsync<JsonElement>("/api/demonstrations/puerto-azul");
        Assert.Equal("Puerto Azul", page!.GetProperty("businessName").GetString());
        Assert.Equal("NOT_SENT", page.GetProperty("delivery").GetString());

        document.Demonstrations[0].BusinessName = "Changed Name";
        File.WriteAllText(
            Path.Combine(store.Root, "library.json"),
            JsonSerializer.Serialize(document));
        var again = await client.GetFromJsonAsync<JsonElement>("/api/demonstrations/puerto-azul");
        Assert.Equal("Puerto Azul", again!.GetProperty("businessName").GetString());

        using var scope = _factory.Services.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<BlissDbContext>();
        var row = Assert.Single(database.ProspectMemories);
        Assert.Equal("puerto-azul", row.Slug);
        Assert.Equal("Puerto Azul", row.BusinessName);
    }
}
