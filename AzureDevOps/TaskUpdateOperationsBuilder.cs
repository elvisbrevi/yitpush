namespace YitPush;

internal static class TaskUpdateOperationsBuilder
{
    public abstract record Operation;

    public sealed record StringFieldOperation(string RefName, string Value) : Operation;

    public sealed record IdentityFieldOperation(
        string RefName,
        string DisplayName,
        string UniqueName,
        string Id) : Operation;

    public sealed record ClearFieldOperation(string RefName) : Operation;

    public sealed record IdentityMatch(
        string Id,
        string DisplayName,
        string UniqueName);

    public static List<Operation> BuildUpdateOperationsStructured(
        string? title,
        string? description,
        string? effort,
        string? effortReal,
        string? remaining,
        string? state,
        string? evidenceRefName,
        string? evidence,
        IReadOnlyList<string> extraFields,
        string? history,
        string? effortRefName = null,
        string? effortRealRefName = null,
        IdentityMatch? assignedToMatch = null,
        bool clearAssignedTo = false,
        string? assignedToRefName = null)
    {
        var ops = new List<Operation>();

        if (!string.IsNullOrEmpty(title)) ops.Add(new StringFieldOperation("System.Title", title));
        if (!string.IsNullOrEmpty(description)) ops.Add(new StringFieldOperation("System.Description", description));
        if (!string.IsNullOrEmpty(state)) ops.Add(new StringFieldOperation("System.State", state));

        if (!string.IsNullOrEmpty(effort))
        {
            var refName = !string.IsNullOrEmpty(effortRefName) ? effortRefName : Program.AzFieldEffortHH;
            ops.Add(new StringFieldOperation(refName!, effort));
        }

        if (!string.IsNullOrEmpty(effortReal))
        {
            var refName = !string.IsNullOrEmpty(effortRealRefName) ? effortRealRefName : Program.AzFieldEffortRealHH;
            ops.Add(new StringFieldOperation(refName!, effortReal));
            ops.Add(new StringFieldOperation("Microsoft.VSTS.Scheduling.CompletedWork", effortReal));
        }

        if (!string.IsNullOrEmpty(remaining)) ops.Add(new StringFieldOperation(Program.AzFieldRemainingWork!, remaining));

        if (!string.IsNullOrEmpty(evidence) && !string.IsNullOrEmpty(evidenceRefName))
        {
            ops.Add(new StringFieldOperation(evidenceRefName, evidence));
        }

        foreach (var f in extraFields)
        {
            var idx = f.IndexOf('=');
            if (idx > 0)
            {
                var refName = f[..idx];
                var value = f[(idx + 1)..];
                ops.Add(new StringFieldOperation(refName, value));
            }
            else
            {
                ops.Add(new StringFieldOperation(f, ""));
            }
        }

        if (!string.IsNullOrEmpty(history)) ops.Add(new StringFieldOperation("System.History", history));

        var assignedRef = !string.IsNullOrEmpty(assignedToRefName) ? assignedToRefName : "System.AssignedTo";

        if (clearAssignedTo)
        {
            ops.Add(new ClearFieldOperation(assignedRef));
        }
        else if (assignedToMatch != null)
        {
            ops.Add(new IdentityFieldOperation(
                assignedRef,
                assignedToMatch.DisplayName,
                assignedToMatch.UniqueName,
                assignedToMatch.Id));
        }

        return ops;
    }

    public static List<string> BuildUpdateOperations(
        string? title,
        string? description,
        string? effort,
        string? effortReal,
        string? remaining,
        string? state,
        string? evidenceRefName,
        string? evidence,
        IReadOnlyList<string> extraFields,
        string? history,
        string? effortRefName = null,
        string? effortRealRefName = null)
    {
        var structured = BuildUpdateOperationsStructured(
            title, description, effort, effortReal, remaining, state,
            evidenceRefName, evidence, extraFields, history,
            effortRefName, effortRealRefName);

        var ops = new List<string>(structured.Count);
        foreach (var op in structured)
        {
            switch (op)
            {
                case StringFieldOperation s:
                    ops.Add($"{s.RefName}={s.Value}");
                    break;
                case IdentityFieldOperation i:
                    // Identity ops cannot round-trip to "RefName=Value" — callers using the
                    // string-based API are not expected to use --assigned-to.
                    break;
                case ClearFieldOperation c:
                    // Clear ops cannot round-trip to "RefName=Value" — same caveat as above.
                    break;
            }
        }
        return ops;
    }
}
