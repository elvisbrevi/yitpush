namespace YitPush;

internal static class HuTaskPlan
{
    public enum Mode
    {
        Single,
        Trio,
        CustomTitles
    }

    public static (Mode Mode, IReadOnlyList<string> Titles) ComputePlan(
        string? explicitTitle,
        string? taskTitles,
        bool trioRequested)
    {
        if (trioRequested && !string.IsNullOrEmpty(taskTitles))
        {
            throw new ArgumentException(
                "--trio and --task-titles are mutually exclusive. Pick one.");
        }

        if (!string.IsNullOrWhiteSpace(explicitTitle))
        {
            return (Mode.Single, new[] { explicitTitle });
        }

        if (trioRequested)
        {
            return (Mode.Trio, new[] { "Desarrollo", "Pruebas Unitarias", "Code Review" });
        }

        if (!string.IsNullOrEmpty(taskTitles))
        {
            var list = taskTitles
                .Split(',')
                .Select(t => t.Trim())
                .Where(t => !string.IsNullOrEmpty(t))
                .ToList();
            return (Mode.CustomTitles, list);
        }

        return (Mode.Single, new[] { "Desarrollo" });
    }
}
