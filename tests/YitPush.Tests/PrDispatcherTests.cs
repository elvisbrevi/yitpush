using YitPush;

namespace YitPush.Tests;

public class PrDispatcherTests
{
    [Fact]
    public void Route_no_args_returns_Menu()
    {
        var route = Program.PrDispatcher.Route(Array.Empty<string>());

        Assert.Equal(Program.PrRouteKind.Menu, route.Kind);
    }

    [Fact]
    public void Route_known_subcommand_list_returns_List()
    {
        var route = Program.PrDispatcher.Route(new[] { "list" });

        Assert.Equal(Program.PrRouteKind.List, route.Kind);
    }

    [Fact]
    public void Route_known_subcommand_show_with_id_returns_Show()
    {
        var route = Program.PrDispatcher.Route(new[] { "show", "12345" });

        Assert.Equal(Program.PrRouteKind.Show, route.Kind);
        Assert.Equal("12345", route.PrId);
    }

    [Fact]
    public void Route_show_without_id_returns_Show()
    {
        var route = Program.PrDispatcher.Route(new[] { "show" });

        Assert.Equal(Program.PrRouteKind.Show, route.Kind);
        Assert.Equal("", route.PrId);
    }

    [Fact]
    public void Route_known_subcommand_comments_with_id_returns_Comments()
    {
        var route = Program.PrDispatcher.Route(new[] { "comments", "999" });

        Assert.Equal(Program.PrRouteKind.Comments, route.Kind);
        Assert.Equal("999", route.PrId);
    }

    [Fact]
    public void Route_reply_with_prId_threadId_and_body_returns_Reply()
    {
        var route = Program.PrDispatcher.Route(new[] {
            "reply", "12345", "678", "--body", "Fixed in abc"
        });

        Assert.Equal(Program.PrRouteKind.Reply, route.Kind);
        Assert.Equal("12345", route.PrId);
        Assert.Equal("678", route.ThreadId);
        Assert.Equal("Fixed in abc", route.Body);
    }

    [Fact]
    public void Route_create_with_source_target_title_and_body_file_returns_Create()
    {
        var route = Program.PrDispatcher.Route(new[] {
            "create",
            "--source", "feature/x",
            "--target", "main",
            "--title", "feat: x",
            "--body-file", "desc.md"
        });

        Assert.Equal(Program.PrRouteKind.Create, route.Kind);
        Assert.Equal("feature/x", route.Source);
        Assert.Equal("main", route.Target);
        Assert.Equal("feat: x", route.Title);
        Assert.Equal("desc.md", route.BodyFile);
        Assert.False(route.AutoComplete);
    }

    [Fact]
    public void Route_create_with_auto_complete_flag_sets_AutoComplete()
    {
        var route = Program.PrDispatcher.Route(new[] {
            "create",
            "--source", "feature/x",
            "--target", "main",
            "--title", "feat: x",
            "--auto-complete"
        });

        Assert.Equal(Program.PrRouteKind.Create, route.Kind);
        Assert.True(route.AutoComplete);
    }

    [Fact]
    public void Route_create_without_title_returns_Create_with_validation_flag()
    {
        var route = Program.PrDispatcher.Route(new[] {
            "create",
            "--source", "feature/x",
            "--target", "main"
        });

        Assert.Equal(Program.PrRouteKind.Create, route.Kind);
        Assert.True(route.ValidationError);
    }

    [Fact]
    public void Route_create_with_empty_title_returns_Create_with_validation_flag()
    {
        var route = Program.PrDispatcher.Route(new[] {
            "create",
            "--source", "feature/x",
            "--target", "main",
            "--title", ""
        });

        Assert.Equal(Program.PrRouteKind.Create, route.Kind);
        Assert.True(route.ValidationError);
    }

    [Fact]
    public void Route_create_without_source_or_target_returns_Create_with_validation_flag()
    {
        var route = Program.PrDispatcher.Route(new[] {
            "create", "--title", "x"
        });

        Assert.Equal(Program.PrRouteKind.Create, route.Kind);
        Assert.True(route.ValidationError);
    }

    [Fact]
    public void Route_reply_without_body_returns_Reply_with_validation_flag()
    {
        var route = Program.PrDispatcher.Route(new[] { "reply", "1", "2" });

        Assert.Equal(Program.PrRouteKind.Reply, route.Kind);
        Assert.True(route.ValidationError);
    }

    [Fact]
    public void Route_unknown_non_flag_subcommand_returns_Unknown()
    {
        var route = Program.PrDispatcher.Route(new[] { "wat" });

        Assert.Equal(Program.PrRouteKind.Unknown, route.Kind);
        Assert.False(route.ValidationError);
    }

    [Fact]
    public void Route_ai_flags_without_subcommand_returns_Ai()
    {
        var route = Program.PrDispatcher.Route(new[] { "--detailed", "--save", "-l", "spanish" });

        Assert.Equal(Program.PrRouteKind.Ai, route.Kind);
        Assert.True(route.Detailed);
        Assert.True(route.Save);
        Assert.Equal("spanish", route.Language);
    }

    [Fact]
    public void Route_no_args_no_flags_is_Menu_not_Ai()
    {
        // The new default: `yp pr` (no args) shows the interactive menu.
        // AI mode is opt-in via the dispatcher (or by passing --detailed).
        var route = Program.PrDispatcher.Route(Array.Empty<string>());

        Assert.Equal(Program.PrRouteKind.Menu, route.Kind);
        Assert.NotEqual(Program.PrRouteKind.Ai, route.Kind);
    }

    [Fact]
    public void Route_subcommand_with_json_flag_sets_Json()
    {
        var route = Program.PrDispatcher.Route(new[] { "list", "--json" });

        Assert.Equal(Program.PrRouteKind.List, route.Kind);
        Assert.True(route.Json);
    }

    [Fact]
    public void Route_create_with_body_via_stdin_marker_returns_Create()
    {
        // When --body-file is not provided the description is read from stdin;
        // we still classify it as a Create route so the dispatcher can call
        // Console.In.ReadToEndAsync at the right time.
        var route = Program.PrDispatcher.Route(new[] {
            "create",
            "--source", "f",
            "--target", "main",
            "--title", "t"
        });

        Assert.Equal(Program.PrRouteKind.Create, route.Kind);
        Assert.Null(route.BodyFile);
    }
}
