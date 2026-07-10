using System.Reflection;
using YitPush;

namespace YitPush.Tests;

public class CommitFormatTests
{
    private static string InvokeRenderTemplate(string template, IDictionary<string, string> values)
    {
        var method = typeof(Program).GetMethod(
            "RenderCommitTemplate",
            BindingFlags.Static | BindingFlags.NonPublic);
        Assert.NotNull(method);
        return (string)method!.Invoke(null, new object?[] { template, values })!;
    }

    private static string InvokeFormatConventional(
        string type,
        string? scope,
        string subject,
        string body,
        string? breakingReason)
    {
        var method = typeof(Program).GetMethod(
            "FormatConventionalCommit",
            BindingFlags.Static | BindingFlags.NonPublic);
        Assert.NotNull(method);
        return (string)method!.Invoke(null, new object?[] { type, scope, subject, body, breakingReason })!;
    }

    private static IReadOnlyList<string> InvokeDetectBreaking(string diff)
    {
        var method = typeof(Program).GetMethod(
            "DetectBreakingChanges",
            BindingFlags.Static | BindingFlags.NonPublic);
        Assert.NotNull(method);
        return (IReadOnlyList<string>)method!.Invoke(null, new object?[] { diff })!;
    }

    private static CommitArgs InvokeParseArgs(string[] args, AppConfig? config = null)
    {
        var method = typeof(Program).GetMethod(
            "ParseCommitArgs",
            BindingFlags.Static | BindingFlags.NonPublic);
        Assert.NotNull(method);
        return (CommitArgs)method!.Invoke(null, new object?[] { args, config })!;
    }

    private static string InvokeBuild(string aiOutput, CommitArgs args, IReadOnlyList<string> markers)
    {
        var method = typeof(Program).GetMethod(
            "BuildCommitMessage",
            BindingFlags.Static | BindingFlags.NonPublic);
        Assert.NotNull(method);
        return (string)method!.Invoke(null, new object?[] { aiOutput, args, markers })!;
    }

    private static (bool ok, string? type, string? scope, string? subject, bool breaking, string? footer)
        InvokeTryParse(string message)
    {
        var method = typeof(Program).GetMethod(
            "TryParseConventionalCommit",
            BindingFlags.Static | BindingFlags.NonPublic);
        Assert.NotNull(method);
        var parameters = method!.GetParameters();
        object?[] args = new object?[parameters.Length];
        args[0] = message;
        // out params: TryParseConventionalCommit(string message, out string type, out string? scope,
        //                                            out string subject, out bool breaking, out string? footer) -> bool
        args[1] = null; args[2] = null; args[3] = null; args[4] = null; args[5] = null;
        var ok = (bool)method.Invoke(null, args)!;
        return (ok, (string?)args[1], (string?)args[2], (string?)args[3], (bool)args[4]!, (string?)args[5]);
    }

    [Fact]
    public void RenderCommitTemplate_substitutes_all_named_variables()
    {
        var template =
            "{{type}}({{scope}}): {{subject}}\n\n" +
            "{{body}}\n\n" +
            "Refs: {{refs}}";

        var rendered = InvokeRenderTemplate(template, new Dictionary<string, string>
        {
            ["type"] = "feat",
            ["scope"] = "api",
            ["subject"] = "add foo endpoint",
            ["body"] = "Adds the /foo endpoint.",
            ["refs"] = "#123",
        });

        Assert.Equal(
            "feat(api): add foo endpoint\n\n" +
            "Adds the /foo endpoint.\n\n" +
            "Refs: #123",
            rendered);
    }

    [Fact]
    public void RenderCommitTemplate_replaces_missing_variable_with_empty_string()
    {
        var template = "{{type}}({{scope}}): {{subject}}\n\n{{body}}";

        var rendered = InvokeRenderTemplate(template, new Dictionary<string, string>
        {
            ["type"] = "fix",
            ["scope"] = "wcf",
            ["subject"] = "trim trailing whitespace",
        });

        Assert.Equal("fix(wcf): trim trailing whitespace\n\n", rendered);
    }

