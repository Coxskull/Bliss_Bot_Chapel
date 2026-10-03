using Bliss.Api.Demonstrations;
using Bliss.Domain.Demonstrations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Bliss.Api.Controllers;

[ApiController]
[Route("api/operations/discovery")]
public sealed class AdvertiserDiscoveryController(ProspectDemonstrationService demonstrations) : ControllerBase
{
    [HttpGet]
    [AllowAnonymous]
    public ActionResult Read()
    {
        var library = demonstrations.Library();
        var reading = AdvertiserDiscovery.Read(library.Demonstrations.Select(item => new StoredProspect(
            item.Slug,
            item.BusinessName,
            item.Market,
            item.PublicSourceUrl,
            item.OpportunityScore,
            item.ProspectState)).ToList());
        return Ok(new
        {
            reading.Notice,
            reading.Delivery,
            reading.GreenMeansSend,
            reading.CensusClaimed,
            reading.Stored,
            reading.Scored,
            reading.Preserved,
            prospects = reading.Prospects.Select(item => new
            {
                item.BusinessName,
                item.Market,
                item.SourceUrl,
                item.Score,
                item.State,
                item.Notice
            }),
            withheld = reading.Withheld.Select(item => new
            {
                item.Reason,
                item.Count
            })
        });
    }
}
