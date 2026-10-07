using Bliss.Api.Operations;
using Bliss.Domain.AdvertisingRealEstate;
using Bliss.Domain.CreativeAcademy;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Bliss.Api.Controllers;

[ApiController]
[Route("api/operations/blueprint")]
public sealed class BlueprintPhaseController(BlueprintPhaseService phases) : ControllerBase
{
    [HttpGet]
    [AllowAnonymous]
    public async Task<ActionResult> Read(CancellationToken cancellationToken)
    {
        var reading = await phases.ReadAsync(cancellationToken);
        var retrieval = phases.Retrieve("auto-parts", reading.References, ["lighting", "typography"]);
        return Ok(Body(reading, retrieval, null));
    }

    [HttpPost("ask")]
    [AllowAnonymous]
    public async Task<ActionResult> Ask([FromBody] BlueprintAskRequest? request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request?.Message))
        {
            return BadRequest(new
            {
                error = "Ask Alpha needs the advertiser's words. No inventory was invented.",
                inventedProduct = false,
                inventedPrice = false,
                modelCalls = 0,
                campaignReady = false,
                delivery = "NOT_SENT"
            });
        }

        var turn = await phases.AskAsync(request.Message, cancellationToken);
        var reading = await phases.ReadAsync(cancellationToken);
        return Ok(Body(reading, null, turn));
    }

    [HttpGet("references/{referenceId}/image")]
    [AllowAnonymous]
    public ActionResult ReferenceImage(string referenceId) => Image(phases.OpenReference(referenceId));

    [HttpGet("showcases/{showcaseId}/image")]
    [AllowAnonymous]
    public ActionResult ShowcaseImage(string showcaseId) => Image(phases.OpenShowcase(showcaseId));

    [HttpGet("unassigned/{*fileName}")]
    [AllowAnonymous]
    public ActionResult UnassignedImage(string fileName) => Image(phases.OpenUnassigned(fileName));

    private ActionResult Image(BlueprintFile? file)
    {
        if (file is null)
        {
            return NotFound(new
            {
                error = "No stored image matches that id. Nothing was invented.",
                invented = false,
                delivery = "NOT_SENT"
            });
        }

        return PhysicalFile(file.FullPath, file.ContentType);
    }

    private static object Body(BlueprintReading reading, ReferenceRetrieval? retrieval, CatalogTurn? turn)
    {
        var uploaded = reading.References.Count(item => item.AssetPresent);
        return new
        {
            amendmentStatus = reading.Catalog.AmendmentStatus,
            notice = reading.Catalog.Notice,
            modelCalls = 0,
            campaignReady = false,
            greenMeansSend = false,
            delivery = "NOT_SENT",
            economics = reading.Catalog.Economics,
            creatorAuthorized = reading.Catalog.CreatorAuthorized,
            geometryStatus = RealEstateCatalog.Unrecorded,
            slots = reading.Catalog.Slots,
            products = reading.Catalog.Products,
            showcases = reading.Catalog.Showcases,
            deliveries = reading.Catalog.Deliveries,
            qualityDnaVersion = ReferenceLibrary.QualityDnaVersion,
            qualityDna = reading.QualityDna,
            doNotProduce = reading.DoNotProduce,
            regressionBriefs = reading.RegressionBriefs,
            regressionStatus = "BASELINE_NOT_RECORDED",
            references = new
            {
                expected = 50,
                registered = reading.References.Count,
                uploaded,
                awaitingUpload = reading.References.Count - uploaded,
                active = reading.References.Count(item => item.Lifecycle == ReferenceLibrary.Active && item.AssetPresent),
                items = reading.References
            },
            unassigned = reading.Unassigned,
            ownerNeeds = new
            {
                source = "assets/alpha-prototypes/OWNER-NEEDS.tsv",
                open = reading.OwnerNeeds.Count(item => item.Status == OwnerNeeds.Open),
                supplied = reading.OwnerNeeds.Count(item => item.Status == OwnerNeeds.Supplied),
                items = reading.OwnerNeeds
            },
            quality = new
            {
                version = ReferenceLibrary.QualityDnaVersion,
                status = ReferenceLibrary.Unclassified,
                attributes = ReferenceLibrary.UnclassifiedAttributes(),
                allowedGrades = ReferenceLibrary.QualityGrades,
                lifecycles = ReferenceLibrary.Lifecycles
            },
            sampleRetrieval = retrieval,
            turn
        };
    }
}

public sealed record BlueprintAskRequest(string? Message);
