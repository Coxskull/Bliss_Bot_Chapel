using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using Bliss.Api.Contracts;
using Bliss.Api.Controllers;
using Bliss.Api.Operations;
using Bliss.Domain.CreativeAcademy;
using Bliss.Domain.Entities;
using Bliss.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace Bliss.Tests.Api;

public sealed class AcademyProductionTests : IClassFixture<WeddingPlannerOidcFactory>
{
    public const string Pixel = "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNk+A8AAQUBAScY42YAAAAASUVORK5CYII=";
    private readonly WeddingPlannerOidcFactory factory;
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    public AcademyProductionTests(WeddingPlannerOidcFactory factory) => this.factory = factory;
    private sealed record BrandWrite(WeddingPlannerBrandDna Revision);
    private sealed record BriefWrite(AcademyProductionBrief Brief, bool IsReplay, bool CampaignReady, string Delivery);
    private sealed record CandidateWrite(AcademyProductionCandidate Candidate, bool IsReplay);
    private sealed record ReviewWrite(AcademyProductionReview Review, bool IsReplay, bool CampaignReady);
    private sealed record History(AcademyProductionBrief Brief, List<AcademyProductionCandidate> Candidates,
        List<AcademyProductionReview> Reviews, bool CampaignReady, string Delivery);
    private static string Path(Guid workspace) => $"/api/wedding-planner/workspaces/{workspace}/production";
    private async Task<HttpClient> Client(string roles, Guid? advertiser = null)
    {
        var client = factory.CreateSecureClient();
        client.DefaultRequestHeaders.Add("X-Test-Name", $"Production Tester {Guid.NewGuid():N}");
        client.DefaultRequestHeaders.Add("X-Test-Roles", roles);
        if (advertiser.HasValue) client.DefaultRequestHeaders.Add("X-Test-Advertiser-Id", advertiser.Value.ToString());
        var session = await client.GetFromJsonAsync<SessionResponse>("/api/auth/session");
        client.DefaultRequestHeaders.Add("X-CSRF-TOKEN", session!.CsrfToken);
        return client;
    }
    private async Task<(HttpClient Client, Guid Workspace, Guid Brand, Guid Advertiser)> Setup(bool confirm = true)
    {
        var advertiser = Guid.NewGuid();
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<BlissDbContext>();
            db.Advertisers.Add(new Advertiser { Id = advertiser, Name = "TEST Production", CreatedAt = DateTime.UtcNow });
            await db.SaveChangesAsync();
        }
        var client = await Client("bliss.advertiser", advertiser);
        var opened = await client.PostAsJsonAsync("/api/wedding-planner/workspaces", new OpenWeddingPlannerWorkspaceRequest(advertiser, "PRODUCTION_TEST", Guid.NewGuid().ToString()));
        opened.EnsureSuccessStatusCode();
        var workspace = (await opened.Content.ReadFromJsonAsync<WeddingPlannerWorkspaceDto>())!.WorkspaceId;
        var draft = await client.PostAsJsonAsync($"/api/wedding-planner/workspaces/{workspace}/brand-dna",
            new SaveBrandDnaRequest(new BrandDnaFields("TEST Independent Pharmacy", "pharmacy", "Taipei, Taiwan", "English", "Gold", "Montserrat", "Build neighborhood awareness", "No medical claims"), 0, "draft"));
        draft.EnsureSuccessStatusCode();
        var brand = (await draft.Content.ReadFromJsonAsync<BrandWrite>())!.Revision.Id;
        if (confirm)
        {
            var response = await client.PostAsJsonAsync($"/api/wedding-planner/workspaces/{workspace}/brand-dna/confirm", new ConfirmBrandDnaRequest(1, "confirm"));
            response.EnsureSuccessStatusCode(); brand = (await response.Content.ReadFromJsonAsync<BrandWrite>())!.Revision.Id;
        }
        return (client, workspace, brand, advertiser);
    }
    private async Task<AcademyProductionBrief> Brief(HttpClient client, Guid workspace, Guid brand)
    {
        var response = await client.PostAsJsonAsync(Path(workspace) + "/briefs", new CreateProductionBrief(brand, "pharmacy", "Your neighborhood wellness partner", "Visit our store", "brief"));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<BriefWrite>())!.Brief;
    }
    private static RecordProductionReview Review(int version, string decision = "PASS", string key = "review") =>
        new(version, decision, "PASS", "PASS", "PASS", "PASS", false, "Human checked brand, reference parity, originality and local context.", "Keep the lighting", decision == "REVISE" ? "Repair the headline" : "", key);

    [Fact]
    public async Task Confirmed_brand_brief_preserves_recipe_and_reference_provenance_on_resume()
    {
        var (client, workspace, brand, _) = await Setup();
        var brief = await Brief(client, workspace, brand);
        Assert.Equal(brand, brief.BrandDnaId);
        var snapshot = JsonSerializer.Deserialize<WeddingPlannerBrandDna>(brief.BrandSnapshotJson, Json)!;
        Assert.Equal(2, snapshot.Version); Assert.Equal("Gold", snapshot.Colors);
        var spec = JsonSerializer.Deserialize<ProductionBrief>(brief.ProductionBriefJson, Json)!;
        Assert.Equal("Montserrat", spec.FontFamily); Assert.Contains("No medical claims", spec.Requirements);
        Assert.Equal(ReferenceLibrary.QualityDnaVersion, spec.QualityDnaVersion);
        Assert.Single(spec.Retrieval.Selected);
        Assert.Equal(brief.ReferenceId, spec.Retrieval.Selected[0].ReferenceId);
        using (var scope = factory.Services.CreateScope())
        {
            var refs = scope.ServiceProvider.GetRequiredService<BlueprintPhaseService>();
            var file = refs.OpenReference(brief.ReferenceId)!;
            Assert.Equal(Convert.ToHexString(SHA256.HashData(await File.ReadAllBytesAsync(file.FullPath))).ToLowerInvariant(), brief.ReferenceSha256);
        }
        var duplicate = await client.PostAsJsonAsync(Path(workspace) + "/briefs", new CreateProductionBrief(brand, "pharmacy", brief.Headline, brief.Cta, "brief"));
        Assert.Equal(HttpStatusCode.OK, duplicate.StatusCode);
        Assert.True((await duplicate.Content.ReadFromJsonAsync<BriefWrite>())!.IsReplay);
        var changed = await client.PostAsJsonAsync(Path(workspace) + "/briefs", new CreateProductionBrief(brand, "pharmacy", "Changed", brief.Cta, "brief"));
        Assert.Equal(HttpStatusCode.Conflict, changed.StatusCode);
        var newDraft = await client.PostAsJsonAsync($"/api/wedding-planner/workspaces/{workspace}/brand-dna", new SaveBrandDnaRequest(
            new BrandDnaFields("New brand", "pharmacy", "Taipei", "English", "Burgundy", "", "New goal", ""), 2, "new-direction"));
        newDraft.EnsureSuccessStatusCode();
        var resumed = await client.GetFromJsonAsync<History>($"{Path(workspace)}/briefs/{brief.Id}");
        Assert.Equal(brief.ProductionBriefJson, resumed!.Brief.ProductionBriefJson);
        Assert.False(resumed.CampaignReady); Assert.Equal("NOT_SENT", resumed.Delivery);
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync(Path(workspace) + "/briefs", new CreateProductionBrief(brand, "pharmacy", "New", "Visit", "new"))).StatusCode);
    }

    [Fact]
    public async Task Draft_brand_and_unsupported_family_are_refused()
    {
        var (client, workspace, brand, _) = await Setup(false);
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync(Path(workspace) + "/briefs", new CreateProductionBrief(brand, "pharmacy", "Headline", "Visit", "draft"))).StatusCode);
        var confirmed = await client.PostAsJsonAsync($"/api/wedding-planner/workspaces/{workspace}/brand-dna/confirm", new ConfirmBrandDnaRequest(1, "confirm"));
        confirmed.EnsureSuccessStatusCode(); brand = (await confirmed.Content.ReadFromJsonAsync<BrandWrite>())!.Revision.Id;
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync(Path(workspace) + "/briefs", new CreateProductionBrief(brand, "unsupported-family", "Headline", "Visit", "missing"))).StatusCode);
        Assert.Empty((await client.GetFromJsonAsync<List<AcademyProductionBrief>>(Path(workspace) + "/briefs"))!);
    }

    [Fact]
    public async Task Candidates_reviews_and_revisions_are_durable_and_never_campaign_approval()
    {
        var (client, workspace, brand, _) = await Setup();
        var brief = await Brief(client, workspace, brand);
        var path = $"{Path(workspace)}/briefs/{brief.Id}";
        var request = new SubmitProductionCandidate(0, "First draft", "Independent candidate", Pixel, "candidate");
        var saved = await client.PostAsJsonAsync(path + "/candidates", request);
        Assert.Equal(HttpStatusCode.Created, saved.StatusCode);
        Assert.DoesNotContain("imageBytes", await saved.Content.ReadAsStringAsync());
        var candidate = (await saved.Content.ReadFromJsonAsync<CandidateWrite>())!.Candidate;
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync(path + "/candidates", request)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync(path + "/candidates", request with { Notes = "Changed" })).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync(path + "/candidates", request with { IdempotencyKey = "stale" })).StatusCode);
        var image = await client.GetAsync($"{path}/candidates/{candidate.Id}/image");
        Assert.Equal(Convert.FromBase64String(Pixel), await image.Content.ReadAsByteArrayAsync());
        Assert.Contains("no-store", image.Headers.CacheControl!.ToString());
        var reviewPath = $"{path}/candidates/{candidate.Id}/reviews";
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync(reviewPath, Review(0))).StatusCode);
        var reviewer = await Client("bliss.admin");
        Assert.Equal(HttpStatusCode.BadRequest, (await reviewer.PostAsJsonAsync(reviewPath, Review(0) with { QualityParity = "FAIL" })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await reviewer.PostAsJsonAsync(reviewPath, Review(0) with { CriticalDefect = true })).StatusCode);
        var assessed = await reviewer.PostAsJsonAsync(reviewPath, Review(0, "REVISE"));
        Assert.Equal(HttpStatusCode.Created, assessed.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await reviewer.PostAsJsonAsync(reviewPath, Review(0, "REVISE"))).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await reviewer.PostAsJsonAsync(reviewPath, Review(0, "PASS", "stale"))).StatusCode);
        var passed = await reviewer.PostAsJsonAsync(reviewPath, Review(1, "PASS", "pass"));
        Assert.Equal(HttpStatusCode.Created, passed.StatusCode);
        Assert.False((await passed.Content.ReadFromJsonAsync<ReviewWrite>())!.CampaignReady);
        Assert.Equal(HttpStatusCode.Created, (await client.PostAsJsonAsync(path + "/candidates", request with { ExpectedVersion = 1, Label = "Revision", IdempotencyKey = "new" })).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await reviewer.PostAsJsonAsync(reviewPath, Review(2, "PASS", "old-candidate"))).StatusCode);
        var history = await client.GetFromJsonAsync<History>(path);
        Assert.Equal(2, history!.Candidates.Count); Assert.Equal(2, history.Reviews.Count);
        Assert.All(history.Reviews, x => Assert.Equal(candidate.Id, x.CandidateId));
        Assert.False(history.CampaignReady);
        var audit = await client.GetFromJsonAsync<List<WeddingPlannerAuditEventDto>>($"/api/wedding-planner/workspaces/{workspace}/audit");
        Assert.NotNull(audit);
        Assert.Equal(2, audit.Count(x => x.Action == "PRODUCTION_CANDIDATE_SAVED"));
        Assert.Equal(2, audit.Count(x => x.Action == "PRODUCTION_REVIEW_RECORDED"));
    }

    [Fact]
    public async Task Cross_tenant_private_asset_access_and_invalid_uploads_are_refused()
    {
        var (owner, workspace, brand, _) = await Setup();
        var (other, otherWorkspace, _, _) = await Setup();
        var brief = await Brief(owner, workspace, brand);
        var path = $"{Path(workspace)}/briefs/{brief.Id}";
        var upload = new SubmitProductionCandidate(0, "Private", "", Pixel, "private");
        var created = await owner.PostAsJsonAsync(path + "/candidates", upload);
        created.EnsureSuccessStatusCode(); var candidate = (await created.Content.ReadFromJsonAsync<CandidateWrite>())!.Candidate;
        var imagePath = $"{path}/candidates/{candidate.Id}/image";
        Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync(imagePath)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync(path)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await other.PostAsJsonAsync(path + "/candidates", upload)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync($"{Path(otherWorkspace)}/briefs/{brief.Id}")).StatusCode);
        var anonymous = factory.CreateSecureClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync(imagePath)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await owner.PostAsJsonAsync(path + "/candidates", upload with { ImageBase64 = "not base64", ExpectedVersion = 1, IdempotencyKey = "invalid" })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await owner.PostAsJsonAsync(path + "/candidates", upload with { ImageBase64 = Convert.ToBase64String("<svg></svg>"u8.ToArray()), ExpectedVersion = 1, IdempotencyKey = "svg" })).StatusCode);
        owner.DefaultRequestHeaders.Remove("X-CSRF-TOKEN");
        Assert.Equal(HttpStatusCode.BadRequest, (await owner.PostAsJsonAsync(path + "/candidates", upload)).StatusCode);
    }
}
