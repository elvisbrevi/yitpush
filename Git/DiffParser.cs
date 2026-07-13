using System.Text.RegularExpressions;

namespace YitPush;

partial class Program
{
    private static readonly Regex HunkHeaderRegex = new(
        @"^@@ -(?<beforeStart>\d+)(,(?<beforeCount>\d+))? \+(?<afterStart>\d+)(,(?<afterCount>\d+))? @@",
        RegexOptions.Compiled);

    // Captures the b/... side of a "diff --git a/<p> b/<p>" header so binary files
    // (which never emit a +++ line) still get a path.
    private static readonly Regex DiffGitPathRegex = new(
        @"^diff --git a/(?<path>.+?) b/(?<bPath>.+?)$",
        RegexOptions.Compiled);

    internal static IReadOnlyList<DiffFile> ParseUnifiedDiff(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return Array.Empty<DiffFile>();

        var files = new List<DiffFile>();
        var lines = raw.Replace("\r\n", "\n").Split('\n');

        int i = 0;
        while (i < lines.Length)
        {
            // Look for the start of a file diff block.
            if (!lines[i].StartsWith("diff --git ", StringComparison.Ordinal))
            {
                i++;
                continue;
            }

            var file = new DiffFile();
            var hunkContents = new List<string>();
            string? lastBeforePath = null;
            string? lastAfterPath = null;
            int? currentHunkBeforeStart = null;
            int? currentHunkAfterStart = null;
            string? currentHunkRaw = null;
            int beforeCursor = 0;
            int afterCursor = 0;

            i++; // consume the "diff --git ..." line

            // For binary files (no +++ line) we still need a path — pull it from
            // the "diff --git a/<p> b/<p>" header.
            var headerMatch = DiffGitPathRegex.Match(lines[i - 1]);
            if (headerMatch.Success && string.IsNullOrEmpty(file.Path))
            {
                file.Path = headerMatch.Groups["bPath"].Value;
            }

            // Parse preamble (index, ---, +++, similarity, rename, binary, ...)
            while (i < lines.Length && !lines[i].StartsWith("@@ ", StringComparison.Ordinal))
            {
                var line = lines[i];
                if (line.StartsWith("rename from ", StringComparison.Ordinal))
                {
                    file.IsRename = true;
                    lastBeforePath = line["rename from ".Length..];
                }
                else if (line.StartsWith("rename to ", StringComparison.Ordinal))
                {
                    file.IsRename = true;
                    lastAfterPath = line["rename to ".Length..];
                }
                else if (line.StartsWith("--- ", StringComparison.Ordinal))
                {
                    lastBeforePath = line["--- ".Length..];
                    if (lastBeforePath.StartsWith("a/")) lastBeforePath = lastBeforePath[2..];
                }
                else if (line.StartsWith("+++ ", StringComparison.Ordinal))
                {
                    lastAfterPath = line["+++ ".Length..];
                    if (lastAfterPath.StartsWith("b/")) lastAfterPath = lastAfterPath[2..];
                }
                else if (line.StartsWith("Binary files ", StringComparison.Ordinal))
                {
                    file.IsBinary = true;
                }
                i++;
            }

            // If we never saw a +++ line (pure rename or binary), fall back to the
            // diff --git header we already parsed.
            if (string.IsNullOrEmpty(file.Path))
            {
                file.Path = lastAfterPath ?? lastBeforePath ?? file.Path;
            }
            if (file.IsRename && string.IsNullOrEmpty(file.OldPath) && lastBeforePath != null)
            {
                file.OldPath = lastBeforePath;
            }

            // Parse hunks.
            while (i < lines.Length && lines[i].StartsWith("@@ ", StringComparison.Ordinal))
            {
                var header = lines[i];
                var match = HunkHeaderRegex.Match(header);
                if (match.Success)
                {
                    currentHunkBeforeStart = int.Parse(match.Groups["beforeStart"].Value);
                    currentHunkAfterStart = int.Parse(match.Groups["afterStart"].Value);
                    beforeCursor = currentHunkBeforeStart.Value;
                    afterCursor = currentHunkAfterStart.Value;
                }
                currentHunkRaw = header;
                i++;

                // Read hunk body until next "diff --git", "@@", or end.
                while (i < lines.Length
                    && !lines[i].StartsWith("diff --git ", StringComparison.Ordinal)
                    && !lines[i].StartsWith("@@ ", StringComparison.Ordinal))
                {
                    currentHunkRaw += "\n" + lines[i];

                    var bodyLine = lines[i];
                    if (bodyLine.StartsWith("+") && !bodyLine.StartsWith("+++"))
                    {
                        file.Additions++;
                        afterCursor++;
                    }
                    else if (bodyLine.StartsWith("-") && !bodyLine.StartsWith("---"))
                    {
                        file.Deletions++;
                        beforeCursor++;
                    }
                    else if (bodyLine.StartsWith("\\ No newline at end of file", StringComparison.Ordinal))
                    {
                        // Informational marker — don't change counters.
                    }
                    else
                    {
                        // Context line.
                        beforeCursor++;
                        afterCursor++;
                    }
                    i++;
                }

                if (match.Success)
                {
                    file.Hunks.Add(new DiffHunk
                    {
                        BeforeLine = currentHunkBeforeStart!.Value,
                        AfterLine = currentHunkAfterStart!.Value,
                        Content = currentHunkRaw ?? string.Empty
                    });
                }
            }

            files.Add(file);
        }

        return files;
    }
}
