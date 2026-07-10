using YitPush;

namespace YitPush.Tests;

public class HuTaskPlanTests
{
    [Fact]
    public void ComputePlan_returns_single_with_explicit_title_when_only_title_provided()
    {
        var (mode, titles) = HuTaskPlan.ComputePlan(
            explicitTitle: "WCF: agregar op SOAP",
            taskTitles: null,
            trioRequested: false);

        Assert.Equal(HuTaskPlan.Mode.Single, mode);
        Assert.Equal(new[] { "WCF: agregar op SOAP" }, titles);
    }

    [Fact]
    public void ComputePlan_returns_single_with_Desarrollo_fallback_when_neither_title_nor_task_titles_provided()
    {
        var (mode, titles) = HuTaskPlan.ComputePlan(
            explicitTitle: null,
            taskTitles: null,
            trioRequested: false);

        Assert.Equal(HuTaskPlan.Mode.Single, mode);
        Assert.Equal(new[] { "Desarrollo" }, titles);
    }

    [Fact]
    public void ComputePlan_returns_trio_when_trio_requested()
    {
        var (mode, titles) = HuTaskPlan.ComputePlan(
            explicitTitle: null,
            taskTitles: null,
            trioRequested: true);

        Assert.Equal(HuTaskPlan.Mode.Trio, mode);
        Assert.Equal(new[] { "Desarrollo", "Pruebas Unitarias", "Code Review" }, titles);
    }

    [Fact]
    public void ComputePlan_returns_custom_titles_when_only_task_titles_provided()
    {
        var (mode, titles) = HuTaskPlan.ComputePlan(
            explicitTitle: null,
            taskTitles: "Backend, QA, Docs",
            trioRequested: false);

        Assert.Equal(HuTaskPlan.Mode.CustomTitles, mode);
        Assert.Equal(new[] { "Backend", "QA", "Docs" }, titles);
    }

    [Fact]
    public void ComputePlan_explicit_title_wins_over_task_titles()
    {
        var (mode, titles) = HuTaskPlan.ComputePlan(
            explicitTitle: "WCF: agregar op SOAP",
            taskTitles: "Desarrollo Backend, Pruebas E2E",
            trioRequested: false);

        Assert.Equal(HuTaskPlan.Mode.Single, mode);
        Assert.Equal(new[] { "WCF: agregar op SOAP" }, titles);
    }

    [Fact]
    public void ComputePlan_explicit_title_wins_over_trio()
    {
        var (mode, titles) = HuTaskPlan.ComputePlan(
            explicitTitle: "Tarea única",
            taskTitles: null,
            trioRequested: true);

        Assert.Equal(HuTaskPlan.Mode.Single, mode);
        Assert.Equal(new[] { "Tarea única" }, titles);
    }

    [Fact]
    public void ComputePlan_strips_whitespace_and_skips_empty_in_task_titles()
    {
        var (mode, titles) = HuTaskPlan.ComputePlan(
            explicitTitle: null,
            taskTitles: " A , , B ,  ",
            trioRequested: false);

        Assert.Equal(HuTaskPlan.Mode.CustomTitles, mode);
        Assert.Equal(new[] { "A", "B" }, titles);
    }

    [Fact]
    public void ComputePlan_throws_when_trio_and_task_titles_both_provided()
    {
        var ex = Assert.Throws<ArgumentException>(() => HuTaskPlan.ComputePlan(
            explicitTitle: null,
            taskTitles: "A, B, C",
            trioRequested: true));

        Assert.Contains("--trio", ex.Message);
        Assert.Contains("--task-titles", ex.Message);
    }
}
