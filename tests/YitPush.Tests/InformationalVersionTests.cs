using YitPush;

namespace YitPush.Tests;

public class InformationalVersionTests
{
    [Fact]
    public void GetInformationalVersion_returns_non_empty_string()
    {
        var version = Program.GetInformationalVersion();
        Assert.False(string.IsNullOrWhiteSpace(version));
    }

    [Fact]
    public void GetInformationalVersion_strips_build_metadata_suffix()
    {
        var version = Program.GetInformationalVersion();
        Assert.DoesNotContain("+", version);
    }

    [Fact]
    public void GetInformationalVersion_parses_as_a_valid_version()
    {
        var version = Program.GetInformationalVersion();
        Assert.True(System.Version.TryParse(version, out _),
            $"Expected '{version}' to parse as System.Version");
    }
}
