using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace YitPush;

class Program
{
    private const string DeepSeekApiUrl = "https://api.deepseek.com/v1/chat/completions";
    private const string DeepSeekModel = "deepseek-reasoner";

    static async Task<int> Main(string[] args)
    {
        // Configure console encoding for Unicode support (especially Windows PowerShell)
        try
        {
            Console.OutputEncoding = Encoding.UTF8;
            Console.InputEncoding = Encoding.UTF8;
        }
        catch
        {
            // Ignore encoding errors - console may not support UTF-8
        }

    Console.WriteLine("🚀 YitPush - AI-Powered Git Commit Tool\n");

    // Process command line arguments
    bool requireConfirmation = args.Contains("--confirm");
    bool showHelp = args.Contains("--help");
    bool detailed = args.Contains("--detailed");
    string language = "english"; // default language
    
    // Parse language flag (supports --language es, --lang es, --language=es, --lang=es)
    for (int i = 0; i < args.Length; i++)
    {
        if (args[i] == "--language" || args[i] == "--lang")
        {
            if (i + 1 < args.Length && !args[i + 1].StartsWith("--"))
            {
                language = args[i + 1];
            }
            break;
        }
        else if (args[i].StartsWith("--language=") || args[i].StartsWith("--lang="))
        {
            language = args[i].Split('=', 2)[1];
            break;
        }
    }
    
    if (showHelp)
    {
        Console.WriteLine("Usage: yitpush [options]");
        Console.WriteLine();
        Console.WriteLine("Options:");
        Console.WriteLine("  --confirm    Ask for confirmation before committing");
        Console.WriteLine("  --detailed   Generate detailed commit with body (title + paragraphs + bullet points)");
        Console.WriteLine("  --language   Set output language for commit message (e.g., 'english', 'spanish', 'french')");
        Console.WriteLine("  --help       Show this help message");
        Console.WriteLine();
        Console.WriteLine("By default, YitPush will automatically commit and push without confirmation.");
        Console.WriteLine("Use --confirm if you want to review the commit message before proceeding.");
        Console.WriteLine("Use --detailed for detailed commit messages with full explanations.");
        Console.WriteLine("Use --language to specify the output language (default: english).");
        Console.WriteLine();
        return 0;
    }

    try
        {
            // Get API key from environment variable
            var apiKey = Environment.GetEnvironmentVariable("DEEPSEEK_API_KEY");
            if (string.IsNullOrWhiteSpace(apiKey))
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("❌ Error: DEEPSEEK_API_KEY environment variable not found.");
                Console.ResetColor();
                Console.WriteLine("\nPlease set your DeepSeek API key:");
                Console.WriteLine("  export DEEPSEEK_API_KEY='your-api-key-here'");
                return 1;
            }

            // Check if we're in a git repository
            if (!await IsGitRepository())
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("❌ Error: Not a git repository.");
                Console.ResetColor();
                Console.WriteLine("\nPlease run this command from within a git repository.");
                return 1;
            }

            // Get git diff
            Console.WriteLine("📊 Analyzing git changes...");
            var diff = await GetGitDiff();

            bool hasChanges = !string.IsNullOrWhiteSpace(diff);
            
            if (hasChanges)
            {
                Console.WriteLine($"Found changes ({diff.Length} characters)\n");
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine("⚠️  No changes detected in the repository.");
                Console.ResetColor();
                Console.WriteLine("\nWill attempt to push existing commits...\n");
            }

            string? commitMessage = null;
            
            if (hasChanges)
            {
                // Get commit message from DeepSeek
                Console.WriteLine($"🤖 Generating commit message with DeepSeek Reasoning...{(detailed ? " (detailed mode)" : "")}");
                commitMessage = await GenerateCommitMessage(apiKey, diff, detailed, language);

                if (string.IsNullOrWhiteSpace(commitMessage))
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("❌ Error: Failed to generate commit message.");
                    Console.ResetColor();
                    return 1;
                }

                Console.WriteLine("\n📝 Generated commit message:");
                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.WriteLine($"   \"{commitMessage}\"");
                Console.ResetColor();
                Console.WriteLine();

