using System.Security.Claims;
using Bliss.Api.Controllers;
using Bliss.Api.Security;
using Bliss.Domain.Scouting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;

namespace Bliss.Tests.Scouting;

public sealed class ScoutCompatibilityReviewTests
{
    private static ScoutReviewPair Pair(
        string creatorCountry = "MY",
        string creatorCity = "Kuala Lumpur",
        string creatorLocation = "PUBLIC_SOURCE_SUPPORTED",
        string creatorVerification = "SOURCE_VERIFIED") => new(
        new ScoutReviewProspect(
            "AGM-MKT-20261024-CR-MY-AIDIDITMYWAY", "AididitMyWay", creatorCountry,
            creatorCity, "Lifestyle interview podcast", "https://aididitmyway.com/podcast",
            "INDEPENDENT_PUBLIC_WEB_REVIEW", creatorVerification, creatorLocation,
            "ALPHA-EV-20261024-MARKET001"),
        new ScoutReviewProspect(
            "AGM-MKT-20261024-AD-MY-LOKL-COFFEE", "LOKL Coffee", "MY",
            "Kuala Lumpur", "Cafe", "https://loklcoffee.com/",
            "INDEPENDENT_PUBLIC_WEB_REVIEW", "SOURCE_VERIFIED", "PUBLIC_SOURCE_SUPPORTED",
            "ALPHA-EV-20261024-MARKET001"));

    [Fact]
    public void Same_city_creates_only_an_unattested_review_candidate()
    {
        var finding = ScoutCompatibilityReview.Evaluate(Pair());
        Assert.Equal("GEOGRAPHIC_CANDIDATE_REVIEW_ONLY", finding.ReviewStatus);
        Assert.Equal("CALLER_SUBMITTED_SOURCE_CLAIMS_NOT_ATTESTED", finding.SourceVerification);
        Assert.Equal("NOT_SENT", finding.Delivery);
        Assert.False(finding.IsCommercialMatch);
        Assert.False(finding.IsOutreachAuthorized);
        Assert.Equal("NOT_VERIFIED", finding.InventoryAvailability);
        Assert.Equal("UNKNOWN_ADVERTISER_FORMAT_REQUIREMENTS", finding.AdvertisingFormatCompatibility);
    }

    [Theory]
    [InlineData("PH", "Manila", "PUBLIC_SOURCE_SUPPORTED", "SOURCE_VERIFIED")]
    [InlineData("MY", "Kuala Lumpur", "NEEDS_REVIEW", "SOURCE_VERIFIED")]
    [InlineData("MY", "Kuala Lumpur", "PUBLIC_SOURCE_SUPPORTED", "NEEDS_REVIEW")]
    public void Country_or_verification_gaps_never_recommend_a_pair(
        string country, string city, string location, string verification)
    {
        var result = ScoutCompatibilityReview.Evaluate(Pair(country, city, location, verification));
        Assert.Equal("INSUFFICIENT_EVIDENCE_REVIEW_ONLY", result.ReviewStatus);
        Assert.False(result.IsCommercialMatch);
    }

    [Fact]
    public void Untrusted_insecure_source_is_rejected()
    {
        var p = Pair();
        var altered = p with { Creator = p.Creator with { PublicSourceUrl = "http://example.com/" } };
        Assert.Throws<InvalidOperationException>(() => ScoutCompatibilityReview.Evaluate(altered));
    }

    [Fact]
    public void Endpoint_is_closed_in_production_even_with_signed_in_user()
    {
        var controller = Controller("Production", true, true);
        var response = controller.Preview(Pair());
        Assert.Equal(403, Assert.IsType<ObjectResult>(response.Result).StatusCode);
    }

    [Fact]
    public void Endpoint_requires_auth_enabled_and_a_signed_in_identity_in_staging()
    {
        Assert.Equal(403, Assert.IsType<ObjectResult>(Controller("Staging", false, true).Preview(Pair()).Result).StatusCode);
        Assert.Equal(403, Assert.IsType<ObjectResult>(Controller("Staging", true, false).Preview(Pair()).Result).StatusCode);
        var allowed = Controller("Staging", true, true).Preview(Pair());
        var response = Assert.IsType<OkObjectResult>(allowed.Result);
        Assert.IsType<ScoutCompatibilityFinding>(response.Value);
    }

    private static ScoutCompatibilityPreviewController Controller(
        string environment, bool authEnabled, bool loggedIn)
    {
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
            loggedIn ? [new Claim(ClaimTypes.NameIdentifier, "approved-staging-operator")] : [],
            loggedIn ? "TestIdentity" : null));
        return new ScoutCompatibilityPreviewController(
            new FakeHostEnvironment { EnvironmentName = environment },
            new BlissAuthenticationOptions { Enabled = authEnabled })
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = principal }
            }
        };
    }

    private sealed class FakeHostEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Staging";
        public string ApplicationName { get; set; } = "Bliss.Tests";
        public string ContentRootPath { get; set; } = "/";
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
