using System.Net;
using System.Net.Http.Json;
using Bliss.Api.Contracts;
using Bliss.Api.Controllers;
using Bliss.Domain.Entities;
using Bliss.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Bliss.Tests.Api;

public sealed class WeddingPlannerBrandDnaTests : IClassFixture<WeddingPlannerOidcFactory>
{
    private readonly WeddingPlannerOidcFactory factory;
    public WeddingPlannerBrandDnaTests(WeddingPlannerOidcFactory factory) => this.factory = factory;
    private sealed record WriteResult(WeddingPlannerBrandDna Revision, bool IsReplay);
    private static BrandDnaFields Complete => new("TEST Bakery", "Bakery", "Taipei, Taiwan", "English", "Gold", "", "Grow local awareness", "No unsupported claims");

    private async Task<(HttpClient Client, Guid Workspace)> Owner()
    {
        var advertiserId = Guid.NewGuid();
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<BlissDbContext>();
            db.Advertisers.Add(new Advertiser { Id = advertiserId, Name = "TEST Intake", CreatedAt = DateTime.UtcNow });
            await db.SaveChangesAsync();
        }
        var client = factory.CreateSecureClient();
        client.DefaultRequestHeaders.Add("X-Test-Name", "Intake Owner");
        client.DefaultRequestHeaders.Add("X-Test-Roles", "bliss.advertiser");
        client.DefaultRequestHeaders.Add("X-Test-Advertiser-Id", advertiserId.ToString());
        var session = await client.GetFromJsonAsync<SessionResponse>("/api/auth/session");
        client.DefaultRequestHeaders.Add("X-CSRF-TOKEN", session!.CsrfToken);
        var response = await client.PostAsJsonAsync("/api/wedding-planner/workspaces",
            new OpenWeddingPlannerWorkspaceRequest(advertiserId, "BRAND_TEST", Guid.NewGuid().ToString()));
        response.EnsureSuccessStatusCode();
        var workspace = await response.Content.ReadFromJsonAsync<WeddingPlannerWorkspaceDto>();
        return (client, workspace!.WorkspaceId);
    }
    private static string Path(Guid workspace) => $"/api/wedding-planner/workspaces/{workspace}/brand-dna";

    [Fact]
    public async Task Draft_resumes_confirmation_is_explicit_and_history_stays_intact()
    {
        var (client, workspace) = await Owner();
        var path = Path(workspace);
        var partial = new SaveBrandDnaRequest(Complete with { Goals = "" }, 0, "partial");
        var saved = await client.PostAsJsonAsync(path, partial);
        Assert.Equal(HttpStatusCode.Created, saved.StatusCode);
        var incomplete = await client.PostAsJsonAsync(path + "/confirm", new ConfirmBrandDnaRequest(1, "incomplete"));
        Assert.Equal(HttpStatusCode.BadRequest, incomplete.StatusCode);
        var complete = await client.PostAsJsonAsync(path, new SaveBrandDnaRequest(Complete, 1, "complete"));
        Assert.Equal(HttpStatusCode.Created, complete.StatusCode);
        var confirmed = await client.PostAsJsonAsync(path + "/confirm", new ConfirmBrandDnaRequest(2, "confirm"));
        Assert.Equal(HttpStatusCode.Created, confirmed.StatusCode);
        var confirmedBody = await confirmed.Content.ReadFromJsonAsync<WriteResult>();
        Assert.Equal("CONFIRMED", confirmedBody!.Revision.Status);
        Assert.Equal("Intake Owner", confirmedBody.Revision.ActorLabel);
        var confirmationReplay = await client.PostAsJsonAsync(path + "/confirm", new ConfirmBrandDnaRequest(2, "confirm"));
        Assert.Equal(HttpStatusCode.OK, confirmationReplay.StatusCode);
        Assert.True((await confirmationReplay.Content.ReadFromJsonAsync<WriteResult>())!.IsReplay);
        var newDraft = await client.PostAsJsonAsync(path, new SaveBrandDnaRequest(Complete with { Colors = "Burgundy" }, 3, "revised"));
        Assert.Equal(HttpStatusCode.Created, newDraft.StatusCode);
        var history = await client.GetFromJsonAsync<List<WeddingPlannerBrandDna>>(path);
        Assert.NotNull(history);
        Assert.Equal(new[] { 1, 2, 3, 4 }, history.Select(x => x.Version));
        Assert.Equal("", history[0].Goals);
        Assert.Equal("CONFIRMED", history[2].Status);
        Assert.Equal("Gold", history[2].Colors);
        Assert.Equal("DRAFT", history[3].Status);
        var audit = await client.GetFromJsonAsync<List<WeddingPlannerAuditEventDto>>($"/api/wedding-planner/workspaces/{workspace}/audit");
        Assert.NotNull(audit);
        Assert.Single(audit, x => x.Action == "BRAND_DNA_CONFIRMED");
        Assert.Equal(3, audit.Count(x => x.Action == "BRAND_DNA_DRAFT_SAVED"));
    }

    [Fact]
    public async Task Retries_and_stale_writes_do_not_append_or_confirm_wrong_direction()
    {
        var (client, workspace) = await Owner();
        var path = Path(workspace);
        var request = new SaveBrandDnaRequest(Complete, 0, "save");
        Assert.Equal(HttpStatusCode.Created, (await client.PostAsJsonAsync(path, request)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync(path, request)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync(path, request with { Fields = Complete with { BrandName = "Different" } })).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync(path, request with { IdempotencyKey = "stale" })).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync(path + "/confirm", new ConfirmBrandDnaRequest(0, "stale-confirm"))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync(path, new SaveBrandDnaRequest(Complete with { BrandName = new string('x', 201) }, 1, "oversize"))).StatusCode);
        Assert.Single((await client.GetFromJsonAsync<List<WeddingPlannerBrandDna>>(path))!);
        var audit = await client.GetFromJsonAsync<List<WeddingPlannerAuditEventDto>>($"/api/wedding-planner/workspaces/{workspace}/audit");
        Assert.NotNull(audit);
        Assert.Single(audit, x => x.Action == "BRAND_DNA_DRAFT_SAVED");
        Assert.DoesNotContain(audit, x => x.Action == "BRAND_DNA_CONFIRMED");
    }

    [Fact]
    public async Task Anonymous_cross_tenant_and_csrf_requests_are_refused()
    {
        var (owner, workspace) = await Owner();
        var (other, otherWorkspace) = await Owner();
        var path = Path(workspace);
        var draft = new SaveBrandDnaRequest(Complete, 0, "security");
        Assert.Equal(HttpStatusCode.Created, (await owner.PostAsJsonAsync(path, draft)).StatusCode);
        var anonymous = factory.CreateSecureClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync(path)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.PostAsJsonAsync(path, draft)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync(path)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await other.PostAsJsonAsync(path, draft)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await other.PostAsJsonAsync(path + "/confirm", new ConfirmBrandDnaRequest(1, "confirm"))).StatusCode);
        owner.DefaultRequestHeaders.Remove("X-CSRF-TOKEN");
        Assert.Equal(HttpStatusCode.BadRequest, (await owner.PostAsJsonAsync(path, draft)).StatusCode);
        Assert.Single((await owner.GetFromJsonAsync<List<WeddingPlannerBrandDna>>(path))!);
        Assert.Empty((await other.GetFromJsonAsync<List<WeddingPlannerBrandDna>>(Path(otherWorkspace)))!);
        var viewer = factory.CreateSecureClient();
        viewer.DefaultRequestHeaders.Add("X-Test-Name", "Read Only");
        viewer.DefaultRequestHeaders.Add("X-Test-Roles", "bliss.viewer");
        var viewerSession = await viewer.GetFromJsonAsync<SessionResponse>("/api/auth/session");
        viewer.DefaultRequestHeaders.Add("X-CSRF-TOKEN", viewerSession!.CsrfToken);
        Assert.Equal(HttpStatusCode.Forbidden, (await viewer.PostAsJsonAsync(path, draft)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await viewer.PostAsJsonAsync(path + "/confirm", new ConfirmBrandDnaRequest(1, "viewer"))).StatusCode);
    }

    [Theory]
    [InlineData("bliss.viewer")]
    [InlineData("bliss.reviewer")]
    [InlineData("bliss.unrecognized")]
    public async Task Tenant_bound_read_only_identity_cannot_mutate_planner_or_brand_dna(string role)
    {
        var (owner, workspace) = await Owner();
        var brandPath = Path(workspace);
        var saved = await owner.PostAsJsonAsync(brandPath,
            new SaveBrandDnaRequest(Complete, 0, "owner-approved-draft"));
        Assert.Equal(HttpStatusCode.Created, saved.StatusCode);

        var workspaceDetails = await owner.GetFromJsonAsync<WeddingPlannerWorkspaceDto>(
            $"/api/wedding-planner/workspaces/{workspace}");
        Assert.NotNull(workspaceDetails);
        var (_, foreignWorkspace) = await Owner();

        // A signed-in user can have an advertiser_id claim for tenant-scoped
        // reads while lacking the advertiser WRITE role. The claim alone must
        // never authorize a mutation, even when the CSRF token is valid.
        using var readOnly = factory.CreateSecureClient();
        readOnly.DefaultRequestHeaders.Add("X-Test-Name", $"TEST {role} bound reader");
        readOnly.DefaultRequestHeaders.Add("X-Test-Roles", role);
        readOnly.DefaultRequestHeaders.Add("X-Test-Advertiser-Id", workspaceDetails!.AdvertiserId.ToString());
        var session = await readOnly.GetFromJsonAsync<SessionResponse>("/api/auth/session");
        Assert.NotNull(session?.CsrfToken);
        readOnly.DefaultRequestHeaders.Add("X-CSRF-TOKEN", session!.CsrfToken);

        Assert.Equal(HttpStatusCode.OK, (await readOnly.GetAsync(brandPath)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await readOnly.GetAsync(Path(foreignWorkspace))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await readOnly.PostAsJsonAsync(brandPath,
                new SaveBrandDnaRequest(Complete with { Colors = "Unauthorized" }, 1, $"viewer-{role}"))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await readOnly.PostAsJsonAsync(brandPath + "/confirm",
                new ConfirmBrandDnaRequest(1, $"confirm-{role}"))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await readOnly.PostAsJsonAsync("/api/wedding-planner/workspaces",
                new OpenWeddingPlannerWorkspaceRequest(workspaceDetails.AdvertiserId, "READ_ONLY_TEST", $"open-{role}"))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await readOnly.PostAsJsonAsync($"/api/wedding-planner/workspaces/{workspace}/sessions",
                new CreateWeddingPlannerSessionRequest("READ_ONLY_TEST", $"session-{role}"))).StatusCode);

        var history = await owner.GetFromJsonAsync<List<WeddingPlannerBrandDna>>(brandPath);
        Assert.Single(history!);
        Assert.Equal("DRAFT", history![0].Status);
        Assert.Equal("Gold", history[0].Colors);
        var audit = await owner.GetFromJsonAsync<List<WeddingPlannerAuditEventDto>>(
            $"/api/wedding-planner/workspaces/{workspace}/audit");
        Assert.Single(audit!, row => row.Action == "BRAND_DNA_DRAFT_SAVED");
        Assert.DoesNotContain(audit!, row => row.Action == "BRAND_DNA_CONFIRMED");
    }

}
