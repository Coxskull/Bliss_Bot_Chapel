using Bliss.Domain.Entities;

namespace Bliss.Tests.Domain;

public sealed class EntityDefaultTests
{
    [Fact]
    public void Advertiser_program_defaults_to_active()
    {
        Assert.Equal("ACTIVE", new AdvertiserProgram().Status);
    }

    [Fact]
    public void Advertiser_opportunity_defaults_to_active()
    {
        Assert.Equal("ACTIVE", new AdvertiserOpportunity().Status);
    }

    [Fact]
    public void Bliss_match_defaults_to_created()
    {
        Assert.Equal("CREATED", new BlissMatch().Status);
    }

    [Fact]
    public void Network_and_program_access_default_to_unknown_independently()
    {
        Assert.Equal("UNKNOWN", new NetworkAccess().Status);
        Assert.Equal("UNKNOWN", new ProgramAccess().Status);
    }

    [Fact]
    public void Ad_inventory_slot_defaults_to_available()
    {
        Assert.True(new AdInventorySlot().IsAvailable);
    }

    [Fact]
    public void Data_provenance_confidence_defaults_to_unknown()
    {
        Assert.Equal("UNKNOWN", new DataProvenance().ConfidenceLevel);
    }

    [Fact]
    public void Wedding_planner_workspace_defaults_to_primary_and_active()
    {
        var workspace = new WeddingPlannerWorkspace();
        Assert.True(workspace.IsPrimary);
        Assert.Equal("ACTIVE", workspace.Status);
        Assert.Equal("OPEN", new WeddingPlannerPlanningSession().Status);
    }

    [Fact]
    public void Wedding_planner_phase2_defaults_are_proposed_and_running()
    {
        Assert.Equal("RUNNING", new WeddingPlannerAgentRun().Status);
        Assert.Equal("PROPOSED", new WeddingPlannerBrandDnaVersion().Status);
        Assert.Equal("brand-dna.v1", new WeddingPlannerBrandDnaVersion().SchemaVersion);
        Assert.Null(new WeddingPlannerWorkspace().CurrentApprovedBrandDnaVersionId);
    }

    [Fact]
    public void Wedding_planner_phase5_defaults_are_proposed_and_running()
    {
        Assert.Equal("RUNNING", new WeddingPlannerWorkshopJob().Status);
        Assert.Equal("PROPOSED", new WeddingPlannerConceptPackageVersion().Status);
        Assert.Equal("concept-package.v1", new WeddingPlannerConceptPackageVersion().SchemaVersion);
        Assert.Null(new WeddingPlannerWorkspace().CurrentApprovedConceptPackageVersionId);
        Assert.Null(new WeddingPlannerAgentRun().OutputConceptPackageVersionId);
    }
}
