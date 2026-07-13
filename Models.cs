using System.Text.Json.Serialization;

namespace YitPush;

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

// Config models for multi-provider support
public class ProviderConfig
{
    [JsonPropertyName("apiKey")]
    public string ApiKey { get; set; } = string.Empty;

    [JsonPropertyName("model")]
    public string Model { get; set; } = string.Empty;

    [JsonPropertyName("isActive")]
    public bool IsActive { get; set; } = false;

    [JsonPropertyName("baseUrl")]
    public string? BaseUrl { get; set; }
}

public class AppConfig
{
    [JsonPropertyName("defaultProvider")]
    public string DefaultProvider { get; set; } = string.Empty;

    [JsonPropertyName("providers")]
    public Dictionary<string, ProviderConfig> Providers { get; set; } = new();

    // "conventional", "plain", "gitmoji", or a filesystem path to a template file.
    [JsonPropertyName("commitFormat")]
    public string? CommitFormat { get; set; }
}

public class CommitArgs
{
    public bool RequireConfirmation { get; init; }
    public bool Detailed { get; init; }
    public bool Save { get; init; }
    public string Language { get; init; } = "english";
    public bool Conventional { get; init; }
    public string? Type { get; init; }
    public string? Scope { get; init; }
    public bool DetectBreaking { get; init; }
    public bool Amend { get; init; }
    public string? TemplatePath { get; init; }
    // Effective commit format resolved from config + flags. "plain" means default AI free-form.
    public string? Format { get; init; }
    // When true, skip the AnsiConsole.Status() spinner wrapper for the long ops
    // in this command (push, AI gen). Equivalent to the YITPUSH_NO_SPINNER env var.
    public bool NoSpinner { get; init; }
}

class VersionCheckCache
{
    [JsonPropertyName("lastCheck")]
    public DateTime LastCheck { get; set; }

    [JsonPropertyName("latestVersion")]
    public string? LatestVersion { get; set; }

    [JsonPropertyName("releaseNotes")]
    public string? ReleaseNotes { get; set; }
}

class ModelsCacheEntry
{
    [JsonPropertyName("lastCheck")]
    public DateTime LastCheck { get; set; }

    [JsonPropertyName("models")]
    public List<string> Models { get; set; } = new();
}

class ModelsCache
{
    [JsonPropertyName("providers")]
    public Dictionary<string, ModelsCacheEntry> Providers { get; set; } = new();
}

public class DiffHunk
{
    [JsonPropertyName("beforeLine")]
    public int BeforeLine { get; set; }

    [JsonPropertyName("afterLine")]
    public int AfterLine { get; set; }

    [JsonPropertyName("content")]
    public string Content { get; set; } = string.Empty;
}

public class DiffFile
{
    [JsonPropertyName("path")]
    public string Path { get; set; } = string.Empty;

    [JsonPropertyName("oldPath")]
    public string? OldPath { get; set; }

    [JsonPropertyName("additions")]
    public int Additions { get; set; }

    [JsonPropertyName("deletions")]
    public int Deletions { get; set; }

    [JsonPropertyName("isBinary")]
    public bool IsBinary { get; set; }

    [JsonPropertyName("isRename")]
    public bool IsRename { get; set; }

    [JsonPropertyName("hunks")]
    public List<DiffHunk> Hunks { get; set; } = new();
}

public class PrSummary
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    [JsonPropertyName("author")]
    public string Author { get; set; } = string.Empty;

    [JsonPropertyName("sourceBranch")]
    public string SourceBranch { get; set; } = string.Empty;

    [JsonPropertyName("targetBranch")]
    public string TargetBranch { get; set; } = string.Empty;

    [JsonPropertyName("isDraft")]
    public bool IsDraft { get; set; }

    [JsonPropertyName("creationDate")]
    public string CreationDate { get; set; } = string.Empty;
}

public class PrReviewer
{
    [JsonPropertyName("displayName")]
    public string DisplayName { get; set; } = string.Empty;

    [JsonPropertyName("vote")]
    public int Vote { get; set; }

    [JsonPropertyName("isRequired")]
    public bool IsRequired { get; set; }
}

public class PrChangedFile
{
    [JsonPropertyName("path")]
    public string Path { get; set; } = string.Empty;
}

public class PrDetail
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    [JsonPropertyName("sourceBranch")]
    public string SourceBranch { get; set; } = string.Empty;

    [JsonPropertyName("targetBranch")]
    public string TargetBranch { get; set; } = string.Empty;

    [JsonPropertyName("author")]
    public string Author { get; set; } = string.Empty;

    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;

    [JsonPropertyName("isDraft")]
    public bool IsDraft { get; set; }

    [JsonPropertyName("creationDate")]
    public string CreationDate { get; set; } = string.Empty;

    [JsonPropertyName("reviewers")]
    public List<PrReviewer> Reviewers { get; set; } = new();

    [JsonPropertyName("changedFiles")]
    public List<PrChangedFile> ChangedFiles { get; set; } = new();
}
