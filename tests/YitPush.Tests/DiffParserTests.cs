using YitPush;

namespace YitPush.Tests;

public class DiffParserTests
{
    [Fact]
    public void ParseUnifiedDiff_empty_input_returns_empty_list()
    {
        var result = Program.ParseUnifiedDiff(string.Empty);

        Assert.Empty(result);
    }

    [Fact]
    public void ParseUnifiedDiff_single_file_adds_counts_added_and_deleted_lines()
    {
        const string raw = """
        diff --git a/src/Foo.cs b/src/Foo.cs
        index 0000001..1111112 100644
        --- a/src/Foo.cs
        +++ b/src/Foo.cs
        @@ -1,3 +1,4 @@
         line one
        -line two
        +line two (renamed)
        +line two-and-a-half
         line three
        """;

        var result = Program.ParseUnifiedDiff(raw);

        var file = Assert.Single(result);
        Assert.Equal("src/Foo.cs", file.Path);
        Assert.Equal(2, file.Additions);
        Assert.Equal(1, file.Deletions);
        Assert.False(file.IsBinary);
        Assert.False(file.IsRename);
        Assert.Null(file.OldPath);
    }

    [Fact]
    public void ParseUnifiedDiff_multi_file_returns_one_entry_per_file()
    {
        const string raw = """
        diff --git a/src/A.cs b/src/A.cs
        index 0000001..1111112 100644
        --- a/src/A.cs
        +++ b/src/A.cs
        @@ -1,2 +1,3 @@
         keep
        +inserted
         tail
        diff --git a/src/B.cs b/src/B.cs
        index 0000001..2222223 100644
        --- a/src/B.cs
        +++ b/src/B.cs
        @@ -1,2 +1,1 @@
        -gone
         only
        """;

        var result = Program.ParseUnifiedDiff(raw);

        Assert.Equal(2, result.Count);
        Assert.Equal("src/A.cs", result[0].Path);
        Assert.Equal(1, result[0].Additions);
        Assert.Equal(0, result[0].Deletions);
        Assert.Equal("src/B.cs", result[1].Path);
        Assert.Equal(0, result[1].Additions);
        Assert.Equal(1, result[1].Deletions);
    }

    [Fact]
    public void ParseUnifiedDiff_rename_records_old_and_new_path()
    {
        const string raw = """
        diff --git a/old/name.cs b/new/name.cs
        similarity index 95%
        rename from old/name.cs
        rename to new/name.cs
        index 0000001..1111112 100644
        --- a/old/name.cs
        +++ b/new/name.cs
        @@ -1,2 +1,2 @@
         keep
        -old line
        +new line
        """;

        var result = Program.ParseUnifiedDiff(raw);

        var file = Assert.Single(result);
        Assert.True(file.IsRename);
        Assert.Equal("new/name.cs", file.Path);
        Assert.Equal("old/name.cs", file.OldPath);
        Assert.Equal(1, file.Additions);
        Assert.Equal(1, file.Deletions);
    }

    [Fact]
    public void ParseUnifiedDiff_binary_file_marker_sets_IsBinary_and_counts_no_lines()
    {
        const string raw = """
        diff --git a/assets/logo.png b/assets/logo.png
        index 0000001..1111112 100644
        Binary files a/assets/logo.png and b/assets/logo.png differ
        """;

        var result = Program.ParseUnifiedDiff(raw);

        var file = Assert.Single(result);
        Assert.True(file.IsBinary);
        Assert.Equal("assets/logo.png", file.Path);
        Assert.Equal(0, file.Additions);
        Assert.Equal(0, file.Deletions);
        Assert.Empty(file.Hunks);
    }

    [Fact]
    public void ParseUnifiedDiff_no_newline_at_end_of_file_marker_does_not_count_as_add_or_delete()
    {
        const string raw = """
        diff --git a/notes.txt b/notes.txt
        index 0000001..1111112 100644
        --- a/notes.txt
        +++ b/notes.txt
        @@ -1 +1 @@
        -old line
        \ No newline at end of file
        +new line
        \ No newline at end of file
        """;

        var result = Program.ParseUnifiedDiff(raw);

        var file = Assert.Single(result);
        Assert.Equal("notes.txt", file.Path);
        Assert.Equal(1, file.Additions);
        Assert.Equal(1, file.Deletions);
        // The hunk body should round-trip the \ No newline markers without inflating counts.
        var hunk = Assert.Single(file.Hunks);
        Assert.Contains("No newline at end of file", hunk.Content);
    }

    [Fact]
    public void ParseUnifiedDiff_hunk_with_zero_context_omits_count_suffix_in_header()
    {
        // `git diff -U0` emits "@@ -5 +5,2 @@" (no count after the "before" side)
        // and the body is pure +/- lines.
        const string raw = """
        diff --git a/list.txt b/list.txt
        index 0000001..1111112 100644
        --- a/list.txt
        +++ b/list.txt
        @@ -5 +5,2 @@
        -old one
        +new one
        +new two
        """;

        var result = Program.ParseUnifiedDiff(raw);

        var file = Assert.Single(result);
        var hunk = Assert.Single(file.Hunks);
        Assert.Equal(5, hunk.BeforeLine);
        Assert.Equal(5, hunk.AfterLine);
        Assert.Equal(2, file.Additions);
        Assert.Equal(1, file.Deletions);
    }
}
