using YitPush;

namespace YitPush.Tests;

public class AddLinkToRepoHelpersTests
{
    // ─── BuildBranchLinkArtifact ────────────────────────────────────────────

    [Fact]
    public void BuildBranchLinkArtifact_builds_vstfs_artifactUrl_with_repoId_and_branch()
    {
        var link = AddLinkToRepoHelpers.BuildBranchLinkArtifact(
            orgUrl: "https://dev.azure.com/org",
            projectId: "proj-guid",
            projectName: "Cobro Pago y Tarifas",
            repoId: "repo-guid",
            repoName: "Cobro Pago y Tarifas",
            branch: "feature/estado-sps");

        Assert.Equal("vstfs:///Git/Ref/proj-guid/repo-guid/GBfeature%2Festado-sps", link.ArtifactUrl);
    }

    [Fact]
    public void BuildBranchLinkArtifact_builds_webUrl_with_branch_query()
    {
        var link = AddLinkToRepoHelpers.BuildBranchLinkArtifact(
            orgUrl: "https://dev.azure.com/org",
            projectId: "proj-guid",
            projectName: "Cobro Pago y Tarifas",
            repoId: "repo-guid",
            repoName: "Cobro Pago y Tarifas",
            branch: "feature/estado-sps");

        Assert.Equal(
            "https://dev.azure.com/org/Cobro%20Pago%20y%20Tarifas/_git/Cobro%20Pago%20y%20Tarifas?version=GBfeature%2Festado-sps",
            link.WebUrl);
    }

    [Fact]
    public void BuildBranchLinkArtifact_sets_relationTypeHint_to_Branch()
    {
        var link = AddLinkToRepoHelpers.BuildBranchLinkArtifact(
            "https://dev.azure.com/org", "p", "P", "r", "R", "main");

        Assert.Equal("Branch", link.RelationTypeHint);
    }

    [Fact]
    public void BuildBranchLinkArtifact_preserves_branch_in_BranchToLink()
    {
        var link = AddLinkToRepoHelpers.BuildBranchLinkArtifact(
            "https://dev.azure.com/org", "p", "P", "r", "R", "release/2026.07");

        Assert.Equal("release/2026.07", link.BranchToLink);
    }

    [Fact]
    public void BuildBranchLinkArtifact_url_encodes_slash_in_branch()
    {
        var link = AddLinkToRepoHelpers.BuildBranchLinkArtifact(
            "https://dev.azure.com/org", "p", "P", "r", "R", "feature/abc");

        Assert.Contains("GBfeature%2Fabc", link.ArtifactUrl);
    }

    [Fact]
    public void BuildBranchLinkArtifact_url_encodes_spaces_in_projectName_and_repoName()
    {
        var link = AddLinkToRepoHelpers.BuildBranchLinkArtifact(
            "https://dev.azure.com/org",
            "p", "Cobro Pago y Tarifas",
            "r", "Cobro Pago y Tarifas",
            "main");

        Assert.DoesNotContain(" ", link.WebUrl);
        Assert.Contains("Cobro%20Pago%20y%20Tarifas", link.WebUrl);
    }

    // ─── IsQuickModeLink ───────────────────────────────────────────────────

    [Fact]
    public void IsQuickModeLink_true_when_both_repo_and_branch_supplied()
    {
        Assert.True(AddLinkToRepoHelpers.IsQuickModeLink(fixedRepo: "MyRepo", fixedBranch: "main"));
    }

    [Fact]
    public void IsQuickModeLink_false_when_only_repo_supplied()
    {
        Assert.False(AddLinkToRepoHelpers.IsQuickModeLink(fixedRepo: "MyRepo", fixedBranch: null));
    }

    [Fact]
    public void IsQuickModeLink_false_when_only_branch_supplied()
    {
        Assert.False(AddLinkToRepoHelpers.IsQuickModeLink(fixedRepo: null, fixedBranch: "main"));
    }

    [Fact]
    public void IsQuickModeLink_false_when_both_null()
    {
        Assert.False(AddLinkToRepoHelpers.IsQuickModeLink(fixedRepo: null, fixedBranch: null));
    }

    [Fact]
    public void IsQuickModeLink_false_when_both_empty()
    {
        Assert.False(AddLinkToRepoHelpers.IsQuickModeLink(fixedRepo: "", fixedBranch: ""));
    }

    [Fact]
    public void IsQuickModeLink_false_when_branch_is_whitespace()
    {
        Assert.False(AddLinkToRepoHelpers.IsQuickModeLink(fixedRepo: "MyRepo", fixedBranch: "   "));
    }

    // ─── FindRepoByName ────────────────────────────────────────────────────

    [Fact]
    public void FindRepoByName_returns_repo_when_name_matches_case_insensitive()
    {
        var repos = new List<(string Name, string RemoteUrl, string Id)>
        {
            ("Cobro Pago y Tarifas", "https://example/repo1", "r1"),
            ("Soluciones Transversales", "https://example/repo2", "r2")
        };

        var found = AddLinkToRepoHelpers.FindRepoByName(repos, "cobro pago y tarifas");

        Assert.NotNull(found);
        Assert.Equal("r1", found!.Value.Id);
    }

