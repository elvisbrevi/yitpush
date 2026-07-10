namespace YitPush;

// Pure helpers extracted from AddLinkToRepo so the URL-building, menu-skip and
// repo-resolution logic can be tested without shelling out to `az` or invoking
// the Spectre.Console interactive prompts.
//
// Issue #9: hu link / link — quick mode honored for tasks + URL Commit fallback.
internal static class AddLinkToRepoHelpers
{
    public readonly record struct BranchLinkPayload(
        string ArtifactUrl,
        string WebUrl,
        string RelationTypeHint,
        string BranchToLink);

    public static BranchLinkPayload BuildBranchLinkArtifact(
        string orgUrl,
        string projectId,
        string projectName,
        string repoId,
        string repoName,
        string branch)
    {
        var escapedBranch = Uri.EscapeDataString(branch);
        var escapedProject = Uri.EscapeDataString(projectName);
        var escapedRepo = Uri.EscapeDataString(repoName);

        var artifactUrl = $"vstfs:///Git/Ref/{projectId}/{repoId}/GB{escapedBranch}";
        var webUrl = $"{orgUrl}/{escapedProject}/_git/{escapedRepo}?version=GB{escapedBranch}";

        return new BranchLinkPayload(artifactUrl, webUrl, "Branch", branch);
    }

    public static bool IsQuickModeLink(string? fixedRepo, string? fixedBranch)
    {
        return !string.IsNullOrWhiteSpace(fixedRepo)
            && !string.IsNullOrWhiteSpace(fixedBranch);
    }

    public static (string Name, string RemoteUrl, string Id)? FindRepoByName(
        IReadOnlyList<(string Name, string RemoteUrl, string Id)> repos,
        string name)
    {
        foreach (var r in repos)
        {
            if (string.Equals(r.Name, name, StringComparison.OrdinalIgnoreCase))
                return r;
        }
        return null;
    }

    public static string BuildArtifactLinkPatchBody(string artifactUrl, string relationType)
    {
        return $"[{{\"op\": \"add\", \"path\": \"/relations/-\", \"value\": {{\"rel\": \"ArtifactLink\", \"url\": \"{artifactUrl}\", \"attributes\": {{\"name\": \"{relationType}\"}} }} }}]";
    }
}
