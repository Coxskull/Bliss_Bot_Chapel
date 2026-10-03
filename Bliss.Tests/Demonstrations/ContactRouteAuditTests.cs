using Bliss.Domain.Demonstrations;

namespace Bliss.Tests.Demonstrations;

public sealed class ContactRouteAuditTests
{
    [Fact]
    public void Suppression_stale_evidence_and_a_missing_road_withhold_the_route()
    {
        var reading = ContactRouteAudit.Read(
        [
            new StoredRoute("Puerto Azul", true, "CURRENT", "https://example.com/puerto-azul/team", 2, false),
            new StoredRoute("Casa Verde", false, "STALE", "https://example.com/casa-verde/team", 1, true),
            new StoredRoute("Mesa Norte", false, "CURRENT", "", 1, false),
            new StoredRoute("Andes Table", false, "CURRENT", "https://example.com/andes-table/team", 0, false),
            new StoredRoute("  ", false, "CURRENT", "https://example.com/blank", 0, false)
        ]);

        Assert.False(reading.GreenMeansSend);
        Assert.False(reading.CensusClaimed);
        Assert.Equal("NOT_SENT", reading.Delivery);
        Assert.Contains("not a census", reading.Notice);
        Assert.Contains("not permission to send", reading.Notice);
        Assert.Equal(4, reading.Stored);
        Assert.Equal(1, reading.Suppressed);
        Assert.Equal(1, reading.Stale);
        Assert.Equal(2, reading.MissingEvidence);
        Assert.Equal(1, reading.NoRoad);
        Assert.Equal(0, reading.Preview);
        Assert.Equal("Andes Table", reading.Routes[0].BusinessName);
        Assert.Equal(ContactRouteAudit.NoRoadRoute, reading.Routes[0].Route);
        Assert.Equal(ContactRouteAudit.StaleRoute, reading.Routes[1].Route);
        Assert.Equal(ContactRouteAudit.NoAdapter, reading.Routes[1].Adapter);
        Assert.Equal("NOT_SENT", reading.Routes[1].Transmission);
        Assert.Equal(ContactRouteAudit.MissingEvidenceRoute, reading.Routes[2].Route);
        Assert.Contains("None was invented", reading.Routes[2].Notice);
        Assert.Equal(ContactRouteAudit.SuppressedRoute, reading.Routes[3].Route);
        Assert.Contains("Suppression comes before the adapter", reading.Routes[3].Notice);
    }

    [Fact]
    public void A_current_preview_uses_the_provider_neutral_adapter_and_does_not_send()
    {
        var reading = ContactRouteAudit.Read(
        [
            new StoredRoute("Mesa Norte", false, "CURRENT", "https://example.com/mesa-norte/team", 1, true)
        ]);

        var row = Assert.Single(reading.Routes);
        Assert.Equal(ContactRouteAudit.PreviewRoute, row.Route);
        Assert.Equal(DeliveryPolicy.PreviewAdapter, row.Adapter);
        Assert.Equal("NOT_SENT", row.Transmission);
        Assert.Contains("Transmission remains NOT_SENT", row.Notice);
        Assert.Equal(1, reading.Preview);

        var held = ContactRouteAudit.Read(
        [
            new StoredRoute("Mesa Norte", false, "CURRENT", "https://example.com/mesa-norte/team", 1, false)
        ]);
        Assert.Equal(ContactRouteAudit.WithheldRoute, Assert.Single(held.Routes).Route);
        Assert.Equal(ContactRouteAudit.NoAdapter, Assert.Single(held.Routes).Adapter);
        Assert.Contains("was not authorized", Assert.Single(held.Routes).Notice);
    }

    [Fact]
    public void An_explicit_transmission_request_is_recorded_once_and_not_sent()
    {
        var first = ContactRouteAudit.Authorize("Send the message", "operator-transmission-1", []);
        Assert.True(first.Accepted);
        Assert.True(first.Written);
        Assert.False(first.Duplicate);
        Assert.Equal(ContactRouteAudit.NoAdapter, first.Adapter);
        Assert.Equal("NOT_SENT", first.Transmission);
        Assert.Equal("NOT_SENT", first.Delivery);
        Assert.False(first.GreenMeansSend);
        Assert.Contains("No adapter transmitted it", first.Notice);

        var again = ContactRouteAudit.Authorize(
            "Send it through smtp",
            "operator-transmission-1",
            [new StoredTransmissionAudit("operator-transmission-1", "NOT_SENT")]);
        Assert.True(again.Duplicate);
        Assert.False(again.Written);
        Assert.Equal("NOT_SENT", again.Transmission);
        Assert.Equal(ContactRouteAudit.DuplicateRequest, again.Notice);

        var blank = ContactRouteAudit.Authorize("  ", "operator-transmission-1", []);
        Assert.False(blank.Accepted);
        Assert.False(blank.Written);
        Assert.Equal(ContactRouteAudit.BlankAuthorization, blank.Notice);

        var key = ContactRouteAudit.Authorize("Send the message", "no", []);
        Assert.False(key.Accepted);
        Assert.Equal(ContactRouteAudit.BlankKey, key.Notice);
        Assert.Equal(ContactRouteAudit.AddressWithheld, ContactRouteAudit.DisplayAuthorization("owner@example.com"));
    }

    [Fact]
    public void A_missing_list_is_refused()
    {
        var read = Assert.Throws<InvalidOperationException>(() => ContactRouteAudit.Read(null));
        Assert.Contains("None is invented", read.Message);
        var authorize = Assert.Throws<InvalidOperationException>(() => ContactRouteAudit.Authorize("Send the message", "operator-transmission-1", null));
        Assert.Contains("None is invented", authorize.Message);
    }

    [Fact]
    public void Route_source_does_not_transmit()
    {
        var source = File.ReadAllText(Path.Combine(
            AppContext.BaseDirectory, "..", "..", "..", "..", "Bliss.Domain", "Demonstrations", "ContactRouteAudit.cs"));
        Assert.Contains("not a census", source);
        Assert.Contains("NOT_SENT", source);
        Assert.DoesNotContain("Smtp", source);
        Assert.DoesNotContain("HttpClient", source);
        Assert.DoesNotContain("OpenAI", source);
        Assert.DoesNotContain("MailMessage", source);
        Assert.DoesNotContain("$", source);
    }
}
