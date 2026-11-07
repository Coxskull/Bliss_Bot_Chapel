using Bliss.Domain.AdvertisingRealEstate;
using Bliss.Domain.Economics;
using Bliss.Domain.Entities;

namespace Bliss.Tests.AdvertisingRealEstate;

/// <summary>Only fictional object-graph ledgers; never create a real quote.</summary>
public sealed class CommercialEconomicsReadOnlyGateTests
{
    private static readonly Guid QuoteId = Guid.Parse("10000000-0000-4000-8000-000000000001");
    private static readonly Guid VersionId = Guid.Parse("10000000-0000-4000-8000-000000000002");
    private static readonly Guid OpportunityId = Guid.Parse("10000000-0000-4000-8000-000000000003");
    private static readonly Guid CreatorId = Guid.Parse("10000000-0000-4000-8000-000000000004");
    private static readonly Guid[] SlotIds =
    [
        Guid.Parse("10000000-0000-4000-8000-000000000011"),
        Guid.Parse("10000000-0000-4000-8000-000000000012")
    ];
    private static readonly Guid[] RecommendationIds =
    [
        Guid.Parse("10000000-0000-4000-8000-000000000021"),
        Guid.Parse("10000000-0000-4000-8000-000000000022")
    ];

    private static CatalogProduct Product()
    {
        var board = RealEstateCatalog.Board(
            assets: new Dictionary<string, bool> { ["ARE-001-V1"] = true },
            creatorAuthorized: true,
            lifecycleOverrides: new Dictionary<string, string>
            {
                ["ARE-P01"] = RealEstateCatalog.Active,
                ["ARE-001-V1"] = RealEstateCatalog.Active
            }, deviceStatus: "OWNER_APPROVED", platformStatus: "OWNER_APPROVED");
        return board.Products.Single(p => p.ProductId == "ARE-P01");
    }

    private static CommercialInventoryReview Inventory() => new(
        "OFFLINE_SYNTHETIC_ONLY", "NEEDS_REVIEW", Product().ProductId, Product().Version,
        Product().ShowcaseId,
        new CommercialGeometryEvidence("GEOMETRY_VALIDATED", "G1", 576000, 2073600, 2778, 5000, []),
        "PRICING_UNAVAILABLE", true, true, false, false, false, "NOT_SENT",
        "fictional only", []);

    private static Quote Quote() => new()
    {
        Id = QuoteId, Status = QuoteStatuses.Accepted, CurrentVersionNumber = 1,
        AdvertiserOpportunityId = OpportunityId, CurrencyCode = "PHP",
        RequestedBy = "FICTIONAL", SourceSystem = "SYNTHETIC"
    };
    private static QuoteVersion Version(int occurrences = 5) => new()
    {
        Id = VersionId, QuoteId = QuoteId, VersionNumber = 1, CurrencyCode = "PHP",
        SubtotalAmount = 40m * occurrences,
        TotalAmount = 40m * occurrences,
        LineItems = new List<QuoteLineItem>
        {
            new() { Id = Guid.Parse("20000000-0000-4000-8000-000000000001"),
                QuoteVersionId = VersionId, RateRecommendationId = RecommendationIds[0],
                Quantity = occurrences, UnitAmount = 20m, LineAmount = occurrences * 20m,
                CurrencyCode = "PHP" },
            new() { Id = Guid.Parse("20000000-0000-4000-8000-000000000002"),
                QuoteVersionId = VersionId, RateRecommendationId = RecommendationIds[1],
                Quantity = occurrences, UnitAmount = 20m, LineAmount = occurrences * 20m,
                CurrencyCode = "PHP" }
        }
    };

    private static QuoteApprovalDecision Approval() => new()
    {
        QuoteId = QuoteId, QuoteVersionId = VersionId,
        Decision = QuoteApprovalDecisions.Approved, ReviewerLabel = "FICTIONAL_REVIEWER",
        Rationale = "SYNTHETIC TEST"
    };

    private static QuoteOutcome Outcome(int occurrences = 5) => new()
    {
        QuoteId = QuoteId, QuoteVersionId = VersionId,
        Response = QuoteOutcomeResponses.Accepted, Amount = 40m * occurrences,
        CurrencyCode = "PHP"
    };

    private static IReadOnlyList<RateRecommendation> Recommendations() =>
    [
        new() { Id = RecommendationIds[0], CreatorId = CreatorId,
            AdvertiserOpportunityId = OpportunityId, AdInventorySlotId = SlotIds[0],
            CurrencyCode = "PHP" },
        new() { Id = RecommendationIds[1], CreatorId = CreatorId,
            AdvertiserOpportunityId = OpportunityId, AdInventorySlotId = SlotIds[1],
            CurrencyCode = "PHP" }
    ];

