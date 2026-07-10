using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Text;
using YitPush;

namespace YitPush.Tests;

public class ProviderDispatchTests
{
    private static string CachePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        ".yitpush", "models-cache.json");

    public ProviderDispatchTests()
    {
        // Clear the cross-test models cache so each test exercises the live HTTP path.
        try
        {
            if (File.Exists(CachePath)) File.Delete(CachePath);
        }
        catch { /* best effort */ }
    }

    private static string InvokeDefaultBaseUrl(string providerName)
    {
        var method = typeof(Program).GetMethod(
            "GetDefaultBaseUrl",
            BindingFlags.Static | BindingFlags.NonPublic);
        Assert.NotNull(method);
        return (string)method!.Invoke(null, new object[] { providerName })!;
    }

    private static List<string> InvokeDefaultModelsForProvider(string providerName)
    {
        var method = typeof(Program).GetMethod(
            "GetDefaultModelsForProvider",
            BindingFlags.Static | BindingFlags.NonPublic);
        Assert.NotNull(method);
        return (List<string>)method!.Invoke(null, new object[] { providerName })!;
    }

    private static async Task<List<string>> InvokeFetchModelsForProviderAsync(
        string providerKey,
        string apiKey,
        HttpMessageHandler handler,
        string? customBaseUrl = null)
    {
        var method = typeof(Program).GetMethod(
            "FetchModelsForProvider",
            BindingFlags.Static | BindingFlags.NonPublic);
        Assert.NotNull(method);

        var args = new object?[] { providerKey, apiKey, customBaseUrl, handler };
        var task = (Task<List<string>>)method!.Invoke(null, args)!;
        return await task;
    }

    [Fact]
    public void GetDefaultBaseUrl_returns_nvidia_nim_endpoint_for_Nvidia()
    {
        var url = InvokeDefaultBaseUrl("Nvidia");

        Assert.Equal("https://integrate.api.nvidia.com/v1/chat/completions", url);
    }

    [Fact]
    public void GetDefaultBaseUrl_returns_nvidia_nim_endpoint_for_NVIDIA_NIM()
    {
        // The wizard displays the provider as "NVIDIA NIM" but the config key is "Nvidia".
        // When the user types the display name by mistake, we should still produce a usable URL.
        var url = InvokeDefaultBaseUrl("NVIDIA NIM");

        Assert.Equal("https://integrate.api.nvidia.com/v1/chat/completions", url);
    }

    [Fact]
    public void GetDefaultModelsForProvider_returns_fallback_list_for_Nvidia()
    {
        var models = InvokeDefaultModelsForProvider("Nvidia");

        Assert.NotEmpty(models);
        Assert.Contains("meta/llama-3.1-70b-instruct", models);
        Assert.Contains("meta/llama-3.1-8b-instruct", models);
        Assert.Contains("nvidia/nemotron-4-340b-instruct", models);
    }

    [Fact]
    public async Task FetchModelsForProvider_Nvidia_calls_models_endpoint_with_bearer_token()
    {
        var handler = new StubHttpHandler(req =>
        {
            Assert.Equal(HttpMethod.Get, req.Method);
            Assert.Equal("https://integrate.api.nvidia.com/v1/models", req.RequestUri!.ToString());
            Assert.Equal("Bearer nvapi-test-key", req.Headers.Authorization!.ToString());
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    "{\"object\":\"list\",\"data\":[" +
                    "{\"id\":\"meta/llama-3.1-70b-instruct\",\"object\":\"model\"}," +
                    "{\"id\":\"meta/llama-3.1-8b-instruct\",\"object\":\"model\"}," +
                    "{\"id\":\"nvidia/nemotron-4-340b-instruct\",\"object\":\"model\"}" +
                    "]}",
                    Encoding.UTF8,
                    "application/json")
            };
        });

        var models = await InvokeFetchModelsForProviderAsync("Nvidia", "nvapi-test-key", handler);

        Assert.Equal(3, models.Count);
        Assert.Contains("meta/llama-3.1-70b-instruct", models);
        Assert.Contains("meta/llama-3.1-8b-instruct", models);
        Assert.Contains("nvidia/nemotron-4-340b-instruct", models);
    }

    [Fact]
    public async Task FetchModelsForProvider_Nvidia_returns_empty_list_on_non_2xx()
    {
        var handler = new StubHttpHandler(_ => new HttpResponseMessage(HttpStatusCode.Unauthorized)
        {
            Content = new StringContent("invalid api key", Encoding.UTF8, "text/plain")
        });

        var models = await InvokeFetchModelsForProviderAsync("Nvidia", "bad", handler);

        Assert.Empty(models);
    }

    [Fact]
    public async Task FetchModelsForProvider_Nvidia_uses_provided_custom_base_url()
    {
        // For on-prem NIM deployments, the user can override the base URL via pc.BaseUrl.
        var handler = new StubHttpHandler(req =>
        {
            Assert.Equal("https://nim.internal.example/v1/models", req.RequestUri!.ToString());
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""{"data":[{"id":"custom-model"}]}""",
                    Encoding.UTF8, "application/json")
            };
        });

        var models = await InvokeFetchModelsForProviderAsync(
            "Nvidia", "test", handler, customBaseUrl: "https://nim.internal.example/v1/chat/completions");

        Assert.Single(models);
        Assert.Equal("custom-model", models[0]);
    }

    [Fact]
    public void GetAiInfo_routes_Nvidia_to_NIM_base_url_via_env_var()
    {
        // The user runs `NVIDIA_API_KEY=... yp commit` and the tool should pick up the
        // env var override, identify the provider as "Nvidia", and use the NIM base URL.
        var tempHome = Path.Combine(Path.GetTempPath(), $"yp-home-{Guid.NewGuid():N}");
        var ypDir = Path.Combine(tempHome, ".yitpush");
        Directory.CreateDirectory(ypDir);

        try
        {
            var configPath = Path.Combine(ypDir, "config.json");
            File.WriteAllText(configPath, """
            {
              "defaultProvider": "Nvidia",
              "providers": {
                "Nvidia": { "apiKey": "stored-key", "model": "meta/llama-3.1-70b-instruct", "isActive": true }
              }
            }
            """);

            var prevHome = Environment.GetEnvironmentVariable("HOME");
            var prevUserProfile = Environment.GetEnvironmentVariable("USERPROFILE");
            var prevKey = Environment.GetEnvironmentVariable("NVIDIA_API_KEY");
            Environment.SetEnvironmentVariable("HOME", tempHome);
            Environment.SetEnvironmentVariable("USERPROFILE", tempHome);
            Environment.SetEnvironmentVariable("NVIDIA_API_KEY", "env-var-key");
            try
            {
                var (apiKey, model, provider, baseUrl) = InvokeGetAiInfo();

                Assert.Equal("env-var-key", apiKey);
                Assert.Equal("meta/llama-3.1-70b-instruct", model);
                Assert.Equal("Nvidia", provider);
                Assert.Equal("https://integrate.api.nvidia.com/v1/chat/completions", baseUrl);
            }
            finally
            {
                Environment.SetEnvironmentVariable("HOME", prevHome);
                Environment.SetEnvironmentVariable("USERPROFILE", prevUserProfile);
                Environment.SetEnvironmentVariable("NVIDIA_API_KEY", prevKey);
            }
        }
        finally
        {
            try { Directory.Delete(tempHome, recursive: true); } catch { }
        }
    }

    private static (string apiKey, string model, string providerName, string baseUrl) InvokeGetAiInfo()
    {
        var method = typeof(Program).GetMethod(
            "GetAiInfo",
            BindingFlags.Static | BindingFlags.NonPublic);
        Assert.NotNull(method);
        return ((string, string, string, string))method!.Invoke(null, Array.Empty<object>())!;
    }
}
