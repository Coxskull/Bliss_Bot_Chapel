using Bliss.Domain.Demonstrations;

namespace Bliss.Api.Demonstrations;

public sealed class ProspectDemonstrationService(ProspectDemonstrationStore store, DemonstrationVideoStudio studio)
{
    public LibraryDocument Library() => store.Read();

    public async Task<SourceClipRecord> SaveUploadAsync(
        string originalFileName,
        byte[] bytes,
        string market,
        string country,
        string culture,
        string provenance,
        CancellationToken cancellationToken)
    {
        var temporary = Path.Combine(store.ClipsDirectory, Guid.NewGuid().ToString("N") + ".upload");
        await File.WriteAllBytesAsync(temporary, bytes, cancellationToken);
        try
        {
            return await ClassifyFileAsync(temporary, originalFileName, market, country, culture, provenance, cancellationToken);
        }
        finally
        {
            if (File.Exists(temporary))
            {
                File.Delete(temporary);
            }
        }
    }

    public async Task<SourceClipRecord> CreateStudioSliceAsync(
        string market,
        string country,
        int seconds,
        CancellationToken cancellationToken)
    {
        var temporary = Path.Combine(store.ClipsDirectory, Guid.NewGuid().ToString("N") + ".mp4");
        await studio.CreateStudioSliceAsync(temporary, market, country, seconds, cancellationToken);
        return await ClassifyFileAsync(
            temporary,
            "alpha-studio-slice.mp4",
            market,
            country,
            "original studio",
            "Original Alpha studio slice. Not a copied podcast.",
            cancellationToken);
    }

    public async Task<DemonstrationRecord> ProduceReferenceAsync(
        string publicBaseUrl,
        Guid? sourceClipId,
        int conceptCount,
        CancellationToken cancellationToken)
    {
        var library = store.Read();
        var clip = sourceClipId is Guid id
            ? library.Clips.FirstOrDefault(x => x.Id == id)
            : library.Clips.LastOrDefault(x => x.Status == SourceMediaStatus.Qualified);
        if (clip is null || clip.Status != SourceMediaStatus.Qualified)
        {
            throw new InvalidOperationException("A qualified source slice is required before an overlay can be made.");
        }

        var count = Math.Clamp(conceptCount, 1, ReferenceProspect.Concepts.Length);
        var selected = ReferenceProspect.Concepts.Take(count).ToArray();
        var demonstration = NewReferenceRecord();
        foreach (var concept in selected)
        {
            var destination = publicBaseUrl.TrimEnd('/') + "/demonstrations/" + ReferenceProspect.Slug;
            var qrName = ReferenceProspect.Slug + "-" + concept.Id + ".png";
            var videoName = ReferenceProspect.Slug + "-" + concept.Id + ".mp4";
            var qrPath = store.RenderPath(qrName);
            var videoPath = store.RenderPath(videoName);
            await File.WriteAllBytesAsync(qrPath, studio.CreateQrPng(destination), cancellationToken);
            await studio.CompositeAsync(
                store.ClipPath(clip),
                qrPath,
                videoPath,
                concept,
                demonstration.BusinessName,
                cancellationToken);
            demonstration.Concepts.Add(new ConceptRecord
            {
                Id = concept.Id,
                Name = concept.Name,
                Headline = concept.Headline,
                Subhead = concept.Subhead,
                Detail = concept.Detail,
                CallToAction = concept.CallToAction,
                VideoFileName = videoName,
                QrFileName = qrName,
                QrDestination = destination,
                SourceClipId = clip.Id
            });
        }

        demonstration.Messages.Add(new ChatRecord
        {
            Role = "ASSISTANT",
            Text = "Hello. This is a private demonstration for ABC Pharmacy in Panama City. "
                + "I can explain the concepts and how Alpha works. No named decision-maker is verified, so I will not use a personal name.",
            Signal = "INTRODUCED",
            At = DateTime.UtcNow
        });

        store.Update(document =>
        {
            document.Demonstrations.RemoveAll(x => x.Slug == ReferenceProspect.Slug);
            document.Demonstrations.Add(demonstration);
        });
        return demonstration;
    }

    public DemonstrationRecord? Find(string slug) =>
        store.Read().Demonstrations.FirstOrDefault(x => x.Slug == slug);

