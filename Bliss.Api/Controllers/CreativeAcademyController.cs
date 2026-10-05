using Bliss.Api.Operations;
using Bliss.Api.Runtime;
using Bliss.Api.Security;
using Bliss.Domain.CreativeAcademy;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Bliss.Api.Controllers;

[ApiController]
[Route("api/operations/academy")]
public sealed class CreativeAcademyController(CreativeAcademyService academy) : ControllerBase
{
    [HttpGet]
    [AllowAnonymous]
    public async Task<ActionResult> Read(CancellationToken cancellationToken)
    {
        var board = await academy.ReadAsync(cancellationToken);
        return Ok(Body(board, null));
    }

    [HttpPost("curriculum")]
    [Authorize(Policy = BlissAuthorization.WritePolicy)]
    [EnableRateLimiting(BlissRateLimitPolicies.Writes)]
    public async Task<ActionResult> Curriculum([FromBody] AcademyCurriculumRequest? request, CancellationToken cancellationToken)
    {
        try
        {
            var write = await academy.StoreCurriculumAsync(request?.CurriculumKey, cancellationToken);
            var board = await academy.ReadAsync(cancellationToken);
            return Ok(Body(board, write));
        }
        catch (InvalidOperationException ex)
        {
            return Refuse(ex);
        }
    }

    [HttpPost("dna")]
    [Authorize(Policy = BlissAuthorization.WritePolicy)]
    [EnableRateLimiting(BlissRateLimitPolicies.Writes)]
    public async Task<ActionResult> Dna([FromBody] AcademyDnaRequest? request, CancellationToken cancellationToken)
    {
        try
        {
            var write = await academy.StoreDnaAsync(
                request?.DnaKey,
                request?.Family,
                request?.BrandName,
                request?.Hero,
                request?.Palette,
                request?.Cta,
                request?.Personality,
                request?.CallModel ?? false,
                cancellationToken);
            var board = await academy.ReadAsync(cancellationToken);
            return Ok(Body(board, write));
        }
        catch (InvalidOperationException ex)
        {
            return Refuse(ex);
        }
    }

    [HttpPost("visual")]
    [Authorize(Policy = BlissAuthorization.WritePolicy)]
    [EnableRateLimiting(BlissRateLimitPolicies.Writes)]
    public async Task<ActionResult> Visual([FromBody] AcademyVisualRequest? request, CancellationToken cancellationToken)
    {
        try
        {
            var write = await academy.RecordVisualAsync(
                request?.LessonKey,
                request?.VisualBenchmarkMet,
                request?.VisualNote,
                request?.CallModel ?? false,
                request?.CampaignReady ?? false,
                cancellationToken);
            var board = await academy.ReadAsync(cancellationToken);
            return Ok(Body(board, write));
        }
        catch (InvalidOperationException ex)
        {
            return Refuse(ex);
        }
    }

    [HttpPost("production-brief")]
    [Authorize(Policy = BlissAuthorization.WritePolicy)]
    [EnableRateLimiting(BlissRateLimitPolicies.Writes)]
    public ActionResult ProductionBrief([FromBody] AcademyProductionRequest? request)
    {
        try
        {
            return Ok(CreativeAcademy.PrepareProductionBrief(
                request?.Family,
                request?.BrandName,
                request?.Palette,
                request?.FontFamily,
                request?.Headline,
                request?.Cta,
                request?.Market,
                request?.Language,
                request?.Requirements,
                request?.MarketResearchComplete ?? false,
                request?.ApprovedPeopleProvided ?? false,
                request?.CallModel ?? false));
        }
        catch (InvalidOperationException ex)
        {
            return Refuse(ex);
        }
    }

    [HttpPost("parity")]
    [Authorize(Policy = BlissAuthorization.WritePolicy)]
    [EnableRateLimiting(BlissRateLimitPolicies.Writes)]
    public ActionResult Parity([FromBody] AcademyParityRequest? request) =>
        Ok(CreativeAcademy.EvaluateFinalGates(
            request?.CustomizationCompliance,
            request?.QualityParity,
            request?.Originality,
            request?.GeographicAuthenticity));

    private static ActionResult Refuse(InvalidOperationException ex) =>
        new BadRequestObjectResult(new
        {
            error = ex.Message,
            delivery = "NOT_SENT",
            greenMeansSend = false,
            modelCalls = 0,
            campaignReady = false
        });

    private static object Body(AcademyBoard board, AcademyWrite? write) => new
    {
        notice = write?.Notice ?? CreativeAcademy.Notice,
        delivery = "NOT_SENT",
        greenMeansSend = false,
        duplicate = write?.Duplicate ?? false,
        written = write?.Written ?? false,
        modelCalls = 0,
        campaignReady = false,
        purpose = "Produce original advertisements at or above the approved reference quality class.",
        doctrine = "Reproduce the craftsmanship. Replace the creative content. Originality without quality parity fails. Quality parity without originality fails.",
        referenceParityGate = CreativeAcademy.ReferenceParityGate,
        masterPrototype = new
        {
            lessonKey = CreativeAcademy.PharmacyTeacher,
            designation = "ALPHA MASTER PROTOTYPE 01/50",
            niche = "PHARMACY / DRUGSTORE",
            purpose = "QUALITY ANCHOR",
            creativeTemplate = false
        },
        lessons = board.Lessons.Select(Lesson),
        dna = board.Dna.Select(Dna)
    };

    private static object Lesson(Bliss.Domain.Entities.CreativeAcademyLessonRow item) => new
    {
        item.LessonKey,
        item.Role,
        item.Family,
        item.BrandName,
        item.Headline,
        item.Body,
        item.ImagePath,
        item.Status,
        item.Score,
        item.Critical,
        item.VisualRecorded,
        defects = Split(item.Defects),
        preserve = Split(item.PreserveList),
        repair = Split(item.RepairList),
        item.ModelCalls,
        item.CampaignReady,
        item.Notice,
        item.Delivery
    };

    private static object Dna(Bliss.Domain.Entities.CreativeAcademyDnaRow item) => new
    {
        item.DnaKey,
        item.Family,
        item.BrandName,
        item.Hero,
        item.Palette,
        item.Cta,
        item.Personality,
        item.TeacherKey,
        item.TeacherOnFile,
        item.Distinct,
        item.ModelCalls,
        item.Notice,
        item.Delivery
    };

    private static string[] Split(string value) =>
        string.IsNullOrWhiteSpace(value)
            ? []
            : value.Split('\n', StringSplitOptions.RemoveEmptyEntries);
}

public sealed record AcademyCurriculumRequest(string? CurriculumKey);

public sealed record AcademyDnaRequest(
    string? DnaKey,
    string? Family,
    string? BrandName,
    string? Hero,
    string? Palette,
    string? Cta,
    string? Personality,
    bool? CallModel);

public sealed record AcademyVisualRequest(
    string? LessonKey,
    bool? VisualBenchmarkMet,
    string? VisualNote,
    bool? CallModel,
    bool? CampaignReady);

public sealed record AcademyProductionRequest(
    string? Family,
    string? BrandName,
    string? Palette,
    string? FontFamily,
    string? Headline,
    string? Cta,
    string? Market,
    string? Language,
    string? Requirements,
    bool? MarketResearchComplete,
    bool? ApprovedPeopleProvided,
    bool? CallModel);

public sealed record AcademyParityRequest(
    bool? CustomizationCompliance,
    bool? QualityParity,
    bool? Originality,
    bool? GeographicAuthenticity);
