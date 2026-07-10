namespace YitPush;

internal record AzureDevOpsFlagParser(
    string? Title,
    string? Description,
    string? Evidence,
    string? History,
    string? Comment,
    string? Effort,
    string? EffortReal,
    string? Remaining,
    string? State,
    string? Repo,
    string? Branch,
    string? TaskTitles,
    bool NoLink,
    bool Trio,
    IReadOnlyList<string> ExtraFields,
    bool Json = false,
    bool Yes = false,
    string? AssignedTo = null)
{
    public static AzureDevOpsFlagParser Parse(string[] args)
    {
        string? title = null;
        string? description = null;
        string? evidence = null;
        string? history = null;
        string? comment = null;
        string? effort = null;
        string? effortReal = null;
        string? remaining = null;
        string? state = null;
        string? repo = null;
        string? branch = null;
        string? taskTitles = null;
        bool noLink = false;
        bool trio = false;
        var extraFields = new List<string>();
        bool json = false;
        bool yes = false;
        string? assignedTo = null;

        for (int i = 0; i < args.Length; i++)
        {
            if (args[i] == "--title" && i + 1 < args.Length)
            {
                title = args[i + 1];
            }
            if ((args[i] == "--description" || args[i] == "-D" || args[i] == "-d") && i + 1 < args.Length)
            {
                description = args[i + 1];
            }
            if (args[i] == "--evidence" && i + 1 < args.Length)
            {
                evidence = args[i + 1];
            }
            if (args[i] == "--history" && i + 1 < args.Length)
            {
                history = args[i + 1];
            }
            if ((args[i] == "--comment" || args[i] == "-c") && i + 1 < args.Length)
            {
                comment = args[i + 1];
            }
            if ((args[i] == "--effort" || args[i] == "-e") && i + 1 < args.Length)
            {
                effort = args[i + 1];
            }
            if ((args[i] == "--effort-real" || args[i] == "-er") && i + 1 < args.Length)
            {
                effortReal = args[i + 1];
            }
            if ((args[i] == "--remaining" || args[i] == "-r") && i + 1 < args.Length)
            {
                remaining = args[i + 1];
            }
            if ((args[i] == "--state" || args[i] == "-s") && i + 1 < args.Length)
            {
                state = args[i + 1];
            }
            if (args[i] == "--repo" && i + 1 < args.Length)
            {
                repo = args[i + 1];
            }
            if (args[i] == "--branch" && i + 1 < args.Length)
            {
                branch = args[i + 1];
            }
            if ((args[i] == "--task-titles" || args[i] == "-t") && i + 1 < args.Length)
            {
                taskTitles = args[i + 1];
            }
            if (args[i] == "--no-link" || args[i] == "-n")
            {
                noLink = true;
            }
            if (args[i] == "--trio")
            {
                trio = true;
            }
            if (args[i] == "--field" && i + 1 < args.Length)
            {
                extraFields.Add(args[i + 1]);
            }
            if (args[i] == "--json")
            {
                json = true;
            }
            if (args[i] == "--yes" || args[i] == "-y")
            {
                yes = true;
            }
            if (args[i] == "--assigned-to" && i + 1 < args.Length)
            {
                assignedTo = args[i + 1];
            }
        }

        return new AzureDevOpsFlagParser(
            title, description, evidence, history, comment,
            effort, effortReal, remaining, state,
            repo, branch, taskTitles, noLink, trio,
            extraFields, json, yes, assignedTo);
    }
}