    [Fact]
    public void RenderCommitTemplate_leaves_malformed_tokens_untouched()
    {
        var template = "{{type}}: {{ 1abc }} and {{a.b}} and {{}}";

        var rendered = InvokeRenderTemplate(template, new Dictionary<string, string>
        {
            ["type"] = "chore",
        });

        Assert.Equal("chore: {{ 1abc }} and {{a.b}} and {{}}", rendered);
    }

    [Fact]
    public void RenderCommitTemplate_returns_input_unchanged_when_template_has_no_tokens()
    {
        var template = "plain message, no variables here";

        var rendered = InvokeRenderTemplate(template, new Dictionary<string, string>());

        Assert.Equal(template, rendered);
    }

    [Fact]
    public void TryParseConventionalCommit_parses_plain_subject()
    {
        var (ok, type, scope, subject, breaking, footer) =
            InvokeTryParse("feat: add new endpoint");

        Assert.True(ok);
        Assert.Equal("feat", type);
        Assert.Null(scope);
        Assert.Equal("add new endpoint", subject);
        Assert.False(breaking);
        Assert.Null(footer);
    }

    [Fact]
    public void TryParseConventionalCommit_parses_type_with_scope()
    {
        var (ok, type, scope, subject, breaking, footer) =
            InvokeTryParse("fix(wcf): trim trailing whitespace");

        Assert.True(ok);
        Assert.Equal("fix", type);
        Assert.Equal("wcf", scope);
        Assert.Equal("trim trailing whitespace", subject);
        Assert.False(breaking);
    }

    [Fact]
    public void TryParseConventionalCommit_parses_breaking_marker_with_bang()
    {
        var (ok, type, scope, subject, breaking, footer) =
            InvokeTryParse("feat(api)!: remove deprecated entry point");

        Assert.True(ok);
        Assert.Equal("feat", type);
        Assert.Equal("api", scope);
        Assert.Equal("remove deprecated entry point", subject);
        Assert.True(breaking);
    }

    [Fact]
    public void TryParseConventionalCommit_extracts_breaking_change_footer()
    {
        var input =
            "feat(api): add new endpoint\n\n" +
            "Adds a brand-new endpoint.\n\n" +
            "BREAKING CHANGE: old endpoint removed";

        var (ok, type, scope, subject, breaking, footer) = InvokeTryParse(input);

        Assert.True(ok);
        Assert.Equal("feat", type);
        Assert.Equal("api", scope);
        Assert.Equal("add new endpoint", subject);
        Assert.True(breaking);
        Assert.Equal("old endpoint removed", footer);
    }

    [Fact]
    public void TryParseConventionalCommit_strips_surrounding_whitespace_from_subject()
    {
        var (ok, type, scope, subject, breaking, footer) =
            InvokeTryParse("chore :   bump deps   ");

        Assert.True(ok);
        Assert.Equal("chore", type);
        Assert.Equal("bump deps", subject);
        Assert.False(breaking);
    }

    [Fact]
    public void TryParseConventionalCommit_rejects_non_conventional_subject()
    {
        var (ok, _, _, _, _, _) =
            InvokeTryParse("this is a regular message without a type prefix");

        Assert.False(ok);
    }

    [Fact]
    public void TryParseConventionalCommit_rejects_unknown_type()
    {
        var (ok, _, _, _, _, _) =
            InvokeTryParse("wat: does not start with a valid type");

        Assert.False(ok);
    }

    [Fact]
    public void TryParseConventionalCommit_accepts_all_documented_types()
    {
        string[] types = { "feat", "fix", "chore", "refactor", "docs", "test", "perf", "build", "ci", "style" };
        foreach (var t in types)
        {
            var (ok, parsedType, _, _, _, _) = InvokeTryParse($"{t}: something");
            Assert.True(ok, $"type '{t}' should be accepted");
            Assert.Equal(t, parsedType);
        }
    }