    private static SyntheticQuoteProductBinding Binding(int occurrences = 5) =>
        new(Product().ProductId, Product().Version, "G1", occurrences,
            CreatorId, OpportunityId, QuoteId, VersionId,
            "synthetic://quote-product-scope", "SYNTHETIC-V1",
            Product().SlotIds.Select((slot, index) =>
                new SyntheticQuoteSlotBinding(slot, SlotIds[index], RecommendationIds[index])).ToArray());

    private static CommercialEconomicsReadOnlyDecision Evaluate(
        Quote? quote = null, QuoteVersion? version = null,
        QuoteApprovalDecision? approval = null, QuoteOutcome? outcome = null,
        IReadOnlyList<RateRecommendation>? recommendations = null,
        SyntheticQuoteProductBinding? binding = null,
        CommercialInventoryReview? inventory = null)
        => CommercialEconomicsReadOnlyGate.Inspect(
            inventory ?? Inventory(), Product(), quote ?? Quote(), version ?? Version(),
            approval ?? Approval(), outcome ?? Outcome(),
            recommendations ?? Recommendations(), binding ?? Binding());

    [Fact]
    public void Exactly_reconciled_accepted_Economics_object_graph_is_only_a_synthetic_internal_link()
    {
        var result = Evaluate();
        Assert.Equal("OFFLINE_SYNTHETIC_ONLY", result.Mode);
        Assert.Equal(CommercialEconomicsReadOnlyGate.LinkedFixture, result.Status);
        Assert.Equal(QuoteId.ToString(), result.QuoteId);
        Assert.Equal(VersionId.ToString(), result.QuoteVersionId);
        Assert.Equal("ARE-P01", result.ProductId);
        Assert.Equal("V1", result.ProductVersion);
        Assert.Equal("G1", result.GeometryVersion);
        Assert.Equal(5, result.RequestedOccurrences);
        Assert.True(result.VersionAndAmountConsistent);
        Assert.False(result.RealProductBindingVerified);
        Assert.False(result.CanStatePriceToCustomer);
        Assert.False(result.AutomatedNegotiationAuthorized);
        Assert.False(result.ReservationAuthorized);
        Assert.False(result.PaymentAuthorized);
        Assert.Equal("NOT_SENT", result.DeliveryStatus);
        Assert.Equal(64, result.QuoteEvidenceFingerprintSha256.Length);
        Assert.DoesNotContain("200 PHP", result.SafeCustomerReply);
        Assert.Contains("Human approval", result.SafeCustomerReply);
    }

    [Fact]
    public void T05_five_vs_twenty_occurrences_must_match_every_existing_quote_line()
    {
        var mismatch = Evaluate(binding: Binding(20));
        Assert.Equal(CommercialEconomicsReadOnlyGate.Unavailable, mismatch.Status);
        Assert.False(mismatch.CanStatePriceToCustomer);
        var twenty = Evaluate(version: Version(20), outcome: Outcome(20), binding: Binding(20));
        Assert.Equal(CommercialEconomicsReadOnlyGate.LinkedFixture, twenty.Status);
        Assert.Equal(20, twenty.RequestedOccurrences);
        Assert.NotEqual(Evaluate().QuoteEvidenceFingerprintSha256, twenty.QuoteEvidenceFingerprintSha256);
    }

    [Fact]
    public void T08_missing_linked_quote_proof_or_unapproved_quote_withholds_price()
    {
        var draft = Evaluate(quote: new Quote { Id = QuoteId, Status = QuoteStatuses.Draft, CurrentVersionNumber = 1, AdvertiserOpportunityId = OpportunityId, CurrencyCode = "PHP" });
        Assert.Equal(CommercialEconomicsReadOnlyGate.Unavailable, draft.Status);
        var rejected = Evaluate(approval: new QuoteApprovalDecision { QuoteId = QuoteId, QuoteVersionId = VersionId, Decision = QuoteApprovalDecisions.Rejected, ReviewerLabel = "FICTIONAL_REVIEWER", Rationale = "SYNTHETIC TEST" });
        Assert.Equal(CommercialEconomicsReadOnlyGate.Unavailable, rejected.Status);
        var declined = Evaluate(outcome: new QuoteOutcome { QuoteId = QuoteId, QuoteVersionId = VersionId, Response = QuoteOutcomeResponses.Declined, Amount = 200m, CurrencyCode = "PHP" });
        Assert.Equal(CommercialEconomicsReadOnlyGate.Unavailable, declined.Status);
        var scope = Evaluate(binding: Binding() with { SourceReference = "unverified-external-link" });
        Assert.Equal(CommercialEconomicsReadOnlyGate.Unavailable, scope.Status);
    }