    public string? RenderFile(string slug, string conceptId, bool qr)
    {
        var concept = Find(slug)?.Concepts.FirstOrDefault(x => x.Id == conceptId);
        if (concept is null)
        {
            return null;
        }

        var fileName = qr ? concept.QrFileName : concept.VideoFileName;
        var path = Path.GetFullPath(store.RenderPath(Path.GetFileName(fileName)));
        var root = Path.GetFullPath(store.RendersDirectory);
        return path.StartsWith(root, StringComparison.Ordinal) && File.Exists(path) ? path : null;
    }

    public ChatRecord Converse(string slug, string message)
    {
        var demonstration = Find(slug) ?? throw new InvalidOperationException("Demonstration not found.");
        var facts = new ProspectFacts(
            demonstration.BusinessName,
            demonstration.ContactTier,
            demonstration.ContactRoute,
            BuyingRoleCatalog.RolesFor(demonstration.Niche),
            demonstration.Concepts.Select(x => x.Name).ToArray());
        var turn = DemonstrationConversation.Reply(facts, message);
        var visitor = new ChatRecord
        {
            Role = "VISITOR",
            Text = message.Trim(),
            Signal = "VISITOR",
            At = DateTime.UtcNow
        };
        var assistant = new ChatRecord
        {
            Role = "ASSISTANT",
            Text = turn.Reply,
            Signal = turn.Signal,
            At = DateTime.UtcNow
        };
        store.Update(document =>
        {
            var current = document.Demonstrations.First(x => x.Slug == slug);
            current.Messages.Add(visitor);
            current.Messages.Add(assistant);
            current.LastSignal = turn.Signal;
            current.HumanEscalation = current.HumanEscalation || turn.HumanEscalation;
        });
        return assistant;
    }

    private async Task<SourceClipRecord> ClassifyFileAsync(
        string temporaryPath,
        string originalFileName,
        string market,
        string country,
        string culture,
        string provenance,
        CancellationToken cancellationToken)
    {
        var bytes = await File.ReadAllBytesAsync(temporaryPath, cancellationToken);
        var sha = DemonstrationVideoStudio.Sha256(bytes);
        var duration = await studio.ProbeDurationAsync(temporaryPath, cancellationToken);
        var frameHash = await studio.FrameHashAsync(temporaryPath, cancellationToken);
        var library = store.Read();
        var exact = library.Clips.Any(x => x.Sha256 == sha && x.Status == SourceMediaStatus.Qualified);
        var distance = SourceMediaRules.ClosestFrameDistance(
            frameHash,
            library.Clips.Where(x => x.Status == SourceMediaStatus.Qualified).Select(x => x.FrameHash));
        var status = SourceMediaRules.Classify(exact, distance, SourceMediaRules.DurationIsUsable(duration));
        var storedName = Guid.NewGuid().ToString("N") + Path.GetExtension(originalFileName);
        if (string.IsNullOrWhiteSpace(Path.GetExtension(storedName)))
        {
            storedName += ".mp4";
        }

        var record = new SourceClipRecord
        {
            Id = Guid.NewGuid(),
            OriginalFileName = Path.GetFileName(originalFileName),
            StoredFileName = storedName,
            Sha256 = sha,
            FrameHash = frameHash,
            Market = market.Trim(),
            Country = country.Trim(),
            Culture = culture.Trim(),
            DurationSeconds = Math.Round(duration, 2),
            Status = status,
            QuotaCredit = SourceMediaRules.QuotaCredit(status),
            Provenance = provenance,
            AddedAt = DateTime.UtcNow
        };
        File.Move(temporaryPath, store.ClipPath(record), overwrite: true);
        store.Update(document => document.Clips.Add(record));
        return record;
    }

    private static DemonstrationRecord NewReferenceRecord() => new()
    {
        Slug = ReferenceProspect.Slug,
        BusinessName = ReferenceProspect.BusinessName,
        Niche = ReferenceProspect.Niche,
        Market = ReferenceProspect.Market,
        Country = ReferenceProspect.Country,
        Language = ReferenceProspect.Language,
        BuyingRoles = string.Join(", ", BuyingRoleCatalog.RolesFor(ReferenceProspect.Niche)),
        DecisionMakerStatus = ReferenceProspect.DecisionMakerStatus,
        ContactTier = ReferenceProspect.ContactTier,
        ContactRoute = ReferenceProspect.ContactRoute,
        Disclosure = ReferenceProspect.Disclosure,
        Illustrative = true,
        CreatedAt = DateTime.UtcNow
    };
}
