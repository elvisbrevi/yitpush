using YitPush;

namespace YitPush.Tests;

public class TaskUpdatePreFlightTests
{
    [Fact]
    public void BuildMissingEvidenceMessage_includes_display_name_refname_and_evidence_flag()
    {
        var msg = TaskUpdatePreFlight.BuildMissingEvidenceMessage(
            displayName: "Evidencias de finalización",
            refName: "Custom.b505c83e-3745-4d8b-b76b-b3086a0c4c71");

        Assert.Contains("Evidencias de finalización", msg);
        Assert.Contains("Custom.b505c83e-3745-4d8b-b76b-b3086a0c4c71", msg);
        Assert.Contains("--evidence", msg);
    }

    [Fact]
    public void BuildMissingEvidenceMessage_omits_refname_when_null()
    {
        var msg = TaskUpdatePreFlight.BuildMissingEvidenceMessage(
            displayName: "Evidencias de finalización",
            refName: null);

        Assert.Contains("Evidencias de finalización", msg);
        Assert.Contains("--evidence", msg);
        Assert.DoesNotContain("refname", msg);
    }

    [Fact]
    public void BuildMissingEvidenceMessage_surfaces_transition_keyword()
    {
        var msg = TaskUpdatePreFlight.BuildMissingEvidenceMessage(
            "Evidencias de finalización",
            "Custom.b505c83e-3745-4d8b-b76b-b3086a0c4c71");

        Assert.Contains("Done", msg);
    }
}
