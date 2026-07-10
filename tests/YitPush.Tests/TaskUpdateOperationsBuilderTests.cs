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

    [Fact]
    public void BuildUpdateOperations_uses_resolved_effort_refname_when_provided()
    {
        var ops = TaskUpdateOperationsBuilder.BuildUpdateOperations(
            title: null,
            description: null,
            effort: "8",
            effortReal: null,
            remaining: null,
            state: null,
            evidenceRefName: null,
            evidence: null,
            extraFields: Array.Empty<string>(),
            history: null,
            effortRefName: "Custom.EsfuerzoEstimado",
            effortRealRefName: null);

        Assert.Contains("Custom.EsfuerzoEstimado=8", ops);
        Assert.DoesNotContain(ops, op => op.StartsWith($"{Program.AzFieldEffortHH}="));
    }

    [Fact]
    public void BuildUpdateOperations_uses_resolved_effortReal_refname_when_provided()
    {
        var ops = TaskUpdateOperationsBuilder.BuildUpdateOperations(
            title: null,
            description: null,
            effort: null,
            effortReal: "5",
            remaining: null,
            state: null,
            evidenceRefName: null,
            evidence: null,
            extraFields: Array.Empty<string>(),
            history: null,
            effortRefName: null,
            effortRealRefName: "Custom.EsfuerzoReal");

        Assert.Contains("Custom.EsfuerzoReal=5", ops);
        Assert.Contains("Microsoft.VSTS.Scheduling.CompletedWork=5", ops);
        Assert.DoesNotContain(ops, op => op.StartsWith($"{Program.AzFieldEffortRealHH}="));
    }

    [Fact]
    public void BuildUpdateOperations_routes_through_resolver_for_different_projects()
    {
        // Simulates the same call from two different projects with different refnames
        // (e.g. "Soluciones Transversales" -> Custom.EsfuerzoReal, "Cobro Pago y Tarifas" -> Custom.EsfuerzoRealHH).
        var opsSolTrans = TaskUpdateOperationsBuilder.BuildUpdateOperations(
            title: null, description: null, effort: null, effortReal: "5",
            remaining: null, state: null, evidenceRefName: null, evidence: null,
            extraFields: Array.Empty<string>(), history: null,
            effortRefName: null, effortRealRefName: "Custom.EsfuerzoReal");
        var opsCobroPago = TaskUpdateOperationsBuilder.BuildUpdateOperations(
            title: null, description: null, effort: null, effortReal: "5",
            remaining: null, state: null, evidenceRefName: null, evidence: null,
            extraFields: Array.Empty<string>(), history: null,
            effortRefName: null, effortRealRefName: "Custom.EsfuerzoRealHH");

        Assert.Contains("Custom.EsfuerzoReal=5", opsSolTrans);
        Assert.Contains("Custom.EsfuerzoRealHH=5", opsCobroPago);
    }

    [Fact]
    public void BuildUpdateOperationsStructured_emits_clear_assignedTo_op_when_clear_requested()
    {
        var ops = TaskUpdateOperationsBuilder.BuildUpdateOperationsStructured(
            title: null, description: null, effort: null, effortReal: null,
            remaining: null, state: null, evidenceRefName: null, evidence: null,
            extraFields: Array.Empty<string>(), history: null,
            effortRefName: null, effortRealRefName: null,
            assignedToMatch: null, clearAssignedTo: true);

        var clearOp = Assert.Single(ops.OfType<TaskUpdateOperationsBuilder.ClearFieldOperation>());
        Assert.Equal("System.AssignedTo", clearOp.RefName);
    }

    [Fact]
    public void BuildUpdateOperationsStructured_emits_identity_assignedTo_op_when_match_provided()
    {
        var match = new TaskUpdateOperationsBuilder.IdentityMatch(
            Id: "00000000-0000-0000-0000-000000000abc",
            DisplayName: "Elvis Brevi",
            UniqueName: "elvis.brevi@sag.gob.cl");

        var ops = TaskUpdateOperationsBuilder.BuildUpdateOperationsStructured(
            title: null, description: null, effort: null, effortReal: null,
            remaining: null, state: null, evidenceRefName: null, evidence: null,
            extraFields: Array.Empty<string>(), history: null,
            effortRefName: null, effortRealRefName: null,
            assignedToMatch: match, clearAssignedTo: false);

        var identityOp = Assert.Single(ops.OfType<TaskUpdateOperationsBuilder.IdentityFieldOperation>());
        Assert.Equal("System.AssignedTo", identityOp.RefName);
        Assert.Equal("Elvis Brevi", identityOp.DisplayName);
        Assert.Equal("elvis.brevi@sag.gob.cl", identityOp.UniqueName);
        Assert.Equal("00000000-0000-0000-0000-000000000abc", identityOp.Id);
    }

    [Fact]
    public void BuildUpdateOperationsStructured_omits_assignedTo_when_neither_match_nor_clear_provided()
    {
        var ops = TaskUpdateOperationsBuilder.BuildUpdateOperationsStructured(
            title: null, description: null, effort: null, effortReal: null,
            remaining: null, state: null, evidenceRefName: null, evidence: null,
            extraFields: Array.Empty<string>(), history: null,
            effortRefName: null, effortRealRefName: null,
            assignedToMatch: null, clearAssignedTo: false);

        Assert.DoesNotContain(ops, op => op is TaskUpdateOperationsBuilder.IdentityFieldOperation);
        Assert.DoesNotContain(ops, op => op is TaskUpdateOperationsBuilder.ClearFieldOperation
            && ((TaskUpdateOperationsBuilder.ClearFieldOperation)op).RefName == "System.AssignedTo");
    }

    [Fact]
    public void BuildUpdateOperationsStructured_does_not_emit_assignedTo_clear_when_other_fields_present()
    {
        var ops = TaskUpdateOperationsBuilder.BuildUpdateOperationsStructured(
            title: "Updated",
            description: null, effort: null, effortReal: null,
            remaining: null, state: null, evidenceRefName: null, evidence: null,
            extraFields: Array.Empty<string>(), history: null,
            effortRefName: null, effortRealRefName: null,
            assignedToMatch: null, clearAssignedTo: false);

        Assert.Contains(ops, op => op is TaskUpdateOperationsBuilder.StringFieldOperation s && s.RefName == "System.Title" && s.Value == "Updated");
        Assert.DoesNotContain(ops, op => op is TaskUpdateOperationsBuilder.ClearFieldOperation);
    }
}