    [Fact]
    public void FormatConventionalCommit_builds_plain_subject()
    {
        var message = InvokeFormatConventional("feat", null, "add foo", string.Empty, null);

        Assert.Equal("feat: add foo", message);
    }

    [Fact]
    public void FormatConventionalCommit_includes_scope_when_provided()
    {
        var message = InvokeFormatConventional("fix", "wcf", "trim spaces", string.Empty, null);

        Assert.Equal("fix(wcf): trim spaces", message);
    }

    [Fact]
    public void FormatConventionalCommit_appends_bang_when_breakingReason_provided()
    {
        var message = InvokeFormatConventional(
            "feat",
            "api",
            "replace foo",
            string.Empty,
            "the foo API is gone");

        Assert.StartsWith("feat(api)!: replace foo", message);
        Assert.Contains("BREAKING CHANGE: the foo API is gone", message);
    }

    [Fact]
    public void FormatConventionalCommit_includes_body_when_provided()
    {
        var message = InvokeFormatConventional(
            "feat",
            null,
            "add foo",
            "Adds a brand new foo.",
            null);

        Assert.Equal("feat: add foo\n\nAdds a brand new foo.", message);
    }

    [Fact]
    public void FormatConventionalCommit_body_and_footer_with_proper_separation()
    {
        var message = InvokeFormatConventional(
            "feat",
            "api",
            "rename foo to bar",
            "Renames the foo entrypoint to bar.\r\nThe old path redirects for one release.",
            "old path /foo returns 410");

        // Body lines: renames ..., old path ...
        Assert.Contains("Renames the foo entrypoint to bar.", message);
        Assert.Contains("The old path redirects for one release.", message);
        Assert.Contains("BREAKING CHANGE: old path /foo returns 410", message);
    }

    [Fact]
    public void DetectBreakingChanges_returns_empty_for_addition_only_diff()
    {
        var diff =
            "diff --git a/src/foo.cs b/src/foo.cs\n" +
            "new file mode 100644\n" +
            "@@ -0,0 +1,3 @@\n" +
            "+namespace X;\n" +
            "+public class Foo {\n" +
            "+    public string Bar() => \"bar\";\n";

        var markers = InvokeDetectBreaking(diff);
        Assert.Empty(markers);
    }

    [Fact]
    public void DetectBreakingChanges_detects_removed_csharp_public_member()
    {
        var diff =
            "diff --git a/src/Svc.cs b/src/Svc.cs\n" +
            "@@ -3,7 +3,0 @@\n" +
            "-namespace X {\n" +
            "-  public class OldApi {\n" +
            "-    public string Ping() { return \"pong\"; }\n" +
            "-  }\n" +
            "-}\n";

        var markers = InvokeDetectBreaking(diff);
        Assert.NotEmpty(markers);
        Assert.Contains(markers, m => m.Contains("public ", StringComparison.Ordinal));
    }

    [Fact]
    public void DetectBreakingChanges_detects_removed_ts_js_export()
    {
        var diff =
            "--- a/src/utils.ts\n" +
            "+++ b/src/utils.ts\n" +
            "@@ -1,4 +1,0 @@\n" +
            "-export function foo() { return 1; }\n" +
            "-export const bar = 2;\n";

        var markers = InvokeDetectBreaking(diff);
        Assert.NotEmpty(markers);
        Assert.Contains(markers, m => m.Contains("export", StringComparison.Ordinal));
    }

