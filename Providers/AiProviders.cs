using System.Text;
using System.Text.Json;
using Spectre.Console;

namespace YitPush;

partial class Program
{
    private static Task<string> CallDeepSeekApi(string apiKey, string prompt)
        => CallOpenAiCompatibleApi(apiKey, DeepSeekModel, DeepSeekApiUrl, prompt);

    private static (string apiKey, string model, string providerName, string baseUrl) GetAiInfo()
    {
        var config = ConfigManager.Load();

        // Try config first
        if (!string.IsNullOrEmpty(config.DefaultProvider) && config.Providers.ContainsKey(config.DefaultProvider))
        {
            var pc = config.Providers[config.DefaultProvider];
            var apiKey = pc.ApiKey;

            // Env var overrides stored key
            var envVarName = config.DefaultProvider.ToUpperInvariant().Replace(" ", "_") + "_API_KEY";
            var envKey = Environment.GetEnvironmentVariable(envVarName);
            if (!string.IsNullOrEmpty(envKey)) apiKey = envKey;

            var baseUrl = pc.BaseUrl ?? GetDefaultBaseUrl(config.DefaultProvider);
            return (apiKey, pc.Model, config.DefaultProvider, baseUrl);
        }

        // Backward compat: try DEEPSEEK_API_KEY
        var deepseekKey = Environment.GetEnvironmentVariable("DEEPSEEK_API_KEY");
        if (!string.IsNullOrEmpty(deepseekKey))
            return (deepseekKey, "deepseek-chat", "DeepSeek", GetDefaultBaseUrl("DeepSeek"));

        return (string.Empty, string.Empty, string.Empty, string.Empty);
    }

    private static string GetDefaultBaseUrl(string providerName) => providerName switch
    {
        "OpenAI" => "https://api.openai.com/v1/chat/completions",
        "Anthropic" => "https://api.anthropic.com/v1/messages",
        "Google" => "https://generativelanguage.googleapis.com/v1beta",
        "DeepSeek" => "https://api.deepseek.com/v1/chat/completions",
        "OpenRouter" => "https://openrouter.ai/api/v1/chat/completions",
        _ => "https://api.openai.com/v1/chat/completions"
    };

    private static async Task<string> CallAiApi(string prompt)
    {
        var (apiKey, model, providerName, baseUrl) = GetAiInfo();

        if (string.IsNullOrEmpty(apiKey))
        {
            AnsiConsole.MarkupLine("[red]❌ No AI provider configured.[/]");
            AnsiConsole.MarkupLine("Run [cyan]yp setup[/] to configure your AI provider.");
            AnsiConsole.MarkupLine("[dim]Or set DEEPSEEK_API_KEY environment variable for DeepSeek (backward compatible).[/]");
            return string.Empty;
        }

        return providerName switch
        {
            "Anthropic" => await CallAnthropicApi(apiKey, model, prompt),
            "Google" => await CallGeminiApi(apiKey, model, prompt),
            _ => await CallOpenAiCompatibleApi(apiKey, model, baseUrl, prompt)
        };
    }

    private static async Task<string> CallOpenAiCompatibleApi(string apiKey, string model, string baseUrl, string prompt)
    {
        const int maxRetries = 3;
        const int baseDelayMs = 1000;

        for (int attempt = 1; attempt <= maxRetries; attempt++)
        {
            try
            {
                using var httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(ApiTimeoutSeconds) };
                httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiKey}");
                if (baseUrl.Contains("openrouter.ai"))
                {
                    httpClient.DefaultRequestHeaders.Add("HTTP-Referer", "https://github.com/elvisbrevi/yitpush");
                    httpClient.DefaultRequestHeaders.Add("X-Title", "YitPush");
                }

                var requestBody = new
                {
                    model,
                    messages = new[] { new { role = "user", content = prompt } },
                    max_tokens = ApiMaxTokens
                };

                var jsonContent = JsonSerializer.Serialize(requestBody);
                var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");
                var response = await httpClient.PostAsync(baseUrl, content);

                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    Console.WriteLine($"API Error (attempt {attempt}/{maxRetries}): {response.StatusCode}");

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
                    if (attempt < maxRetries)
                    {
                        await Task.Delay(baseDelayMs * (int)Math.Pow(2, attempt - 1));
                        continue;
                    }
                    return string.Empty;
                }

