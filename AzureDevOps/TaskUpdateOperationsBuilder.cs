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
        string? history)
    {
        var ops = new List<string>();

        if (!string.IsNullOrEmpty(title)) ops.Add($"System.Title={title}");
        if (!string.IsNullOrEmpty(description)) ops.Add($"System.Description={description}");
        if (!string.IsNullOrEmpty(state)) ops.Add($"System.State={state}");
        if (!string.IsNullOrEmpty(effort)) ops.Add($"{Program.AzFieldEffortHH}={effort}");

        if (!string.IsNullOrEmpty(effortReal))
        {
            ops.Add($"{Program.AzFieldEffortRealHH}={effortReal}");
            ops.Add($"Microsoft.VSTS.Scheduling.CompletedWork={effortReal}");
        }

        if (!string.IsNullOrEmpty(remaining)) ops.Add($"{Program.AzFieldRemainingWork}={remaining}");

        if (!string.IsNullOrEmpty(evidence) && !string.IsNullOrEmpty(evidenceRefName))
        {
            ops.Add($"{evidenceRefName}={evidence}");
        }

        foreach (var f in extraFields) ops.Add(f);

        return ops;
    }
}