    [Fact]
    public void A_previous_quote_version_or_inconsistent_accepted_amount_cannot_be_spoken()
    {
        Assert.Equal(CommercialEconomicsReadOnlyGate.Unavailable,
            Evaluate(quote: new Quote { Id = QuoteId, Status = QuoteStatuses.Accepted, CurrentVersionNumber = 2, AdvertiserOpportunityId = OpportunityId, CurrencyCode = "PHP" }).Status);
        Assert.Equal(CommercialEconomicsReadOnlyGate.Unavailable,
            Evaluate(outcome: new QuoteOutcome { QuoteId = QuoteId, QuoteVersionId = Guid.NewGuid(), Response = QuoteOutcomeResponses.Accepted, Amount = 200m, CurrencyCode = "PHP" }).Status);
        Assert.Equal(CommercialEconomicsReadOnlyGate.Unavailable,
            Evaluate(outcome: new QuoteOutcome { QuoteId = QuoteId, QuoteVersionId = VersionId, Response = QuoteOutcomeResponses.Accepted, Amount = 1m, CurrencyCode = "PHP" }).Status);
        Assert.Equal(CommercialEconomicsReadOnlyGate.Unavailable,
            Evaluate(version: new QuoteVersion { Id = VersionId, QuoteId = QuoteId, VersionNumber = 1, CurrencyCode = "PHP", SubtotalAmount = 200m, TotalAmount = 195m, LineItems = Version().LineItems }).Status);
    }

    [Fact]
    public void Cross_creator_cross_opportunity_and_slot_swap_are_all_denied()
    {
        var recs = Recommendations().ToArray();
        recs[0].CreatorId = Guid.NewGuid();
        Assert.Equal(CommercialEconomicsReadOnlyGate.Unavailable,
            Evaluate(recommendations: recs).Status);

        Assert.Equal(CommercialEconomicsReadOnlyGate.Unavailable,
            Evaluate(quote: new Quote { Id = QuoteId, Status = QuoteStatuses.Accepted, CurrentVersionNumber = 1, AdvertiserOpportunityId = Guid.NewGuid(), CurrencyCode = "PHP" }).Status);

        var badSlot = Binding() with
        {
            Slots = Binding().Slots.Select((x, i) => i == 0
                ? x with { AdInventorySlotId = Guid.NewGuid() } : x).ToArray()
        };
        Assert.Equal(CommercialEconomicsReadOnlyGate.Unavailable,
            Evaluate(binding: badSlot).Status);
    }

    [Fact]
    public void Currency_or_duplicate_ledger_lines_fail_closed()
    {
        Assert.Equal(CommercialEconomicsReadOnlyGate.Unavailable,
            Evaluate(outcome: new QuoteOutcome { QuoteId = QuoteId, QuoteVersionId = VersionId, Response = QuoteOutcomeResponses.Accepted, Amount = 200m, CurrencyCode = "USD" }).Status);

        var wrong = Version();
        var rows = wrong.LineItems.ToArray();
        wrong.LineItems = new List<QuoteLineItem> { rows[0], rows[0] };
        Assert.Equal(CommercialEconomicsReadOnlyGate.Unavailable,
            Evaluate(version: wrong).Status);

        var wrongQuantity = Version();
        wrongQuantity.LineItems.First().Quantity = 20m;
        Assert.Equal(CommercialEconomicsReadOnlyGate.Unavailable,
            Evaluate(version: wrongQuantity).Status);
    }

    [Fact]
    public void No_geometry_means_no_proof_even_when_the_economics_object_graph_is_valid()
    {
        var unverified = Inventory() with { Geometry = null };
        var decision = Evaluate(inventory: unverified);
        Assert.Equal(CommercialEconomicsReadOnlyGate.Unavailable, decision.Status);
        Assert.False(decision.RealProductBindingVerified);
        Assert.Equal("NOT_SENT", decision.DeliveryStatus);
    }

    [Fact]
    public void No_real_negotiation_or_booking_can_be_created_by_a_consistent_fixture()
    {
        var decision = Evaluate();
        Assert.False(decision.AutomatedNegotiationAuthorized);
        Assert.False(decision.PaymentAuthorized);
        Assert.False(decision.ReservationAuthorized);
        Assert.False(decision.CanStatePriceToCustomer);
        Assert.Equal("PRICING_UNAVAILABLE",
            CommercialEconomicsReadOnlyGate.Inspect(
                Inventory(), Product(), null, null, null, null, null, null).Status);
    }
}
