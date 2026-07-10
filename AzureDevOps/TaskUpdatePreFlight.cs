namespace YitPush;

internal static class TaskUpdatePreFlight
{
    public static string BuildMissingEvidenceMessage(string displayName, string? refName)
    {
        var refnamePart = !string.IsNullOrEmpty(refName) ? $" (refname {refName})" : "";
        return
            $"❌ Missing required field for Done transition: {displayName}{refnamePart}. " +
            $"Re-run with --evidence \"<text>\".";
    }
}