    [Fact]
    public void DetectBreakingChanges_detects_json_major_version_bump()
    {
        var diff =
            "--- a/package.json\n" +
            "+++ b/package.json\n" +
            "@@ -1,3 +1,3 @@\n" +
            "-  \"version\": \"2.4.1\"\n" +
            "+  \"version\": \"3.0.0\"\n";

        var markers = InvokeDetectBreaking(diff);
        Assert.NotEmpty(markers);
        Assert.Contains(markers, m => m.Contains("major", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void DetectBreakingChanges_detects_major_bump_marker_in_diff()
    {
        // Marker can be added by humans as `#major.bump` in a code comment that gets diffed.
        var diff =
            "--- a/src/version.txt\n" +
            "+++ b/src/version.txt\n" +
            "@@ -1,1 +1,1 @@\n" +
            "-v2\n" +
            "+v3 #major.bump\n";

        var markers = InvokeDetectBreaking(diff);
        Assert.NotEmpty(markers);
        Assert.Contains(markers, m => m.Contains("major.bump", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void DetectBreakingChanges_does_not_flag_minor_version_bump()
    {
        var diff =
            "--- a/package.json\n" +
            "+++ b/package.json\n" +
            "@@ -1,3 +1,3 @@\n" +
            "-  \"version\": \"2.4.1\"\n" +
            "+  \"version\": \"2.5.0\"\n";

        var markers = InvokeDetectBreaking(diff);
        Assert.Empty(markers);
    }

    [Fact]
    public void ParseCommitArgs_returns_defaults_when_no_flags_passed()
    {
        var ca = InvokeParseArgs(Array.Empty<string>());

        Assert.False(ca.RequireConfirmation);
        Assert.False(ca.Detailed);
        Assert.False(ca.Save);
        Assert.False(ca.Conventional);
        Assert.False(ca.DetectBreaking);
        Assert.False(ca.Amend);
        Assert.Null(ca.Type);
        Assert.Null(ca.Scope);
        Assert.Null(ca.TemplatePath);
        Assert.Null(ca.Format);
        Assert.Equal("english", ca.Language);
    }

    [Fact]
    public void ParseCommitArgs_recognizes_legacy_flags()
    {
        var ca = InvokeParseArgs(new[] { "--confirm", "--detailed", "--save", "--language", "spanish" });

        Assert.True(ca.RequireConfirmation);
        Assert.True(ca.Detailed);
        Assert.True(ca.Save);
        Assert.Equal("spanish", ca.Language);
    }

    [Fact]
    public void ParseCommitArgs_conventional_flag_sets_Format_to_conventional()
    {
        var ca = InvokeParseArgs(new[] { "--conventional" });

        Assert.True(ca.Conventional);
        Assert.Equal("conventional", ca.Format);
    }

    [Fact]
    public void ParseCommitArgs_type_flag_is_lowercased_and_validated()
    {
        var ca = InvokeParseArgs(new[] { "--type", "Feat" });

        Assert.Equal("feat", ca.Type);
    }

    [Fact]
    public void ParseCommitArgs_type_flag_accepts_equals_form()
    {
        var ca = InvokeParseArgs(new[] { "--type=fix" });

        Assert.Equal("fix", ca.Type);
    }

    [Fact]
    public void ParseCommitArgs_scope_flag_is_passed_through_as_is()
    {
        var ca = InvokeParseArgs(new[] { "--scope", "wcf" });

        Assert.Equal("wcf", ca.Scope);
    }

    [Fact]
    public void ParseCommitArgs_detect_breaking_flag_is_recognized()
    {
        var ca = InvokeParseArgs(new[] { "--detect-breaking" });

        Assert.True(ca.DetectBreaking);
    }

    [Fact]
    public void ParseCommitArgs_amend_flag_is_recognized()
    {
        var ca = InvokeParseArgs(new[] { "--amend" });

        Assert.True(ca.Amend);
    }

    [Fact]
    public void ParseCommitArgs_template_flag_captures_path_argument()
    {
        var ca = InvokeParseArgs(new[] { "--template", "/tmp/commit-template.md" });

        Assert.Equal("/tmp/commit-template.md", ca.TemplatePath);
        Assert.Equal("/tmp/commit-template.md", ca.Format);
    }

    [Fact]
    public void ParseCommitArgs_format_falls_back_to_config_when_no_flags()
    {
        var config = new AppConfig { CommitFormat = "gitmoji" };
        var ca = InvokeParseArgs(Array.Empty<string>(), config);

        Assert.Equal("gitmoji", ca.Format);
        Assert.False(ca.Conventional);
    }

    [Fact]
    public void ParseCommitArgs_explicit_conventional_flag_overrides_config_template()
    {
        var config = new AppConfig { CommitFormat = "/tmp/tpl.md" };
        var ca = InvokeParseArgs(new[] { "--conventional" }, config);

        Assert.True(ca.Conventional);
        Assert.Equal("conventional", ca.Format);
    }

    [Fact]
    public void BuildCommitMessage_passes_through_when_no_format_set()
    {
        var args = InvokeParseArgs(Array.Empty<string>());
        var output = InvokeBuild("free-form AI message\n\nbody", args, Array.Empty<string>());
        Assert.Equal("free-form AI message\n\nbody", output);
    }

    [Fact]
    public void BuildCommitMessage_format_conventional_normalizes_ai_output()
    {
        var args = InvokeParseArgs(new[] { "--conventional" });
        var ai = "feat(api): add new endpoint\n\nThis adds /endpoint.";

        var rendered = InvokeBuild(ai, args, Array.Empty<string>());

        Assert.StartsWith("feat(api): add new endpoint", rendered);
        Assert.Contains("This adds /endpoint.", rendered);
    }

    [Fact]
    public void BuildCommitMessage_appends_detected_breaking_reason_when_no_footer_yet()
    {
        var args = InvokeParseArgs(new[] { "--conventional", "--detect-breaking" });
        var ai = "feat(api): remove old endpoint\n\nRemoves the old endpoint.";

        var markers = new List<string>
        {
            "csharp removed public symbol: public class OldApi",
        };

        var rendered = InvokeBuild(ai, args, markers);

        Assert.Contains("feat(api)!: remove old endpoint", rendered);
        Assert.Contains("BREAKING CHANGE: csharp removed public symbol: public class OldApi", rendered);
    }

    [Fact]
    public void BuildCommitMessage_keeps_ai_in_footer_when_already_present()
    {
        var args = InvokeParseArgs(new[] { "--conventional", "--detect-breaking" });
        var ai =
            "feat(api)!: remove old endpoint\n\n" +
            "Removes the old endpoint.\n\n" +
            "BREAKING CHANGE: clients must migrate to /v2";

        var markers = new List<string> { "csharp removed public symbol: public class OldApi" };

        var rendered = InvokeBuild(ai, args, markers);

        Assert.Contains("BREAKING CHANGE: clients must migrate to /v2", rendered);
        Assert.DoesNotContain("csharp removed public symbol", rendered);
    }

    [Fact]
    public void BuildCommitMessage_forces_type_when_type_flag_provided()
    {
        var args = InvokeParseArgs(new[] { "--conventional", "--type", "feat", "--scope", "wcf" });
        // AI drifted from the user-provided type/scope — Build must re-align.
        var ai = "fix: random fix message";

        var rendered = InvokeBuild(ai, args, Array.Empty<string>());

        Assert.StartsWith("feat(wcf): random fix message", rendered);
    }

    [Fact]
    public void BuildCommitMessage_renders_template_with_all_five_vars()
    {
        var templatePath = Path.Combine(Path.GetTempPath(), $"tpl-{Guid.NewGuid():N}.md");
        File.WriteAllText(templatePath,
            "{{type}}({{scope}}): {{subject}}\n\n" +
            "{{body}}\n\n" +
            "Refs: {{refs}}");

        try
        {
            var args = InvokeParseArgs(new[] { "--template", templatePath });
            var ai = "feat(api): add foo\n\nAdds /foo.";

            var rendered = InvokeBuild(ai, args, Array.Empty<string>());

            Assert.Contains("feat(api): add foo", rendered);
            Assert.Contains("Adds /foo.", rendered);
        }
        finally
        {
            if (File.Exists(templatePath)) File.Delete(templatePath);
        }
    }

    [Fact]
    public void BuildCommitMessage_renders_template_with_missing_vars_left_blank()
    {
        var templatePath = Path.Combine(Path.GetTempPath(), $"tpl-{Guid.NewGuid():N}.md");
        File.WriteAllText(templatePath, "{{type}}: {{subject}}");

        try
        {
            var args = InvokeParseArgs(new[] { "--template", templatePath });
            // AI emits a commit with no scope and no body
            var ai = "chore: bump deps";

            var rendered = InvokeBuild(ai, args, Array.Empty<string>());

            Assert.Equal("chore: bump deps", rendered);
        }
        finally
        {
            if (File.Exists(templatePath)) File.Delete(templatePath);
        }
    }

    [Fact]
    public void BuildCommitMessage_throws_FileNotFoundException_when_template_missing()
    {
        var missing = Path.Combine(Path.GetTempPath(), $"never-exists-{Guid.NewGuid():N}.md");
        var args = InvokeParseArgs(new[] { "--template", missing });

        var ex = Assert.Throws<TargetInvocationException>(() =>
            InvokeBuild("feat: x", args, Array.Empty<string>()));

        Assert.IsType<FileNotFoundException>(ex.InnerException);
        Assert.Contains(missing, ex.InnerException!.Message);
    }

    private static string InvokeBuildPrompt(CommitArgs args, string diff, IReadOnlyList<string> breakingMarkers)
    {
        var method = typeof(Program).GetMethod(
            "BuildCommitPrompt",
            BindingFlags.Static | BindingFlags.NonPublic);
        Assert.NotNull(method);
        return (string)method!.Invoke(null, new object?[] { args, diff, breakingMarkers })!;
    }

    [Fact]
    public void BuildCommitPrompt_contains_conventional_subject_constraint_when_conventional_set()
    {
        var args = InvokeParseArgs(new[] { "--conventional" });
        var prompt = InvokeBuildPrompt(args, "diff content", Array.Empty<string>());

        Assert.Contains("type(scope)?!?: ", prompt);
    }

    [Fact]
    public void BuildCommitPrompt_contains_explicit_type_when_provided()
    {
        var args = InvokeParseArgs(new[] { "--conventional", "--type", "feat", "--scope", "wcf" });
        var prompt = InvokeBuildPrompt(args, "diff content", Array.Empty<string>());

        Assert.Contains("\"feat\"", prompt);
        Assert.Contains("\"wcf\"", prompt);
    }

    [Fact]
    public void BuildCommitPrompt_mentions_breaking_change_footer_when_conventional_set()
    {
        var args = InvokeParseArgs(new[] { "--conventional" });
        var prompt = InvokeBuildPrompt(args, "diff content", Array.Empty<string>());

        Assert.Contains("BREAKING CHANGE:", prompt);
    }

    [Fact]
    public void BuildCommitPrompt_does_not_mention_conventional_constraints_when_no_format()
    {
        var args = InvokeParseArgs(Array.Empty<string>());
        var prompt = InvokeBuildPrompt(args, "diff content", Array.Empty<string>());

        // Default mode ("plain") describes the AI in a free-form way without the constraint marker.
        Assert.DoesNotContain("type(scope)?!?:", prompt);
    }

    [Fact]
    public void BuildCommitPrompt_includes_detected_breaking_markers_when_detect_breaking_set()
    {
        var args = InvokeParseArgs(new[] { "--detect-breaking" });
        var markers = new List<string>
        {
            "csharp removed public symbol: public class OldApi",
            "json major version bump to 3.0.0",
        };

        var prompt = InvokeBuildPrompt(args, "diff content", markers);

        Assert.Contains("csharp removed public symbol", prompt);
        Assert.Contains("json major version bump", prompt);
    }

    [Fact]
    public void BuildCommitPrompt_omits_breaking_markers_when_detect_breaking_not_set()
    {
        var args = InvokeParseArgs(Array.Empty<string>());
        var markers = new List<string> { "csharp removed public symbol" };

        var prompt = InvokeBuildPrompt(args, "diff content", markers);

        Assert.DoesNotContain("csharp removed public symbol", prompt);
    }
}
