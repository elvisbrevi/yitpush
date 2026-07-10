using System.Net;
using System.Net.Http;
using System.Text;
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

    [Fact]
    public async Task RunAsync_returns_passed_when_evidence_flag_provided_for_Done_state()
    {
        var handler = new RecordingHttpHandler();
        using var http = new HttpClient(handler);

        var outcome = await TaskUpdatePreFlight.RunAsync(new TaskUpdatePreFlight.PreFlightRequest(
            Http: http,
            OrgUrl: "https://dev.azure.com/org",
            WorkItemId: "22427",
            State: "Done",
            EvidenceFlagValue: "Ver descripción del commit",
            EvidenceRefName: "Custom.b505c83e-3745-4d8b-b76b-b3086a0c4c71",
            EvidenceDisplayName: "Evidencias de finalización"));

        Assert.IsType<TaskUpdatePreFlight.PreFlightPassed>(outcome);
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task RunAsync_returns_passed_when_state_is_not_Done_and_makes_no_http_calls()
    {
        var handler = new RecordingHttpHandler();
        using var http = new HttpClient(handler);

        var outcome = await TaskUpdatePreFlight.RunAsync(new TaskUpdatePreFlight.PreFlightRequest(
            Http: http,
            OrgUrl: "https://dev.azure.com/org",
            WorkItemId: "22427",
            State: "Doing",
            EvidenceFlagValue: null,
            EvidenceRefName: "Custom.b505c83e-3745-4d8b-b76b-b3086a0c4c71",
            EvidenceDisplayName: "Evidencias de finalización"));

        Assert.IsType<TaskUpdatePreFlight.PreFlightPassed>(outcome);
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task RunAsync_returns_passed_when_work_item_already_has_evidence_for_Done_state()
    {
        const string workItemJson = """
        {
          "id": 22427,
          "fields": {
            "System.WorkItemType": "Task",
            "Custom.b505c83e-3745-4d8b-b76b-b3086a0c4c71": "PR #123 contains the fix"
          }
        }
        """;
        var handler = new RecordingHttpHandler(
            _ => new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(workItemJson, Encoding.UTF8, "application/json")
            });
        using var http = new HttpClient(handler);

        var outcome = await TaskUpdatePreFlight.RunAsync(new TaskUpdatePreFlight.PreFlightRequest(
            Http: http,
            OrgUrl: "https://dev.azure.com/org",
            WorkItemId: "22427",
            State: "Done",
            EvidenceFlagValue: null,
            EvidenceRefName: "Custom.b505c83e-3745-4d8b-b76b-b3086a0c4c71",
            EvidenceDisplayName: "Evidencias de finalización"));

        Assert.IsType<TaskUpdatePreFlight.PreFlightPassed>(outcome);
        Assert.Single(handler.Requests);
    }

    [Fact]
    public async Task RunAsync_returns_failed_when_work_item_lacks_evidence_for_Done_state()
    {
        const string workItemJson = """
        {
          "id": 22427,
          "fields": {
            "System.WorkItemType": "Task",
            "Custom.b505c83e-3745-4d8b-b76b-b3086a0c4c71": ""
          }
        }
        """;
        var handler = new RecordingHttpHandler(
            _ => new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(workItemJson, Encoding.UTF8, "application/json")
            });
        using var http = new HttpClient(handler);

        var outcome = await TaskUpdatePreFlight.RunAsync(new TaskUpdatePreFlight.PreFlightRequest(
            Http: http,
            OrgUrl: "https://dev.azure.com/org",
            WorkItemId: "22427",
            State: "Done",
            EvidenceFlagValue: null,
            EvidenceRefName: "Custom.b505c83e-3745-4d8b-b76b-b3086a0c4c71",
            EvidenceDisplayName: "Evidencias de finalización"));

        var failed = Assert.IsType<TaskUpdatePreFlight.PreFlightFailed>(outcome);
        Assert.Contains("Evidencias de finalización", failed.Message);
        Assert.Contains("Custom.b505c83e-3745-4d8b-b76b-b3086a0c4c71", failed.Message);
        Assert.Contains("--evidence", failed.Message);
        Assert.Single(handler.Requests);
    }

    [Fact]
    public async Task RunAsync_returns_skipped_when_GET_returns_500_to_avoid_regression()
    {
        var handler = new RecordingHttpHandler(
            _ => new HttpResponseMessage(HttpStatusCode.InternalServerError)
            {
                Content = new StringContent("server error", Encoding.UTF8, "text/plain")
            });
        using var http = new HttpClient(handler);

        var outcome = await TaskUpdatePreFlight.RunAsync(new TaskUpdatePreFlight.PreFlightRequest(
            Http: http,
            OrgUrl: "https://dev.azure.com/org",
            WorkItemId: "22427",
            State: "Done",
            EvidenceFlagValue: null,
            EvidenceRefName: "Custom.b505c83e-3745-4d8b-b76b-b3086a0c4c71",
            EvidenceDisplayName: "Evidencias de finalización"));

        Assert.IsType<TaskUpdatePreFlight.PreFlightSkipped>(outcome);
    }
}

internal class RecordingHttpHandler : HttpMessageHandler
{
    private readonly Func<HttpRequestMessage, HttpResponseMessage>? _responder;

    public RecordingHttpHandler(Func<HttpRequestMessage, HttpResponseMessage>? responder = null)
    {
        _responder = responder;
    }

    public List<HttpRequestMessage> Requests { get; } = new();

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Requests.Add(request);
        var response = _responder is { } r
            ? r(request)
            : new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{}", Encoding.UTF8, "application/json")
            };
        return Task.FromResult(response);
    }
}
