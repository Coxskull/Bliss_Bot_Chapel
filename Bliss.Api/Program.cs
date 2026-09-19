using System.Text.Json.Serialization;
using Bliss.Infrastructure.DependencyInjection;
using Bliss.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
        options.JsonSerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;
    });

builder.Services.AddDbContext<BlissDbContext>(options =>
{
    options.UseNpgsql(
        builder.Configuration.GetConnectionString("DefaultConnection"));
});

builder.Services.AddBlissInfrastructure(builder.Configuration);
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new()
    {
        Title = "Bliss Bot Chapel API",
        Version = "v1",
        Description = "Phase 1 foundation read-only visibility endpoints."
    });
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();

    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<BlissDbContext>();
    var connectionString = app.Configuration.GetConnectionString("DefaultConnection");
    if (!string.IsNullOrWhiteSpace(connectionString) && db.Database.IsRelational())
    {
        try
        {
            if (db.Database.CanConnect())
            {
                var seeder = scope.ServiceProvider.GetRequiredService<Phase1DataSeeder>();
                await seeder.SeedAsync();
            }
        }
        catch (Exception ex)
        {
            app.Logger.LogWarning(ex, "Phase 1 seed skipped because the database was not reachable.");
        }
    }
}

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();
app.Run();

public partial class Program;