                // Confirm with user if --confirm flag is set
                if (requireConfirmation)
                {
                    Console.Write("Do you want to proceed with this commit? (y/n): ");
                    var response = Console.ReadLine()?.Trim().ToLower();

                    if (response != "y" && response != "yes")
                    {
                        Console.WriteLine("\n❌ Commit cancelled.");
                        return 0;
                    }
                }
                else
                {
                    Console.WriteLine("⏩ Proceeding automatically (use --confirm to review)...");
                }
            }
            else
            {
                // No changes, just push - confirm if in confirmation mode
                if (requireConfirmation)
                {
                    Console.Write("No changes to commit. Do you want to push existing commits? (y/n): ");
                    var response = Console.ReadLine()?.Trim().ToLower();

                    if (response != "y" && response != "yes")
                    {
                        Console.WriteLine("\n❌ Push cancelled.");
                        return 0;
                    }
                }
                else
                {
                    Console.WriteLine("⏩ No changes to commit, proceeding with push...");
                }
            }

            // Execute git commands
            Console.WriteLine("\n⚙️  Executing git commands...");

            if (hasChanges)
            {
                // git add .
                Console.WriteLine("   git add .");
                if (!await ExecuteGitCommand("add ."))
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("❌ Error: git add failed.");
                    Console.ResetColor();
                    return 1;
                }

                // git commit
                Console.WriteLine($"   git commit -m \"{commitMessage}\"");
                if (!await ExecuteGitCommand($"commit -m \"{commitMessage}\""))
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("❌ Error: git commit failed.");
                    Console.ResetColor();
                    return 1;
                }
            }

            // git push
            if (!await ExecuteGitPush(requireConfirmation))
            {
                return 1;
            }

            Console.ForegroundColor = ConsoleColor.Green;
            if (hasChanges)
            {
                Console.WriteLine("\n✅ Successfully committed and pushed changes!");
            }
            else
            {
                Console.WriteLine("\n✅ Successfully pushed changes!");
            }
            Console.ResetColor();

            return 0;
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"\n❌ Unexpected error: {ex.Message}");
            Console.ResetColor();
            return 1;
        }
    }

    private static async Task<bool> IsGitRepository()
    {
        try
        {
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "git",
                    Arguments = "rev-parse --git-dir",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            process.Start();
            await process.WaitForExitAsync();
            return process.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    private static async Task<string> GetGitDiff()
    {
        try
        {
            // First, stage all changes (including untracked files) to get a complete diff
            var addProcess = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "git",
                    Arguments = "add -A --dry-run",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };
            addProcess.Start();
            await addProcess.WaitForExitAsync();

            // Get diff including untracked files
            // First try to get already staged changes
            var stagedProcess = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "git",
                    Arguments = "diff --cached",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };
            stagedProcess.Start();
            var stagedOutput = await stagedProcess.StandardOutput.ReadToEndAsync();
            await stagedProcess.WaitForExitAsync();

            // Get unstaged changes in tracked files
            var unstagedProcess = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "git",
                    Arguments = "diff",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };
            unstagedProcess.Start();
            var unstagedOutput = await unstagedProcess.StandardOutput.ReadToEndAsync();
            await unstagedProcess.WaitForExitAsync();

            // Get untracked files
            var untrackedProcess = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "git",
                    Arguments = "ls-files --others --exclude-standard",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };
            untrackedProcess.Start();
            var untrackedFiles = await untrackedProcess.StandardOutput.ReadToEndAsync();
            await untrackedProcess.WaitForExitAsync();

            var output = new StringBuilder();

            if (!string.IsNullOrWhiteSpace(stagedOutput))
            {
                output.AppendLine("=== Staged Changes ===");
                output.AppendLine(stagedOutput);
                output.AppendLine();
            }

            if (!string.IsNullOrWhiteSpace(unstagedOutput))
            {
                output.AppendLine("=== Unstaged Changes ===");
                output.AppendLine(unstagedOutput);
                output.AppendLine();
            }

            if (!string.IsNullOrWhiteSpace(untrackedFiles))
            {
                output.AppendLine("=== New Files (Untracked) ===");
                output.AppendLine(untrackedFiles);
            }

            return output.ToString();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error getting git diff: {ex.Message}");
            return string.Empty;
        }
    }

    private static async Task<string> GenerateCommitMessage(string apiKey, string diff, bool detailed = false, string language = "english")
    {
        const int maxRetries = 3;
        const int baseDelayMs = 1000;


        for (int attempt = 1; attempt <= maxRetries; attempt++)
        {
            try
            {
                using var httpClient = new HttpClient
                {
                    Timeout = TimeSpan.FromSeconds(120) // Increased timeout for reasoning models
                };
                httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiKey}");

                string prompt;
                if (detailed)
                {
                    prompt = $@"You are a git commit message expert. Based on the following git diff, generate a detailed conventional commit with title and body.

LANGUAGE: Write the commit message in {language}.

FORMAT REQUIREMENTS:
1. TITLE LINE (required):
   - Start with conventional commit type: feat, fix, docs, style, refactor, test, chore
   - Use imperative mood: 'add' not 'added' or 'adds'
   - Max 50 characters for the subject line
   - Example: 'feat: add user authentication with JWT'

2. BODY (required for detailed mode):
   - Add one blank line after title
   - Include 1-2 descriptive paragraphs explaining the changes
   - Add bullet points for key changes (use '-' or '*')
   - Mention important files modified or added
   - Keep lines under 72 characters for git compatibility
   - Focus on the 'why' not just the 'what'

3. STYLE:
    - Write in clear, professional {language}
   - Use present tense for changes
   - Be specific about technical implementation

Git diff:
{diff}

Generate the complete commit message (title + body):";
                }
                else
                {
                    prompt = $@"You are a git commit message expert. Based on the following git diff, generate a concise, clear, and descriptive commit message following conventional commits format.

LANGUAGE: Write the commit message in {language}.

The commit message should:
- Start with a type (feat, fix, docs, style, refactor, test, chore)
- Be in imperative mood (e.g., 'add' not 'added' or 'adds')
- Be concise but descriptive (50 characters or less for the subject)
- Not include any extra formatting, quotes, or explanations
- Just return the commit message itself, nothing else

Git diff:
{diff}

Generate only the commit message:";
                }

                var requestBody = new
                {
                    model = DeepSeekModel,
                    messages = new[]
                    {
                        new { role = "user", content = prompt }
                    },
                    max_tokens = 8000  // Increased for reasoning models to complete both reasoning and final answer
                };

                var jsonContent = JsonSerializer.Serialize(requestBody);
                var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

                var response = await httpClient.PostAsync(DeepSeekApiUrl, content);

                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    Console.WriteLine($"API Error (attempt {attempt}/{maxRetries}): {response.StatusCode}");
                    Console.WriteLine($"Response: {errorContent}");

                    // If it's a rate limit or server error, retry
                    if (attempt < maxRetries && ((int)response.StatusCode == 429 || (int)response.StatusCode >= 500))
                    {
                        var delay = baseDelayMs * (int)Math.Pow(2, attempt - 1);
                        Console.WriteLine($"Retrying in {delay}ms...");
                        await Task.Delay(delay);
                        continue;
                    }

                    return string.Empty;
                }

                var responseJson = await response.Content.ReadAsStringAsync();
                var apiResponse = JsonSerializer.Deserialize<DeepSeekResponse>(responseJson);

                if (apiResponse?.Choices == null || apiResponse.Choices.Length == 0)
                {
                    Console.WriteLine($"No response from API (attempt {attempt}/{maxRetries})");

                    if (attempt < maxRetries)
                    {
                        var delay = baseDelayMs * (int)Math.Pow(2, attempt - 1);
                        Console.WriteLine($"Retrying in {delay}ms...");
                        await Task.Delay(delay);
                        continue;
                    }

                    return string.Empty;
                }

                var message = apiResponse.Choices[0].Message?.Content?.Trim() ?? string.Empty;

                // Clean up the message (remove quotes if present)
                message = message.Trim('"', '\'', ' ', '\n', '\r');

                return message;
            }
            catch (TaskCanceledException ex)
            {
                Console.WriteLine($"Request timeout (attempt {attempt}/{maxRetries}): {ex.Message}");

                if (attempt < maxRetries)
                {
                    var delay = baseDelayMs * (int)Math.Pow(2, attempt - 1);
                    Console.WriteLine($"Retrying in {delay}ms...");
                    await Task.Delay(delay);
                    continue;
                }

                return string.Empty;
            }
            catch (HttpRequestException ex)
            {
                Console.WriteLine($"Network error (attempt {attempt}/{maxRetries}): {ex.Message}");

                if (attempt < maxRetries)
                {
                    var delay = baseDelayMs * (int)Math.Pow(2, attempt - 1);
                    Console.WriteLine($"Retrying in {delay}ms...");
                    await Task.Delay(delay);
                    continue;
                }

                return string.Empty;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error calling DeepSeek API (attempt {attempt}/{maxRetries}): {ex.Message}");

                if (attempt < maxRetries)
                {
                    var delay = baseDelayMs * (int)Math.Pow(2, attempt - 1);
                    Console.WriteLine($"Retrying in {delay}ms...");
                    await Task.Delay(delay);
                    continue;
                }

                return string.Empty;
            }
        }

        return string.Empty;
    }

    private static async Task<string?> GetCurrentBranch()
    {
        try
        {
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "git",
                    Arguments = "branch --show-current",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            process.Start();
            var output = await process.StandardOutput.ReadToEndAsync();
            await process.StandardError.ReadToEndAsync(); // discard
            await process.WaitForExitAsync();

            if (process.ExitCode == 0)
            {
                return output.Trim();
            }
            else
            {
                return null;
            }
        }
        catch
        {
            return null;
        }
    }

    private static async Task<bool> ExecuteGitPush(bool requireConfirmation)
    {
        // First try regular push
        Console.WriteLine("   git push");
        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "git",
                Arguments = "push",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };

        process.Start();
        var output = await process.StandardOutput.ReadToEndAsync();
        var error = await process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();

        if (process.ExitCode == 0)
        {
            return true;
        }

        // Check if error indicates no upstream branch
        if (!string.IsNullOrEmpty(error) && (error.Contains("no upstream branch") || error.Contains("has no upstream branch")))
        {
            // In automatic mode, set upstream automatically
            if (!requireConfirmation)
            {
                var currentBranch = await GetCurrentBranch();
                if (currentBranch != null)
                {
                    Console.WriteLine($"   git push --set-upstream origin {currentBranch}");
                    var upstreamProcess = new Process
                    {
                        StartInfo = new ProcessStartInfo
                        {
                            FileName = "git",
                            Arguments = $"push --set-upstream origin {currentBranch}",
                            RedirectStandardOutput = true,
                            RedirectStandardError = true,
                            UseShellExecute = false,
                            CreateNoWindow = true
                        }
                    };

                    upstreamProcess.Start();
                    await upstreamProcess.StandardOutput.ReadToEndAsync();
                    var upstreamError = await upstreamProcess.StandardError.ReadToEndAsync();
                    await upstreamProcess.WaitForExitAsync();

                    if (upstreamProcess.ExitCode == 0)
                    {
                        return true;
                    }
                    else
                    {
                        Console.WriteLine($"Git error: {upstreamError}");
                        return false;
                    }
                }
            }
            else
            {
                // In confirmation mode, ask user if they want to set upstream
                if (!string.IsNullOrEmpty(error))
                {
                    Console.WriteLine($"Git error: {error}");
                }
                
                var currentBranch = await GetCurrentBranch();
                if (currentBranch != null)
                {
                    Console.WriteLine($"\nThe current branch '{currentBranch}' has no upstream branch.");
                    Console.Write($"Do you want to push and set upstream to origin/{currentBranch}? (y/n): ");
                    var response = Console.ReadLine()?.Trim().ToLower();

                    if (response == "y" || response == "yes")
                    {
                        Console.WriteLine($"   git push --set-upstream origin {currentBranch}");
                        var upstreamProcess = new Process
                        {
                            StartInfo = new ProcessStartInfo
                            {
                                FileName = "git",
                                Arguments = $"push --set-upstream origin {currentBranch}",
                                RedirectStandardOutput = true,
                                RedirectStandardError = true,
                                UseShellExecute = false,
                                CreateNoWindow = true
                            }
                        };

                        upstreamProcess.Start();
                        await upstreamProcess.StandardOutput.ReadToEndAsync();
                        var upstreamError = await upstreamProcess.StandardError.ReadToEndAsync();
                        await upstreamProcess.WaitForExitAsync();

                        if (upstreamProcess.ExitCode == 0)
                        {
                            return true;
                        }
                        else
                        {
                            Console.WriteLine($"Git error: {upstreamError}");
                            return false;
                        }
                    }
                    else
                    {
                        Console.WriteLine("Push cancelled.");
                        return false;
                    }
                }
                else
                {
                    Console.WriteLine("\nNote: The current branch has no upstream branch.");
                    Console.WriteLine("      You can set it with: git push --set-upstream origin <branch-name>");
                    return false;
                }
            }
        }

        if (!string.IsNullOrEmpty(error))
        {
            Console.WriteLine($"Git error: {error}");
        }
        return false;
    }

    private static async Task<bool> ExecuteGitCommand(string arguments)
    {
        try
        {
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "git",
                    Arguments = arguments,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            process.Start();
            var output = await process.StandardOutput.ReadToEndAsync();
            var error = await process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();

            if (!string.IsNullOrWhiteSpace(error) && process.ExitCode != 0)
            {
                Console.WriteLine($"Git error: {error}");
            }

            return process.ExitCode == 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error executing git command: {ex.Message}");
            return false;
        }
    }
}

// JSON models for DeepSeek API
class DeepSeekResponse
{
    [JsonPropertyName("choices")]
    public Choice[]? Choices { get; set; }
}

class Choice
{
    [JsonPropertyName("message")]
    public Message? Message { get; set; }
}

class Message
{
    [JsonPropertyName("content")]
    public string? Content { get; set; }

    [JsonPropertyName("reasoning_content")]
    public string? ReasoningContent { get; set; }
}
