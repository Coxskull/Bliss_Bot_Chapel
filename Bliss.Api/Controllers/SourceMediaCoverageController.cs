using Bliss.Api.Demonstrations;
using Bliss.Domain.Demonstrations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Bliss.Api.Controllers;

[ApiController]
[Route("api/operations/fuel")]
public sealed class SourceMediaCoverageController(ProspectDemonstrationService demonstrations) : ControllerBase
{
    [HttpGet]
    [AllowAnonymous]
    public ActionResult Read()
    {
        var library = demonstrations.Library();
        var reading = SourceMediaCoverage.Read(library.Clips.Select(clip => new StoredSlice(
            clip.Market,
            clip.Country,
            clip.Status,
            clip.QuotaCredit,
            clip.Sha256,
            clip.FrameHash,
            clip.Provenance)));
        return Ok(new
        {
            reading.Notice,
            reading.Delivery,
            reading.GreenMeansSend,
            fuel = new
            {
                reading.Fuel.DailyTarget,
                reading.Fuel.Submitted,
                reading.Fuel.QualifiedUnique,
                reading.Fuel.Duplicates,
                reading.Fuel.Rejected,
                reading.Fuel.ReplacementRequired,
                reading.Fuel.DailyRemaining,
                reading.Fuel.FuelStatus
            },
            markets = reading.Markets.Select(item => new
            {
                item.Market,
                item.CountryLine,
                item.QualifiedSlices,
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
