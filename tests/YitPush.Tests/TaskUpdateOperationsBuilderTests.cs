using YitPush;

namespace YitPush.Tests;

public class TaskUpdateOperationsBuilderTests
{
    [Fact]
    public void BuildUpdateOperations_includes_title_when_provided()
    {
        var ops = TaskUpdateOperationsBuilder.BuildUpdateOperations(
            title: "Nuevo título",
            description: null,
            effort: null,
            effortReal: null,
            remaining: null,
            state: null,
            evidenceRefName: null,
            evidence: null,
            extraFields: Array.Empty<string>(),
            history: null);

        Assert.Contains("System.Title=Nuevo título", ops);
    }

    [Fact]
    public void BuildUpdateOperations_includes_description_when_provided()
    {
        var ops = TaskUpdateOperationsBuilder.BuildUpdateOperations(
            title: null,
            description: "Detalles",
            effort: null,
            effortReal: null,
            remaining: null,
            state: null,
            evidenceRefName: null,
            evidence: null,
            extraFields: Array.Empty<string>(),
            history: null);

        Assert.Contains("System.Description=Detalles", ops);
    }

    [Fact]
    public void BuildUpdateOperations_includes_evidence_using_resolved_refname()
    {
        var ops = TaskUpdateOperationsBuilder.BuildUpdateOperations(
            title: null,
            description: null,
            effort: null,
            effortReal: null,
            remaining: null,
            state: null,
            evidenceRefName: "Custom.b505c83e-3745-4d8b-b76b-b3086a0c4c71",
            evidence: "resultado del curl",
            extraFields: Array.Empty<string>(),
            history: null);

        Assert.Contains("Custom.b505c83e-3745-4d8b-b76b-b3086a0c4c71=resultado del curl", ops);
    }

    [Fact]
    public void BuildUpdateOperations_skips_evidence_when_refname_missing()
    {
        var ops = TaskUpdateOperationsBuilder.BuildUpdateOperations(
            title: null,
            description: null,
            effort: null,
            effortReal: null,
            remaining: null,
            state: null,
            evidenceRefName: null,
            evidence: "resultado del curl",
            extraFields: Array.Empty<string>(),
            history: null);

        Assert.DoesNotContain(ops, op => op.StartsWith("Custom."));
    }

    [Fact]
    public void BuildUpdateOperations_passes_extra_fields_through()
    {
        var extras = new[] { "System.Tags=foo", "System.AssignedTo=bar" };

        var ops = TaskUpdateOperationsBuilder.BuildUpdateOperations(
            title: null,
            description: null,
            effort: null,
            effortReal: null,
            remaining: null,
            state: null,
            evidenceRefName: null,
            evidence: null,
            extraFields: extras,
            history: null);

        Assert.Contains("System.Tags=foo", ops);
        Assert.Contains("System.AssignedTo=bar", ops);
    }

    [Fact]
    public void BuildUpdateOperations_includes_history_when_provided()
    {
        var ops = TaskUpdateOperationsBuilder.BuildUpdateOperations(
            title: null, description: null, effort: null, effortReal: null,
            remaining: null, state: null, evidenceRefName: null, evidence: null,
            extraFields: Array.Empty<string>(), history: "Audit log entry");

        Assert.Contains("System.History=Audit log entry", ops);
    }

    [Fact]
    public void BuildUpdateOperations_preserves_existing_fields()
    {
        var ops = TaskUpdateOperationsBuilder.BuildUpdateOperations(
            title: null,
            description: null,
            effort: "8",
            effortReal: "5",
            remaining: "2",
            state: "Doing",
            evidenceRefName: null,
            evidence: null,
            extraFields: Array.Empty<string>(),
            history: null);

        Assert.Contains($"{Program.AzFieldEffortHH}=8", ops);
        Assert.Contains($"{Program.AzFieldEffortRealHH}=5", ops);
        Assert.Contains("Microsoft.VSTS.Scheduling.CompletedWork=5", ops);
        Assert.Contains($"{Program.AzFieldRemainingWork}=2", ops);
        Assert.Contains("System.State=Doing", ops);
    }
}