                var message = apiResponse.Choices[0].Message?.Content?.Trim() ?? string.Empty;
                return message.Trim('"', '\'', ' ', '\n', '\r');
            }
            catch (TaskCanceledException ex)
            {
                Console.WriteLine($"Request timeout (attempt {attempt}/{maxRetries}): {ex.Message}");
                if (attempt < maxRetries)
                {
                    await Task.Delay(baseDelayMs * (int)Math.Pow(2, attempt - 1));
                    continue;
                }
                return string.Empty;
            }
            catch (HttpRequestException ex)
            {
                Console.WriteLine($"Network error (attempt {attempt}/{maxRetries}): {ex.Message}");
                if (attempt < maxRetries)
                {
                    await Task.Delay(baseDelayMs * (int)Math.Pow(2, attempt - 1));
                    continue;
                }
                return string.Empty;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error calling API (attempt {attempt}/{maxRetries}): {ex.Message}");
                if (attempt < maxRetries)
                {
                    await Task.Delay(baseDelayMs * (int)Math.Pow(2, attempt - 1));
                    continue;
                }
                return string.Empty;
            }
        }

        return string.Empty;
    }

    private static async Task<string> CallAnthropicApi(string apiKey, string model, string prompt)
    {
        const int maxRetries = 3;
        const int baseDelayMs = 1000;

        for (int attempt = 1; attempt <= maxRetries; attempt++)
        {
            try
            {
                using var httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(ApiTimeoutSeconds) };
                httpClient.DefaultRequestHeaders.Add("x-api-key", apiKey);
                httpClient.DefaultRequestHeaders.Add("anthropic-version", "2023-06-01");

                var requestBody = new
                {
                    model,
                    max_tokens = ApiMaxTokens,
                    messages = new[] { new { role = "user", content = prompt } }
                };

                var jsonContent = JsonSerializer.Serialize(requestBody);
                var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");
                var response = await httpClient.PostAsync("https://api.anthropic.com/v1/messages", content);

                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    Console.WriteLine($"Anthropic API Error (attempt {attempt}/{maxRetries}): {response.StatusCode}");

                    if (attempt < maxRetries && ((int)response.StatusCode == 429 || (int)response.StatusCode >= 500))
                    {
                        var delay = baseDelayMs * (int)Math.Pow(2, attempt - 1);
                        await Task.Delay(delay);
                        continue;
                    }

                    return string.Empty;
                }

                var responseJson = await response.Content.ReadAsStringAsync();
                var doc = JsonDocument.Parse(responseJson);
                var text = doc.RootElement
                    .GetProperty("content")[0]
                    .GetProperty("text")
                    .GetString()?.Trim() ?? string.Empty;
                return text.Trim('"', '\'', ' ', '\n', '\r');
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error calling Anthropic API (attempt {attempt}/{maxRetries}): {ex.Message}");
                if (attempt < maxRetries)
                {
                    await Task.Delay(baseDelayMs * (int)Math.Pow(2, attempt - 1));
                    continue;
                }
                return string.Empty;
            }
        }

        return string.Empty;
    }

    private static async Task<string> CallGeminiApi(string apiKey, string model, string prompt)
    {
        const int maxRetries = 3;
        const int baseDelayMs = 1000;

        for (int attempt = 1; attempt <= maxRetries; attempt++)
        {
            try
            {
                using var httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(ApiTimeoutSeconds) };
                var url = $"https://generativelanguage.googleapis.com/v1beta/models/{model}:generateContent?key={apiKey}";

                var requestBody = new
                {
                    contents = new[] { new { parts = new[] { new { text = prompt } } } }
                };

                var jsonContent = JsonSerializer.Serialize(requestBody);
                var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");
                var response = await httpClient.PostAsync(url, content);

                if (!response.IsSuccessStatusCode)
                {
                    Console.WriteLine($"Gemini API Error (attempt {attempt}/{maxRetries}): {response.StatusCode}");

                    if (attempt < maxRetries && ((int)response.StatusCode == 429 || (int)response.StatusCode >= 500))
                    {
                        var delay = baseDelayMs * (int)Math.Pow(2, attempt - 1);
                        await Task.Delay(delay);
                        continue;
                    }

                    return string.Empty;
                }

                var responseJson = await response.Content.ReadAsStringAsync();
                var doc = JsonDocument.Parse(responseJson);
                var text = doc.RootElement
                    .GetProperty("candidates")[0]
                    .GetProperty("content")
                    .GetProperty("parts")[0]
                    .GetProperty("text")
                    .GetString()?.Trim() ?? string.Empty;
                return text.Trim('"', '\'', ' ', '\n', '\r');
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error calling Gemini API (attempt {attempt}/{maxRetries}): {ex.Message}");
                if (attempt < maxRetries)
                {
                    await Task.Delay(baseDelayMs * (int)Math.Pow(2, attempt - 1));
                    continue;
                }
                return string.Empty;
            }
        }

        return string.Empty;
    }

    private static class ConfigManager
    {
        private static string ConfigPath => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".yitpush", "config.json");

        public static AppConfig Load()
        {
            try
            {
                if (!File.Exists(ConfigPath)) return new AppConfig();
                var json = File.ReadAllText(ConfigPath);
                return JsonSerializer.Deserialize<AppConfig>(json,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                    ?? new AppConfig();
            }
            catch { return new AppConfig(); }
        }

        public static void Save(AppConfig config)
        {
            var dir = Path.GetDirectoryName(ConfigPath)!;
            Directory.CreateDirectory(dir);
            var json = JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(ConfigPath, json);
        }
    }
}