    [Fact]
    public void FindRepoByName_returns_null_when_name_not_found()
    {
        var repos = new List<(string Name, string RemoteUrl, string Id)>
        {
            ("Cobro Pago y Tarifas", "https://example/repo1", "r1")
        };

        var found = AddLinkToRepoHelpers.FindRepoByName(repos, "NoExiste");

        Assert.Null(found);
    }

    [Fact]
    public void FindRepoByName_returns_null_for_empty_repo_list()
    {
        var found = AddLinkToRepoHelpers.FindRepoByName(
            new List<(string Name, string RemoteUrl, string Id)>(),
            "anything");

        Assert.Null(found);
    }

    // ─── BuildArtifactLinkPatchBody ────────────────────────────────────────

    [Fact]
    public void BuildArtifactLinkPatchBody_emits_single_add_op_to_relations_path()
    {
        var body = AddLinkToRepoHelpers.BuildArtifactLinkPatchBody(
            artifactUrl: "vstfs:///Git/Ref/p/r/GBmain",
            relationType: "Branch");

        Assert.Contains("\"op\": \"add\"", body);
        Assert.Contains("\"/relations/-\"", body);
    }

    [Fact]
    public void BuildArtifactLinkPatchBody_includes_artifactUrl_and_relation_type_in_relation_attributes()
    {
        var body = AddLinkToRepoHelpers.BuildArtifactLinkPatchBody(
            artifactUrl: "vstfs:///Git/Ref/p/r/GBmain",
            relationType: "Branch");

        Assert.Contains("\"rel\": \"ArtifactLink\"", body);
        Assert.Contains("\"url\": \"vstfs:///Git/Ref/p/r/GBmain\"", body);
        Assert.Contains("\"name\": \"Branch\"", body);
    }

    [Fact]
    public void BuildArtifactLinkPatchBody_produces_balanced_json_array_with_single_object()
    {
        var body = AddLinkToRepoHelpers.BuildArtifactLinkPatchBody(
            artifactUrl: "vstfs:///Git/Ref/p/r/GBmain",
            relationType: "Branch");

        var trimmed = body.Trim();
        Assert.StartsWith("[", trimmed);
        Assert.EndsWith("]", trimmed);
        // 3 levels of nested objects: value -> attributes -> (no further), plus the outer op object
        Assert.Equal(3, trimmed.Count(c => c == '{'));
        Assert.Equal(3, trimmed.Count(c => c == '}'));
    }

    // ─── link route end-to-end flag parsing (issue #9 acceptance criteria) ─

    [Fact]
    public void Link_route_parses_repo_and_branch_flags_for_quick_mode_skip()
    {
        // Simulates the full arg vector as it arrives in AzureDevOpsCommand when
        // the user runs: yp azure-devops link Org Proj 22427 --repo MyRepo --branch feature/x
        var flags = AzureDevOpsFlagParser.Parse(new[]
        {
            "link", "SubdepartamentoSolucionesTI", "Cobro Pago y Tarifas", "22427",
            "--repo", "Cobro Pago y Tarifas",
            "--branch", "feature/estado-sps"
        });

        Assert.Equal("Cobro Pago y Tarifas", flags.Repo);
        Assert.Equal("feature/estado-sps", flags.Branch);
        Assert.True(AddLinkToRepoHelpers.IsQuickModeLink(flags.Repo, flags.Branch),
            "link with both --repo and --branch should skip the menu");
    }

    [Fact]
    public void Link_route_without_flags_yields_null_repo_and_branch_so_menu_runs()
    {
        var flags = AzureDevOpsFlagParser.Parse(new[]
        {
            "link", "Org", "Proj", "22427"
        });

        Assert.Null(flags.Repo);
        Assert.Null(flags.Branch);
        Assert.False(AddLinkToRepoHelpers.IsQuickModeLink(flags.Repo, flags.Branch),
            "link with no --repo/--branch should fall back to the interactive menu");
    }

    [Fact]
    public void Link_route_with_only_repo_flag_does_not_skip_menu()
    {
        var flags = AzureDevOpsFlagParser.Parse(new[]
        {
            "link", "Org", "Proj", "22427", "--repo", "MyRepo"
        });

        Assert.Equal("MyRepo", flags.Repo);
        Assert.Null(flags.Branch);
        Assert.False(AddLinkToRepoHelpers.IsQuickModeLink(flags.Repo, flags.Branch),
            "missing --branch must keep the menu flow (the user has to pick the branch)");
    }

    [Fact]
    public void Link_route_with_only_branch_flag_does_not_skip_menu()
    {
        var flags = AzureDevOpsFlagParser.Parse(new[]
        {
            "link", "Org", "Proj", "22427", "--branch", "main"
        });

        Assert.Null(flags.Repo);
        Assert.Equal("main", flags.Branch);
        Assert.False(AddLinkToRepoHelpers.IsQuickModeLink(flags.Repo, flags.Branch),
            "missing --repo must keep the menu flow (the user has to pick the repo)");
    }
}
