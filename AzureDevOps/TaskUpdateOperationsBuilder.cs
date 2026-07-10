namespace YitPush;

internal static class TaskUpdateOperationsBuilder
{
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
        var ops = new List<string>();

        if (!string.IsNullOrEmpty(title)) ops.Add($"System.Title={title}");
        if (!string.IsNullOrEmpty(description)) ops.Add($"System.Description={description}");
        if (!string.IsNullOrEmpty(state)) ops.Add($"System.State={state}");

        if (!string.IsNullOrEmpty(effort))
        {
            var refName = !string.IsNullOrEmpty(effortRefName) ? effortRefName : Program.AzFieldEffortHH;
            ops.Add($"{refName}={effort}");
        }

        if (!string.IsNullOrEmpty(effortReal))
        {
            var refName = !string.IsNullOrEmpty(effortRealRefName) ? effortRealRefName : Program.AzFieldEffortRealHH;
            ops.Add($"{refName}={effortReal}");
            ops.Add($"Microsoft.VSTS.Scheduling.CompletedWork={effortReal}");
        }

        if (!string.IsNullOrEmpty(remaining)) ops.Add($"{Program.AzFieldRemainingWork}={remaining}");

        if (!string.IsNullOrEmpty(evidence) && !string.IsNullOrEmpty(evidenceRefName))
        {
            ops.Add($"{evidenceRefName}={evidence}");
        }

        foreach (var f in extraFields) ops.Add(f);
        if (!string.IsNullOrEmpty(history)) ops.Add($"System.History={history}");

        return ops;
    }
}
