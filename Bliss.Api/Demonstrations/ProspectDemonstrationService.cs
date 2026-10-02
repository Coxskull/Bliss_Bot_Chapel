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
            await AddConceptAsync(demonstration, concept, clip, publicBaseUrl, cancellationToken);
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

    public DemonstrationRecord Discover(string niche, string market, string businessName, string sourceUrl)
    {
        var screen = OpportunityScreen.Evaluate(niche, market, businessName, sourceUrl);
        if (!screen.PreservesBusiness)
        {
            throw new DiscoveryRejectedException(screen);
        }

        var slug = OpportunityScreen.Slug(businessName);
        if (slug.Length < 3)
        {
            throw new InvalidOperationException("The business name does not produce a usable prospect id.");
        }

        var existing = store.Read().Demonstrations.FirstOrDefault(x => x.Slug == slug);
        if (existing is not null)
        {
            return existing;
        }

        var trimmedName = businessName.Trim();
        var trimmedMarket = string.IsNullOrWhiteSpace(market) ? "Outside the initial markets" : market.Trim();
        var knownNiche = BuyingRoleCatalog.Niches.Contains(niche.Trim(), StringComparer.OrdinalIgnoreCase);
        var preserved = !screen.PassesInitialScreen;
        var record = new DemonstrationRecord
        {
            Slug = slug,
            BusinessName = trimmedName,
            Niche = niche.Trim(),
            Market = trimmedMarket,
            Country = screen.Country,
            Language = screen.Language,
            BuyingRoles = knownNiche ? string.Join(", ", BuyingRoleCatalog.RolesFor(niche)) : string.Empty,
            DecisionMakerStatus = ReferenceProspect.DecisionMakerStatus,
            ContactTier = ReferenceProspect.ContactTier,
            ContactRoute = ReferenceProspect.ContactRoute,
            Disclosure = NicheOverlay.Disclosure(trimmedName),
            PublicSourceUrl = sourceUrl.Trim(),
            OpportunityScore = screen.Score,
            ProspectState = preserved ? "PRESERVED" : "OPPORTUNITY_SCORED",
            BusinessIdentity = "PUBLIC_SOURCE_RECORDED",
            Illustrative = true,
            CreatedAt = DateTime.UtcNow,
            Messages =
            [
                new ChatRecord
                {
                    Role = "ASSISTANT",
                    Text = preserved
                        ? trimmedName + " is preserved. The road score is " + screen.Score
                            + ". No demonstration was manufactured. Nothing is sent."
                        : "Hello. This is a private demonstration for " + trimmedName
                            + " in " + trimmedMarket
                            + ". I can explain the concept and how Alpha works. No named decision-maker is verified, so I will not use a personal name.",
                    Signal = preserved ? "PRESERVED" : "INTRODUCED",
                    At = DateTime.UtcNow
                }
            ]
        };
        store.Update(document => document.Demonstrations.Add(record));
        return record;
    }

    public DemonstrationRecord RecordDecisionMaker(string slug, DecisionMakerInput input)
    {
        var current = Find(slug) ?? throw new InvalidOperationException("Prospect not found.");
        if (current.OpportunityScore != 100)
        {
            throw new InvalidOperationException("A decision-maker is recorded only after the road scores 100. Nothing is sent.");
        }

        var assessment = DecisionMakerEvidence.Evaluate(current.Niche, input);
        if (!assessment.Accepted)
        {
            throw new DecisionMakerRejectedException(assessment);
        }

        current.DecisionMakerName = assessment.PersonName;
        current.DecisionMakerRole = assessment.Role;
        current.DecisionMakerStatus = assessment.Confidence;
        current.EvidenceKind = assessment.EvidenceKind;
        current.EvidenceSourceUrl = assessment.EvidenceSourceUrl;
        current.CorroboratingKind = assessment.CorroboratingKind;
        current.CorroboratingSourceUrl = assessment.CorroboratingSourceUrl;
        current.ContactTier = assessment.ContactTier;
        current.ContactRoute = assessment.ContactRoute;
        current.ContactType = assessment.ContactType;
        current.ContactValue = assessment.ContactValue;
        current.ContactSourceUrl = assessment.ContactSourceUrl;
        current.ContactVerification = assessment.ContactVerification;
        current.FreshnessStatus = assessment.Freshness;
        current.LastVerifiedAt = input.VerifiedAt;
        current.PersonalizationAllowed = assessment.PersonalizationAllowed;
        current.Messages.Add(new ChatRecord
        {
            Role = "ASSISTANT",
            Text = assessment.PersonalizationAllowed
                ? "Updated evidence: the recorded public name is " + assessment.PersonName
                    + ", " + assessment.Role + ". Confidence is " + assessment.Confidence
                    + ". Delivery remains NOT_SENT."
                : "Updated evidence: confidence is " + assessment.Confidence
                    + ". A personal name is not used. Delivery remains NOT_SENT.",
            Signal = "EVIDENCE_RECORDED",
            At = DateTime.UtcNow
        });
        store.Update(document =>
        {
            var index = document.Demonstrations.FindIndex(x => x.Slug == slug);
            if (index < 0)
            {
                document.Demonstrations.Add(current);
            }
            else
            {
                document.Demonstrations[index] = current;
            }
        });
        return current;
    }

    public async Task<DemonstrationRecord> ProduceDiscoveredAsync(
        string slug,
        string publicBaseUrl,
        Guid? sourceClipId,
        CancellationToken cancellationToken)
    {
        var current = Find(slug) ?? throw new InvalidOperationException("Prospect not found.");
        if (current.OpportunityScore != 100)
        {
            throw new InvalidOperationException("The road score is below 100. The business is preserved and no demonstration is manufactured.");
        }

        var library = store.Read();
        var clip = sourceClipId is Guid id
            ? library.Clips.FirstOrDefault(x => x.Id == id)
            : library.Clips.LastOrDefault(x => x.Status == SourceMediaStatus.Qualified && x.Market == current.Market)
                ?? library.Clips.LastOrDefault(x => x.Status == SourceMediaStatus.Qualified);
        if (clip is null || clip.Status != SourceMediaStatus.Qualified)
        {
            throw new InvalidOperationException("A qualified source slice is required before an overlay can be made.");
        }

        var concept = NicheOverlay.One(current.Niche, current.Language, current.Market);
        current.Concepts.Clear();
        await AddConceptAsync(current, concept, clip, publicBaseUrl, cancellationToken);
        current.ProspectState = "DEMONSTRATION_PREPARED";
        store.Update(document =>
        {
            var index = document.Demonstrations.FindIndex(x => x.Slug == slug);
            if (index < 0)
            {
                document.Demonstrations.Add(current);
            }
            else
            {
                document.Demonstrations[index] = current;
            }
        });
        return current;
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
        var presentation = DecisionMakerEvidence.Present(
            demonstration.DecisionMakerStatus,
            demonstration.DecisionMakerName,
            demonstration.LastVerifiedAt,
            DateTime.UtcNow);
        var facts = new ProspectFacts(
            demonstration.BusinessName,
            demonstration.ContactTier,
            demonstration.ContactRoute,
            BuyingRoleCatalog.RolesFor(demonstration.Niche),
            demonstration.Concepts.Select(x => x.Name).ToArray(),
            demonstration.DecisionMakerName,
            demonstration.DecisionMakerRole,
            string.IsNullOrWhiteSpace(demonstration.DecisionMakerStatus) ? "UNVERIFIED" : demonstration.DecisionMakerStatus,
            presentation.PersonalizationAllowed,
            presentation.Freshness);
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

    private async Task AddConceptAsync(
        DemonstrationRecord demonstration,
        OverlayConcept concept,
        SourceClipRecord clip,
        string publicBaseUrl,
        CancellationToken cancellationToken)
    {
        var destination = publicBaseUrl.TrimEnd('/') + "/demonstrations/" + demonstration.Slug;
        var qrName = demonstration.Slug + "-" + concept.Id + ".png";
        var videoName = demonstration.Slug + "-" + concept.Id + ".mp4";
        await File.WriteAllBytesAsync(store.RenderPath(qrName), studio.CreateQrPng(destination), cancellationToken);
        await studio.CompositeAsync(
            store.ClipPath(clip),
            store.RenderPath(qrName),
            store.RenderPath(videoName),
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

public sealed class DecisionMakerRejectedException : InvalidOperationException
{
    public DecisionMakerRejectedException(DecisionMakerAssessment result)
        : base(string.Join(" ", result.Reasons))
    {
        Result = result;
    }

    public DecisionMakerAssessment Result { get; }
}

public sealed class DiscoveryRejectedException : InvalidOperationException
{
    public DiscoveryRejectedException(OpportunityResult result)
        : base(string.Join(" ", result.Reasons))
    {
        Result = result;
    }

    public OpportunityResult Result { get; }
}
