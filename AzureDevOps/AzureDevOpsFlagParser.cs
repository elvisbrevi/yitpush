namespace YitPush;

internal record AzureDevOpsFlagParser(
    string? Title,
    string? Description,
    string? Evidence,
    string? History,
    IReadOnlyList<string> ExtraFields)
{
    public static AzureDevOpsFlagParser Parse(string[] args)
    {
        string? title = null;
        string? description = null;
        string? evidence = null;
        string? history = null;
        var extraFields = new List<string>();

        for (int i = 0; i < args.Length; i++)
        {
            if (args[i] == "--title" && i + 1 < args.Length)
            {
                title = args[i + 1];
            }
            if ((args[i] == "--description" || args[i] == "-D") && i + 1 < args.Length)
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
            if (args[i] == "--field" && i + 1 < args.Length)
            {
                extraFields.Add(args[i + 1]);
            }
        }

        return new AzureDevOpsFlagParser(title, description, evidence, history, extraFields);
    }
}
