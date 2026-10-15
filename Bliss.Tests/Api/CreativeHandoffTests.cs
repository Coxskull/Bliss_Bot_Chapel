using System.Net;
using System.Security.Cryptography;
using Bliss.Api.Financials;
using Bliss.Api.Demonstrations;
using Bliss.Domain.Financials;
using Bliss.Infrastructure.Financials;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using Bliss.Api.Operations;
using Bliss.Domain.Common;
using Bliss.Domain.Entities;
using Bliss.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Bliss.Tests.Api;

public sealed class CreativeHandoffTests : IClassFixture<WeddingPlannerOidcFactory>
{
    private readonly WeddingPlannerOidcFactory factory;
    public CreativeHandoffTests(WeddingPlannerOidcFactory factory) => this.factory = factory;
    private sealed record DecisionResponse(CandidateApprovalDecision Decision);
    private sealed record PlanResponse(CreativeCampaignHandoff Handoff, bool IsReplay, bool CampaignActivated);
    private sealed class Scenario(WebApplicationFactory<Program> app, CreativeHandoffFixture fixture) : IAsyncDisposable
    {
        public WebApplicationFactory<Program> App = app;
        public CreativeHandoffFixture Fixture = fixture;
        public string Path => $"/api/wedding-planner/workspaces/{Fixture.Workspace.WorkspaceId}/handoff";
        public ValueTask DisposeAsync() { App.Dispose(); return ValueTask.CompletedTask; }
    }
    private async Task<Scenario> Setup()
    {
        var name = Guid.NewGuid().ToString();
        var app = factory.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
        {
            services.RemoveAll<BlissDbContext>(); services.RemoveAll<DbContextOptions<BlissDbContext>>();
            services.AddDbContext<BlissDbContext>(options => options.UseInMemoryDatabase(name));
        }));
        using var scope = app.Services.CreateScope();
        var fixture = await CreativeHandoffTestData.Seed(scope.ServiceProvider.GetRequiredService<BlissDbContext>(), scope.ServiceProvider.GetRequiredService<IWebHostEnvironment>());
        return new(app, fixture);
    }
    private async Task<HttpClient> Client(Scenario s, string roles, Guid? advertiser = null, string? identity = null, Guid? reviewWorkspace = null)
    {
        var client = s.App.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost"), HandleCookies = true });
        client.DefaultRequestHeaders.Add("X-Test-Name", identity ?? $"Handoff {Guid.NewGuid():N}");
        client.DefaultRequestHeaders.Add("X-Test-Roles", roles);
        if (advertiser.HasValue) client.DefaultRequestHeaders.Add("X-Test-Advertiser-Id", advertiser.Value.ToString());
        if (reviewWorkspace.HasValue) client.DefaultRequestHeaders.Add("X-Test-Review-Workspaces", reviewWorkspace.Value.ToString());
        var session = await client.GetFromJsonAsync<SessionResponse>("/api/auth/session"); client.DefaultRequestHeaders.Add("X-CSRF-TOKEN", session!.CsrfToken); return client;
    }
    private async Task<PlanCreativeHandoff> Approve(Scenario s, HttpClient owner, HttpClient reviewer)
    {
        async Task<Guid> Decide(HttpClient client, string stage, string key)
        {
            var response = await client.PostAsJsonAsync($"{s.Path}/candidates/{s.Fixture.Candidate}/approvals", CreativeHandoffTestData.Approval(s.Fixture, stage, key));
            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
            return (await response.Content.ReadFromJsonAsync<DecisionResponse>())!.Decision.Id;
        }
        var advertiser = await Decide(owner, "ADVERTISER", "client-approval"); var qa = await Decide(reviewer, "ALPHA_QA", "qa-approval"); var fit = await Decide(reviewer, "INVENTORY_FIT", "fit-approval");
        return new(s.Fixture.Candidate, s.Fixture.Review, advertiser, qa, fit, Phase2DataSeeder.MatchBrazilApprovedId, s.Fixture.Campaign,
            CreativeHandoffFixture.Slot, s.Fixture.QuoteLine, "handoff-plan");
    }
    [Fact]
    public async Task Distinct_humans_plan_one_durable_handoff_using_only_accepted_economics()
    {
        await using var s = await Setup(); var owner = await Client(s, "bliss.advertiser", Phase2DataSeeder.Advertiser2Id); var reviewer = await Client(s, "bliss.admin");
        var request = await Approve(s, owner, reviewer);
        var result = await reviewer.PostAsJsonAsync(s.Path + "/plans", request); Assert.Equal(HttpStatusCode.Created, result.StatusCode);
        var body = await result.Content.ReadFromJsonAsync<PlanResponse>(); Assert.False(body!.CampaignActivated);
        using var snapshot = JsonDocument.Parse(body.Handoff.SnapshotJson); Assert.Equal(s.Fixture.Amount, snapshot.RootElement.GetProperty("lineAmount").GetDecimal());
        Assert.Equal(s.Fixture.Candidate, snapshot.RootElement.GetProperty("id").GetGuid());
        var replay = await reviewer.PostAsJsonAsync(s.Path + "/plans", request); Assert.Equal(HttpStatusCode.OK, replay.StatusCode);
        Assert.True((await replay.Content.ReadFromJsonAsync<PlanResponse>())!.IsReplay);
        Assert.Equal(HttpStatusCode.Conflict, (await reviewer.PostAsJsonAsync(s.Path + "/plans", request with { CampaignId = Guid.NewGuid() })).StatusCode);
        using var scope = s.App.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<BlissDbContext>();
        Assert.Single(await db.CreativeCampaignHandoffs.ToListAsync()); Assert.Single(await db.CampaignPlacementRuns.Where(x => x.SourceSystem == "CREATIVE_HANDOFF_V1").ToListAsync());
        var placement = await db.CampaignPlacements.SingleAsync(x => x.Id == body.Handoff.CampaignPlacementId);
        Assert.Equal("PLANNED", placement.Status); Assert.Null(placement.StartAt); Assert.Null(placement.EndAt);
        Assert.Equal("DRAFT", (await db.Campaigns.SingleAsync(x => x.Id == s.Fixture.Campaign)).Status);
        Assert.True((await db.AdInventorySlots.SingleAsync(x => x.Id == CreativeHandoffFixture.Slot)).IsAvailable);
    }

    [Fact]
    public async Task Launch_013_Ask_Alpha_metadata_preflight_requires_exact_workspace_session_quote_and_never_consent()
    {
        await using var s = await Setup();
        using var owner = await Client(s, "bliss.advertiser", Phase2DataSeeder.Advertiser2Id);
        using var reviewer = await Client(s, "bliss.admin");
        using var stranger = await Client(s, "bliss.advertiser", Guid.NewGuid());
        var request = await Approve(s, owner, reviewer);
        var posted = await reviewer.PostAsJsonAsync(s.Path + "/plans", request);
        Assert.Equal(HttpStatusCode.Created, posted.StatusCode);
        var handoff = (await posted.Content.ReadFromJsonAsync<PlanResponse>())!.Handoff;
        using var scope = s.App.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BlissDbContext>();
        var workspace = await db.WeddingPlannerWorkspaces.SingleAsync(x => x.Id == handoff.WorkspaceId);
        var run = await db.CampaignPlacementRuns.SingleAsync(x => x.Id == handoff.CampaignPlacementRunId);
        var placement = await db.CampaignPlacements.SingleAsync(x => x.Id == handoff.CampaignPlacementId);
        var campaign = await db.Campaigns.SingleAsync(x => x.Id == run.CampaignId);
        var line = await db.QuoteLineItems.SingleAsync(x => x.Id == handoff.QuoteLineItemId);
        var version = await db.QuoteVersions.SingleAsync(x => x.Id == line.QuoteVersionId);
        var quote = await db.Quotes.SingleAsync(x => x.Id == version.QuoteId);
        var session = new WeddingPlannerPlanningSession
        {
            Id = Guid.NewGuid(), WorkspaceId = workspace.Id, AdvertiserId = workspace.AdvertiserId,
            SourceSystem = "FICTIONAL_ASK_ALPHA_WAKE", Status = "OPEN"
        };
        var prospect = new DemonstrationRecord
        {
            Slug = "sample-local-fictional-prospect", Illustrative = true,
            WeddingPlannerWorkspaceId = workspace.Id.ToString("D"),
            WeddingPlannerSessionId = session.Id.ToString("D"),
            EconomicsQuoteId = quote.Id.ToString("D"),
            Messages = [
                new ChatRecord { Role = "VISITOR", Text = "Fictional question" },
                new ChatRecord { Role = "ASSISTANT", Text = "Fictional answer" }
            ]
        };
        AskAlphaLineagePreflightReading Inspect(DemonstrationRecord? source, WeddingPlannerPlanningSession? target = null) =>
            AskAlphaLineagePolicy.Inspect(handoff, campaign, placement, run,
                quote, version, line, workspace, target ?? session, source);

        var matched = Inspect(prospect);
        Assert.Equal("SOURCE_REFERENCES_MATCH_CONSENT_REQUIRED", matched.Status);
        Assert.True(matched.SourceMetadataMatched);
        Assert.Equal(1, matched.VisitorTurnCount);
        Assert.Equal(1, matched.AssistantTurnCount);
        Assert.Equal("UNRECORDED", matched.ConsentStatus);
        Assert.False(matched.TranscriptLinked);
        Assert.False(matched.TranscriptExportAuthorized);
        Assert.False(matched.ProspectOutreachAuthorized);
        Assert.False(matched.PaymentAuthorized);
        Assert.False(matched.CreatorPayoutAuthorized);
        Assert.False(matched.CampaignReady);
        Assert.False(matched.CampaignDeliveryAuthorized);
        Assert.Equal("NOT_SENT", matched.Delivery);

        // HTTP positive path: actual persisted local prospect source metadata,
        // exact stored workspace/session and the frozen accepted quote.
        db.WeddingPlannerPlanningSessions.Add(session);
        await db.SaveChangesAsync();
        s.App.Services.GetRequiredService<ProspectDemonstrationStore>().Update(document =>
        {
            document.Demonstrations.RemoveAll(x => x.Slug == prospect.Slug);
            document.Demonstrations.Add(prospect);
        });
        var linkedUrl = $"{s.Path}/plans/{handoff.Id}/ask-alpha-preflight?slug={prospect.Slug}";
        var linked = await owner.GetFromJsonAsync<AskAlphaLineagePreflightReading>(linkedUrl);
        Assert.NotNull(linked);
        Assert.Equal("SOURCE_REFERENCES_MATCH_CONSENT_REQUIRED", linked.Status);
        Assert.True(linked.SourceMetadataMatched);
        Assert.False(linked.TranscriptLinked);
        Assert.False(linked.TranscriptExportAuthorized);
        Assert.Equal("NOT_SENT", linked.Delivery);
        Assert.Equal(HttpStatusCode.NotFound, (await stranger.GetAsync(linkedUrl)).StatusCode);

        var foreign = prospect.WeddingPlannerWorkspaceId;
        prospect.WeddingPlannerWorkspaceId = Guid.NewGuid().ToString("D");
        Assert.Equal("NOT_LINKED", Inspect(prospect).Status);
        Assert.Equal(0, Inspect(prospect).VisitorTurnCount);
        prospect.WeddingPlannerWorkspaceId = foreign;

        var oldQuote = prospect.EconomicsQuoteId;
        prospect.EconomicsQuoteId = Guid.NewGuid().ToString("D");
        Assert.Equal("NOT_LINKED", Inspect(prospect).Status);
        prospect.EconomicsQuoteId = oldQuote;
        prospect.Suppressed = true;
        Assert.Equal("NOT_LINKED", Inspect(prospect).Status);
        prospect.Suppressed = false;
        Assert.Equal("NOT_LINKED", Inspect(prospect, new WeddingPlannerPlanningSession {
            Id=session.Id,WorkspaceId=Guid.NewGuid(), AdvertiserId=workspace.AdvertiserId }).Status);
        prospect.Messages.Clear();
        Assert.Equal("NOT_LINKED", Inspect(prospect).Status);

        var baseUrl = $"{s.Path}/plans/{handoff.Id}/ask-alpha-preflight?slug=not-a-real-source";
        Assert.Equal(HttpStatusCode.NotFound, (await stranger.GetAsync(baseUrl)).StatusCode);
        var result = await owner.GetFromJsonAsync<AskAlphaLineagePreflightReading>(baseUrl);
        Assert.NotNull(result);
        Assert.Equal("NOT_LINKED", result.Status);
        Assert.False(result.TranscriptLinked);
        Assert.Equal(HttpStatusCode.NotFound, (await owner.GetAsync(
            $"{s.Path}/plans/{Guid.NewGuid()}/ask-alpha-preflight?slug=not-a-real-source")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await owner.GetAsync(
            $"{s.Path}/plans/{handoff.Id}/ask-alpha-preflight?slug=../../external")).StatusCode);
    }

    [Fact]
    public async Task Launch_012_reports_original_PHP_quote_without_inventing_USD_or_leaking_other_workspaces()
    {
        await using var s = await Setup();
        using var owner = await Client(s, "bliss.advertiser", Phase2DataSeeder.Advertiser2Id);
        using var reviewer = await Client(s, "bliss.admin");
        using var stranger = await Client(s, "bliss.advertiser", Guid.NewGuid());
        var request = await Approve(s, owner, reviewer);
        var planned = await reviewer.PostAsJsonAsync(s.Path + "/plans", request);
        Assert.Equal(HttpStatusCode.Created, planned.StatusCode);
        var handoff = (await planned.Content.ReadFromJsonAsync<PlanResponse>())!.Handoff;
        var url = $"{s.Path}/plans/{handoff.Id}/funding-preflight";
        var preflight = await owner.GetFromJsonAsync<CampaignFundingPreflightReading>(url);
        Assert.NotNull(preflight);
        Assert.Equal("PHP", preflight.SourceCurrency);
        Assert.Equal("USD", preflight.RequiredMockCurrency);
        Assert.Equal("SOURCE_CURRENCY_NOT_USD", preflight.Status);
        Assert.Equal(handoff.Id, preflight.HandoffId);
        Assert.False(preflight.MockPaymentAuthorized);
        Assert.False(preflight.ProviderCalled);
        Assert.False(preflight.PaymentCompleted);
        Assert.False(preflight.CommissionAllocated);
        Assert.False(preflight.CreatorPayoutAuthorized);
        Assert.False(preflight.CampaignReady);
        Assert.False(preflight.CampaignDeliveryAuthorized);
        Assert.Equal("NOT_SENT", preflight.Delivery);
        Assert.Equal("NOT_APPROVED", preflight.FxPolicyStatus);
        Assert.Equal(HttpStatusCode.NotFound,(await stranger.GetAsync(url)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,(await owner.GetAsync(
            $"{s.Path}/plans/{Guid.NewGuid()}/funding-preflight")).StatusCode);

        using var scope = s.App.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BlissDbContext>();
        Assert.Single(await db.CreativeCampaignHandoffs.ToListAsync());
        Assert.Single(await db.CampaignPlacements.Where(x => x.Id == handoff.CampaignPlacementId).ToListAsync());
        Assert.Equal("DRAFT",(await db.Campaigns.SingleAsync(x => x.Id == request.CampaignId)).Status);
        Assert.Equal("PLANNED",(await db.CampaignPlacements.SingleAsync(x => x.Id == handoff.CampaignPlacementId)).Status);
    }

    [Fact]
    public async Task Launch_012_currency_preflight_is_read_only_and_rejects_tampered_lineage_and_stale_authority()
    {
        await using var s = await Setup();
        using var owner = await Client(s, "bliss.advertiser", Phase2DataSeeder.Advertiser2Id);
        using var reviewer = await Client(s, "bliss.admin");
        var request = await Approve(s, owner, reviewer);
        var posted = await reviewer.PostAsJsonAsync(s.Path + "/plans", request);
        Assert.Equal(HttpStatusCode.Created, posted.StatusCode);
        var handoff = (await posted.Content.ReadFromJsonAsync<PlanResponse>())!.Handoff;
        using var scope = s.App.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BlissDbContext>();
        var run = await db.CampaignPlacementRuns.SingleAsync(x => x.Id == handoff.CampaignPlacementRunId);
        var placement = await db.CampaignPlacements.SingleAsync(x => x.Id == handoff.CampaignPlacementId);
        var campaign = await db.Campaigns.SingleAsync(x => x.Id == run.CampaignId);
        var line = await db.QuoteLineItems.SingleAsync(x => x.Id == handoff.QuoteLineItemId);
        var version = await db.QuoteVersions.SingleAsync(x => x.Id == line.QuoteVersionId);
        var quote = await db.Quotes.SingleAsync(x => x.Id == version.QuoteId);
        CampaignFundingPreflightReading Inspect(bool approval=true,bool outcome=true) =>
            CampaignFundingPreflight.Inspect(handoff,campaign,placement,run,quote,version,line,approval,outcome);
        Assert.Equal("SOURCE_CURRENCY_NOT_USD",Inspect().Status);
        Assert.Equal("QUOTE_AUTHORITY_CHANGED",Inspect(false).Status);
        Assert.Equal("QUOTE_AUTHORITY_CHANGED",Inspect(true,false).Status);
        var snapshot = handoff.SnapshotJson;
        handoff.SnapshotJson = "{}";
        Assert.Throws<InvalidOperationException>(()=>Inspect());
        handoff.SnapshotJson = snapshot;
        var originalAmount = line.LineAmount;
        line.LineAmount++;
        Assert.Throws<InvalidOperationException>(()=>Inspect());
        line.LineAmount = originalAmount;
        var originalDelivery = handoff.Delivery;
        handoff.Delivery = "SENT";
        Assert.Throws<InvalidOperationException>(()=>Inspect());
        handoff.Delivery = originalDelivery;
        var clone = JsonNode.Parse(handoff.SnapshotJson)!.AsObject();
        clone["currencyCode"] = "USD";
        var usdHandoff = new CreativeCampaignHandoff {
            Id=handoff.Id,WorkspaceId=handoff.WorkspaceId,Status=handoff.Status,Delivery=handoff.Delivery,
            CampaignPlacementId=handoff.CampaignPlacementId,CampaignPlacementRunId=handoff.CampaignPlacementRunId,
            QuoteLineItemId=handoff.QuoteLineItemId,SnapshotJson=clone.ToJsonString()
        };
        var usdQuote = new Quote {
            Id=quote.Id,Status=quote.Status,CurrentVersionNumber=quote.CurrentVersionNumber,
            AdvertiserOpportunityId=quote.AdvertiserOpportunityId,CurrencyCode="USD"
        };
        var usdVersion = new QuoteVersion {
            Id=version.Id,QuoteId=version.QuoteId,VersionNumber=version.VersionNumber,
            TotalAmount=version.TotalAmount,CurrencyCode="USD"
        };
        var usdLine = new QuoteLineItem {
            Id=line.Id,QuoteVersionId=line.QuoteVersionId,
            Quantity=line.Quantity,LineAmount=line.LineAmount,CurrencyCode="USD"
        };
        // Pure synthetic USD projection: not an authorized change to the PHP quote.
        var reading = CampaignFundingPreflight.Inspect(usdHandoff,campaign,placement,run,
            usdQuote,usdVersion,usdLine,true,true);
        Assert.Equal("USD_QUOTE_STILL_REQUIRES_FINANCIAL_AUTHORITY",reading.Status);
        Assert.False(reading.MockPaymentAuthorized);
        Assert.False(reading.CampaignDeliveryAuthorized);
        Assert.Equal("PHP",quote.CurrencyCode);
    }

    [Fact]
    public async Task Launch_011_only_correlates_exact_unsent_handoff_and_mock_financial_quote()
    {
        await using var s = await Setup();
        using var owner = await Client(s, "bliss.advertiser", Phase2DataSeeder.Advertiser2Id);
        using var reviewer = await Client(s, "bliss.admin");
        var request = await Approve(s, owner, reviewer);
        var response = await reviewer.PostAsJsonAsync(s.Path + "/plans", request);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var stored = (await response.Content.ReadFromJsonAsync<PlanResponse>())!.Handoff;
        using var scope = s.App.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BlissDbContext>();
        var workspace = await db.WeddingPlannerWorkspaces.SingleAsync(x => x.Id == stored.WorkspaceId);
        var campaign = await db.Campaigns.SingleAsync(x => x.Id == s.Fixture.Campaign);
        var placement = await db.CampaignPlacements.SingleAsync(x => x.Id == stored.CampaignPlacementId);
        var run = await db.CampaignPlacementRuns.SingleAsync(x => x.Id == stored.CampaignPlacementRunId);
        var quote = await db.Quotes.SingleAsync(x => x.Id == s.Fixture.Quote);
        var line = await db.QuoteLineItems.SingleAsync(x => x.Id == s.Fixture.QuoteLine);
        var version = await db.QuoteVersions.SingleAsync(x => x.Id == line.QuoteVersionId);
        var candidate = await db.AcademyProductionCandidates.SingleAsync(x => x.Id == stored.CandidateId);
        var brief = await db.AcademyProductionBriefs.SingleAsync(x => x.Id == candidate.BriefId);
        var fit = await db.CandidateApprovalDecisions.SingleAsync(x => x.Id == stored.InventoryFitId);
        var fundingScope = new FinancialScope(Guid.NewGuid(), FinancialModule.BlissBot, campaign.Id);
        var binding = new FundingBinding(quote.Id, version.Id, fundingScope, "test-owner",
            workspace.AdvertiserId, Guid.NewGuid());
        var minor = checked((long)(version.TotalAmount * 100m));
        var receipt = new MockCampaignFinancialEvidence(Guid.NewGuid(), version.Id,
            fundingScope, "USD", minor, Guid.NewGuid(),
            [new(Guid.NewGuid(),Guid.NewGuid(),"TEST_CASH","Debit",minor),
             new(Guid.NewGuid(),Guid.NewGuid(),"TEST_RECIPIENT","Credit",minor)],
            new string('A', 64));
        // The service-produced handoff uses the Manila PHP Economics fixture.
        // Its native stored quote MUST be rejected by the USD-only mock funding
        // policy: there is no approved FX conversion or USD payment authority.
        Assert.NotEqual("USD", quote.CurrencyCode);
        Assert.Throws<InvalidOperationException>(() =>
            MockCampaignDossierVerifier.Correlate(receipt, binding, stored, workspace,
                campaign, placement, run, quote, version, line, 1, brief, candidate, fit));

        // Independently model an internally consistent USD-only unit fixture
        // without rewriting, reapproving, or claiming payment for the PHP records.
        // A future accepted actual USD quote/handoff must satisfy these same checks.
        var syntheticSnapshot = JsonNode.Parse(stored.SnapshotJson)!.AsObject();
        syntheticSnapshot["currencyCode"] = "USD";
        var usdHandoff = new CreativeCampaignHandoff {
            Id = stored.Id, WorkspaceId = stored.WorkspaceId,
            CandidateId = stored.CandidateId, ReviewId = stored.ReviewId,
            InventoryFitId = stored.InventoryFitId, QuoteLineItemId = stored.QuoteLineItemId,
            CampaignPlacementId = stored.CampaignPlacementId,
            CampaignPlacementRunId = stored.CampaignPlacementRunId,
            Status = stored.Status, Delivery = stored.Delivery,
            SnapshotJson = syntheticSnapshot.ToJsonString()
        };
        var usdQuote = new Quote {
            Id = quote.Id, Status = quote.Status, CurrencyCode = "USD",
            AdvertiserOpportunityId = quote.AdvertiserOpportunityId,
            CurrentVersionNumber = quote.CurrentVersionNumber
        };
        var usdVersion = new QuoteVersion {
            Id = version.Id, QuoteId = quote.Id, VersionNumber = version.VersionNumber,
            CurrencyCode = "USD", TotalAmount = version.TotalAmount
        };
        var usdLine = new QuoteLineItem {
            Id = line.Id, QuoteVersionId = line.QuoteVersionId,
            CurrencyCode = "USD", Quantity = line.Quantity, LineAmount = line.LineAmount
        };
        MockCampaignDossier Project(MockCampaignFinancialEvidence current) =>
            MockCampaignDossierVerifier.Correlate(current, binding, usdHandoff, workspace,
                campaign, placement, run, usdQuote, usdVersion, usdLine, 1, brief, candidate, fit);
        var dossier = Project(receipt);
        Assert.Equal("STORED_PLANNED_HANDOFF_CORRELATED", dossier.CorrelationStatus);
        Assert.Equal(campaign.Id, dossier.CampaignId);
        Assert.Equal(minor.ToString(), dossier.AmountMinor);
        Assert.Equal("NOT_LINKED", dossier.AskAlphaTranscriptStatus);
        Assert.Equal("NOT_INDEPENDENTLY_VERIFIED", dossier.PlacementPreviewStatus);
        Assert.False(dossier.PaymentCompleted);
        Assert.False(dossier.ProviderCalled);
        Assert.False(dossier.CampaignDeliveryAuthorized);
        Assert.False(dossier.CampaignReady);
        Assert.Equal("NOT_SENT", dossier.Delivery);
        Assert.Equal(32, dossier.ImageWidth);
        Assert.Equal(16, dossier.ImageHeight);
        Assert.Throws<InvalidOperationException>(() => Project(receipt with {
            Scope = fundingScope with { CampaignId = Guid.NewGuid() }
        }));
        Assert.Throws<InvalidOperationException>(() => Project(receipt with {
            AmountMinor = minor + 1
        }));
        Assert.Throws<InvalidOperationException>(() => Project(receipt with {
            PaymentCompleted = true
        }));
        Assert.Throws<InvalidOperationException>(() =>
            MockCampaignDossierVerifier.Correlate(receipt,binding,usdHandoff,workspace,
                campaign,placement,run,usdQuote,usdVersion,usdLine,2,brief,candidate,fit));
        usdHandoff.Delivery = "SENT";
        Assert.Throws<InvalidOperationException>(() => Project(receipt));
        usdHandoff.Delivery = "NOT_SENT";
        var originalSnapshot = usdHandoff.SnapshotJson;
        usdHandoff.SnapshotJson = "{}";
        Assert.Throws<InvalidOperationException>(() => Project(receipt));
        usdHandoff.SnapshotJson = originalSnapshot;
        var originalImage = candidate.ImageBytes;
        candidate.ImageBytes = [.. originalImage];
        candidate.ImageBytes[^1] ^= 1;
        Assert.Throws<InvalidOperationException>(() => Project(receipt));
        candidate.ImageBytes = originalImage;
        Assert.Equal(dossier, Project(receipt));
    }

    [Fact]
    public async Task Standalone_reviewer_can_record_qa_and_inventory_fit_but_cannot_impersonate_advertiser_or_plan()
    {
        await using var s = await Setup();
        using var owner = await Client(s, "bliss.advertiser", Phase2DataSeeder.Advertiser2Id);
        using var reviewer = await Client(s, "bliss.reviewer", reviewWorkspace: s.Fixture.Workspace.WorkspaceId); // assigned, no advertiser_id
        using var unassigned = await Client(s, "bliss.reviewer");
        using var wrongAssignment = await Client(s, "bliss.reviewer", reviewWorkspace: Guid.NewGuid());
        using var reader = await Client(s, "bliss.viewer", Phase2DataSeeder.Advertiser2Id, reviewWorkspace: s.Fixture.Workspace.WorkspaceId);
        var decisionsPath = $"{s.Path}/candidates/{s.Fixture.Candidate}/approvals";

        // Review scope does not widen normal workspace options or grant planner rights.
        Assert.Equal(HttpStatusCode.NotFound, (await reviewer.GetAsync(s.Path + "/options")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await reviewer.GetAsync(decisionsPath)).StatusCode);
        // Both scope and the actual reviewer role are required; role alone and
        // another tenant assignment cannot append any audit/approval evidence.
        foreach (var unauthorized in new[] { unassigned, wrongAssignment, reader })
        {
            Assert.Equal(HttpStatusCode.Forbidden,
                (await unauthorized.PostAsJsonAsync(decisionsPath,
                    CreativeHandoffTestData.Approval(s.Fixture, "ALPHA_QA", Guid.NewGuid().ToString("N")))).StatusCode);
        }
        Assert.Equal(HttpStatusCode.Forbidden,
            (await reader.PostAsJsonAsync(decisionsPath,
                CreativeHandoffTestData.Approval(s.Fixture, "ALPHA_QA", "viewer-qa"))).StatusCode);
        // No advertiser tenant binding: conceal the workspace before approval checks.
        Assert.Equal(HttpStatusCode.NotFound,
            (await reviewer.PostAsJsonAsync(decisionsPath,
                CreativeHandoffTestData.Approval(s.Fixture, "ADVERTISER", "reviewer-owner"))).StatusCode);

        var request = await Approve(s, owner, reviewer);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await reviewer.PostAsJsonAsync(s.Path + "/plans", request)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await reviewer.PostAsJsonAsync(s.Path + "/campaigns",
                new CreateHandoffCampaign(Guid.NewGuid(), Phase2DataSeeder.MatchBrazilApprovedId, "No reviewer campaign"))).StatusCode);

        using var scope = s.App.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BlissDbContext>();
        Assert.Equal(3, await db.CandidateApprovalDecisions.CountAsync());
        Assert.Empty(await db.CreativeCampaignHandoffs.ToListAsync());
        Assert.Empty(await db.CampaignPlacementRuns.Where(x => x.SourceSystem == "CREATIVE_HANDOFF_V1").ToListAsync());
    }

    [Fact]
    public async Task Withholding_after_planning_preserves_one_not_sent_economics_snapshot()
    {
        await using var s = await Setup();
        using var owner = await Client(s, "bliss.advertiser", Phase2DataSeeder.Advertiser2Id);
        using var reviewer = await Client(s, "bliss.admin");
        var request = await Approve(s, owner, reviewer);
        var planned = await reviewer.PostAsJsonAsync(s.Path + "/plans", request);
        Assert.Equal(HttpStatusCode.Created, planned.StatusCode);
        var original = await planned.Content.ReadFromJsonAsync<PlanResponse>();
        Assert.NotNull(original);
        Assert.False(original.CampaignActivated);
        var originalSnapshot = original.Handoff.SnapshotJson;
        using (var economics = JsonDocument.Parse(originalSnapshot))
            Assert.Equal(s.Fixture.Amount, economics.RootElement.GetProperty("lineAmount").GetDecimal());

        // The advertiser may revoke future use, but must not edit past audited
        // pricing or silently send/re-activate a previously planned placement.
        var withdrawal = await owner.PostAsJsonAsync(
            $"{s.Path}/candidates/{s.Fixture.Candidate}/approvals",
            CreativeHandoffTestData.Approval(s.Fixture, "ADVERTISER", "withdraw-after-plan", 1, "WITHHOLD"));
        Assert.Equal(HttpStatusCode.Created, withdrawal.StatusCode);
        var attemptedSecondPlan = await reviewer.PostAsJsonAsync(
            s.Path + "/plans", request with { IdempotencyKey = "new-plan-after-withdrawal" });
        Assert.False(attemptedSecondPlan.IsSuccessStatusCode);

        var history = await reviewer.GetFromJsonAsync<List<CreativeCampaignHandoff>>(s.Path + "/plans");
        var retained = Assert.Single(history!);
        Assert.Equal(original.Handoff.Id, retained.Id);
        Assert.Equal(originalSnapshot, retained.SnapshotJson);
        Assert.Equal(original.Handoff.CampaignPlacementId, retained.CampaignPlacementId);

        using var scope = s.App.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BlissDbContext>();
        Assert.Single(await db.CreativeCampaignHandoffs.ToListAsync());
        Assert.Single(await db.CampaignPlacementRuns.Where(x => x.SourceSystem == "CREATIVE_HANDOFF_V1").ToListAsync());
        Assert.Single(await db.WeddingPlannerAuditEvents.Where(x => x.Action == "CREATIVE_HANDOFF_PLANNED").ToListAsync());
        Assert.Equal(2, await db.CandidateApprovalDecisions.CountAsync(x => x.Stage == "ADVERTISER"));
        var placement = await db.CampaignPlacements.SingleAsync(x => x.Id == retained.CampaignPlacementId);
        Assert.Equal("PLANNED", placement.Status);
        Assert.Null(placement.StartAt);
        Assert.Null(placement.EndAt);
        Assert.Equal("DRAFT", (await db.Campaigns.SingleAsync(x => x.Id == s.Fixture.Campaign)).Status);
        Assert.True((await db.AdInventorySlots.SingleAsync(x => x.Id == CreativeHandoffFixture.Slot)).IsAvailable);
    }

    [Fact]
    public async Task Advertiser_cannot_self_qa_or_plan_and_cross_tenant_anonymous_csrf_are_refused()
    {
        await using var s = await Setup(); var owner = await Client(s, "bliss.advertiser", Phase2DataSeeder.Advertiser2Id);
        var other = await Client(s, "bliss.advertiser", Phase1DataSeeder.AdvertiserId); var reviewer = await Client(s, "bliss.admin");
        var path = $"{s.Path}/candidates/{s.Fixture.Candidate}/approvals";
        Assert.Equal(HttpStatusCode.Forbidden, (await owner.PostAsJsonAsync(path, CreativeHandoffTestData.Approval(s.Fixture, "ALPHA_QA", "bad-qa"))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await reviewer.PostAsJsonAsync(path, CreativeHandoffTestData.Approval(s.Fixture, "ADVERTISER", "bad-owner"))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync(path)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await other.PostAsJsonAsync(path, CreativeHandoffTestData.Approval(s.Fixture, "ADVERTISER", "other-owner"))).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await s.App.CreateClient().GetAsync(path)).StatusCode);
        var request = await Approve(s, owner, reviewer);
        Assert.Equal(HttpStatusCode.Forbidden, (await owner.PostAsJsonAsync(s.Path + "/plans", request)).StatusCode);
        owner.DefaultRequestHeaders.Remove("X-CSRF-TOKEN");
        Assert.Equal(HttpStatusCode.BadRequest, (await owner.PostAsJsonAsync(path, CreativeHandoffTestData.Approval(s.Fixture, "ADVERTISER", "csrf-owner", 1, "WITHHOLD"))).StatusCode);
        using var scope = s.App.Services.CreateScope(); Assert.Empty(await scope.ServiceProvider.GetRequiredService<BlissDbContext>().CreativeCampaignHandoffs.ToListAsync());
    }
    [Fact]
    public async Task One_principal_with_both_roles_cannot_supply_both_advertiser_and_qa_approval()
    {
        await using var s = await Setup(); var combined = await Client(s, "bliss.advertiser,bliss.admin", Phase2DataSeeder.Advertiser2Id, "Same human");
        var path = $"{s.Path}/candidates/{s.Fixture.Candidate}/approvals";
        Assert.Equal(HttpStatusCode.Created, (await combined.PostAsJsonAsync(path, CreativeHandoffTestData.Approval(s.Fixture, "ADVERTISER", "dual-owner"))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await combined.PostAsJsonAsync(path, CreativeHandoffTestData.Approval(s.Fixture, "ALPHA_QA", "dual-qa-approval"))).StatusCode);
    }
    [Theory]
    [InlineData("revoked")]
    [InlineData("inventory")]
    [InlineData("quote")]
    [InlineData("match")]
    [InlineData("new-candidate")]
    [InlineData("new-review")]
    [InlineData("new-brand")]
    [InlineData("quote-slot")]
    [InlineData("revised-quote")]
    public async Task Gate_changes_prevent_any_placement_or_handoff_write(string changed)
    {
        await using var s = await Setup(); var owner = await Client(s, "bliss.advertiser", Phase2DataSeeder.Advertiser2Id); var reviewer = await Client(s, "bliss.admin");
        var request = await Approve(s, owner, reviewer);
        if (changed == "revoked") Assert.Equal(HttpStatusCode.Created, (await owner.PostAsJsonAsync($"{s.Path}/candidates/{s.Fixture.Candidate}/approvals", CreativeHandoffTestData.Approval(s.Fixture, "ADVERTISER", "revoke-owner", 1, "WITHHOLD"))).StatusCode);
        using (var scope = s.App.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<BlissDbContext>();
            if (changed == "inventory") (await db.AdInventorySlots.SingleAsync(x => x.Id == CreativeHandoffFixture.Slot)).IsAvailable = false;
            if (changed == "quote") (await db.Quotes.SingleAsync(x => x.Id == s.Fixture.Quote)).Status = "DRAFT";
            if (changed == "revised-quote") (await db.Quotes.SingleAsync(x => x.Id == s.Fixture.Quote)).CurrentVersionNumber++;
            if (changed == "match") (await db.BlissMatches.SingleAsync(x => x.Id == Phase2DataSeeder.MatchBrazilApprovedId)).Status = EntityStatuses.ReviewRequired;
            var environment = scope.ServiceProvider.GetRequiredService<IWebHostEnvironment>();
            var production = new AcademyProductionService(db, new BlueprintPhaseService(db, environment));
            if (changed == "new-candidate") await production.SubmitAsync(s.Fixture.Workspace, s.Fixture.Brief,
                new SubmitProductionCandidate(1, "Revised asset", "Test revision invalidates old approval", CreativeHandoffTestData.Png, "revised-candidate"), "ADVERTISER", "Fixture owner", "test", default);
            if (changed == "new-review") await production.ReviewAsync(s.Fixture.Workspace, s.Fixture.Brief, s.Fixture.Candidate,
                new RecordProductionReview(1, "PASS", "PASS", "PASS", "PASS", "PASS", false, "Fresh instructor assessment needs fresh approvals", "", "", "revised-review"), "OPERATOR", "Fixture QA", "test", default);
            if (changed == "new-brand") await new WeddingPlannerBrandDnaService(db).AppendAsync(s.Fixture.Workspace,
                new BrandDnaFields("New TEST brand", "pharmacy", "Taipei", "English", "Gold", "Montserrat", "Awareness", "Test"),
                2, "revised-brand", false, "ADVERTISER", "Fixture owner", "test", default);
            if (changed == "quote-slot")
            {
                var line = await db.QuoteLineItems.SingleAsync(x => x.Id == s.Fixture.QuoteLine);
                (await db.RateRecommendations.SingleAsync(x => x.Id == line.RateRecommendationId)).AdInventorySlotId = Guid.Parse("99999999-9999-9999-9999-999999999996");
            }
            await db.SaveChangesAsync();
        }
        var result = await reviewer.PostAsJsonAsync(s.Path + "/plans", request);
        Assert.Equal(changed is "new-candidate" or "new-brand" ? HttpStatusCode.Conflict : HttpStatusCode.BadRequest, result.StatusCode);
        using var verify = s.App.Services.CreateScope(); var context = verify.ServiceProvider.GetRequiredService<BlissDbContext>();
        Assert.Empty(await context.CreativeCampaignHandoffs.ToListAsync()); Assert.Empty(await context.CampaignPlacementRuns.Where(x => x.SourceSystem == "CREATIVE_HANDOFF_V1").ToListAsync());
    }
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Unreviewed_and_pixel_candidates_cannot_receive_inventory_fit_approval(bool reviewedPixel)
    {
        await using var s = await Setup(); var reviewer = await Client(s, "bliss.admin"); Guid candidateId; Guid? reviewId = null;
        using (var scope = s.App.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<BlissDbContext>();
            var production = new AcademyProductionService(db, new BlueprintPhaseService(db, scope.ServiceProvider.GetRequiredService<IWebHostEnvironment>()));
            candidateId = (await production.SubmitAsync(s.Fixture.Workspace, s.Fixture.Brief,
                new SubmitProductionCandidate(1, "Test new candidate", "Fixture", reviewedPixel ? AcademyProductionTests.Pixel : CreativeHandoffTestData.Png,
                    "unready-candidate"), "ADVERTISER", "Fixture owner", "test", default)).Row.Id;
            if (reviewedPixel) reviewId = (await production.ReviewAsync(s.Fixture.Workspace, s.Fixture.Brief, candidateId,
                new RecordProductionReview(0, "PASS", "PASS", "PASS", "PASS", "PASS", false, "Synthetic test only", "", "", "pixel-review"),
                "OPERATOR", "Fixture QA", "test", default)).Row.Id;
        }
        var request = CreativeHandoffTestData.Approval(s.Fixture, "INVENTORY_FIT", "unready-fit") with { ReviewId = reviewId };
        Assert.Equal(HttpStatusCode.BadRequest, (await reviewer.PostAsJsonAsync($"{s.Path}/candidates/{candidateId}/approvals", request)).StatusCode);
        using var verify = s.App.Services.CreateScope(); Assert.Empty(await verify.ServiceProvider.GetRequiredService<BlissDbContext>().CandidateApprovalDecisions.ToListAsync());
    }
    [Fact]
    public async Task Approval_retries_conflict_on_changed_input_and_fit_requires_explicit_human_checks()
    {
        await using var s = await Setup(); var owner = await Client(s, "bliss.advertiser", Phase2DataSeeder.Advertiser2Id); var reviewer = await Client(s, "bliss.admin");
        var path = $"{s.Path}/candidates/{s.Fixture.Candidate}/approvals";
        var approval = CreativeHandoffTestData.Approval(s.Fixture, "ADVERTISER", "retry-client-approval");
        Assert.Equal(HttpStatusCode.Created, (await owner.PostAsJsonAsync(path, approval)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await owner.PostAsJsonAsync(path, approval)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await owner.PostAsJsonAsync(path, approval with { Rationale = "Changed intent" })).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await owner.PostAsJsonAsync(path, approval with { IdempotencyKey = "stale-approval" })).StatusCode);
        var fit = CreativeHandoffTestData.Approval(s.Fixture, "INVENTORY_FIT", "incomplete-fit");
        Assert.Equal(HttpStatusCode.BadRequest, (await reviewer.PostAsJsonAsync(path, fit with { CreatorSafeAreaVerified = false })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await reviewer.PostAsJsonAsync(path, fit with { AdInventorySlotId = Guid.Parse("99999999-9999-9999-9999-999999999998") })).StatusCode);
        using var scope = s.App.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<BlissDbContext>();
        Assert.Single(await db.CandidateApprovalDecisions.ToListAsync()); Assert.Empty(await db.CreativeCampaignHandoffs.ToListAsync());
    }
    [Fact]
    public async Task Staff_draft_campaign_creation_is_scoped_replayable_and_never_active()
    {
        await using var s = await Setup(); var owner = await Client(s, "bliss.advertiser", Phase2DataSeeder.Advertiser2Id); var staff = await Client(s, "bliss.admin");
        var request = new CreateHandoffCampaign(Guid.NewGuid(), Phase2DataSeeder.MatchBrazilApprovedId, "TEST draft campaign");
        Assert.Equal(HttpStatusCode.Forbidden, (await owner.PostAsJsonAsync(s.Path + "/campaigns", request)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await staff.PostAsJsonAsync(s.Path + "/campaigns", request with { BlissMatchId = Phase1DataSeeder.MatchAId })).StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await staff.PostAsJsonAsync(s.Path + "/campaigns", request)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await staff.PostAsJsonAsync(s.Path + "/campaigns", request)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await staff.PostAsJsonAsync(s.Path + "/campaigns", request with { Name = "Changed intent" })).StatusCode);
        using var scope = s.App.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<BlissDbContext>();
        Assert.Equal("DRAFT", (await db.Campaigns.SingleAsync(x => x.Id == request.CampaignId)).Status);
        Assert.Single(await db.WeddingPlannerAuditEvents.Where(x => x.Action == "HANDOFF_CAMPAIGN_DRAFT_CREATED").ToListAsync());
    }
    [Fact]
    public async Task Missing_gates_incorrect_quote_graph_and_duplicate_quote_line_are_refused()
    {
        await using var s = await Setup(); var owner = await Client(s, "bliss.advertiser", Phase2DataSeeder.Advertiser2Id); var reviewer = await Client(s, "bliss.admin");
        var request = await Approve(s, owner, reviewer);
        Assert.Equal(HttpStatusCode.BadRequest, (await reviewer.PostAsJsonAsync(s.Path + "/plans", request with { AlphaQaApprovalId = Guid.NewGuid() })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await reviewer.PostAsJsonAsync(s.Path + "/plans", request with { BlissMatchId = Phase1DataSeeder.MatchAId })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await reviewer.PostAsJsonAsync(s.Path + "/plans", request with { AdInventorySlotId = Guid.Parse("99999999-9999-9999-9999-999999999996") })).StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await reviewer.PostAsJsonAsync(s.Path + "/plans", request)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await reviewer.PostAsJsonAsync(s.Path + "/plans", request with { IdempotencyKey = "different-plan-key" })).StatusCode);
    }
}
