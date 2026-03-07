# Analysis: Enhancing YitPush (yp) with Multi-AI Support and AI-Friendly Skills

This document outlines the strategy for upgrading `yitpush` (the `yp` tool) to support multiple AI providers and making it easily discoverable and usable by other AI agents (Gemini CLI, Cursor, Claude, etc.).

## 1. Objective
Transform `yp` from a DeepSeek-only tool into a universal AI-powered Git and DevOps assistant with:
*   **Multi-Provider Support**: Seamlessly switch between Anthropic, OpenAI, Google Gemini, and DeepSeek.
*   **Interactive Configuration**: User-friendly setup for API keys, models, and provider preferences.
*   **AI-Friendly Ecosystem**: Implementation of `llms.txt` and specialized skill files for various AI agents.
*   **Persistent Preferences**: Robust configuration management to avoid repeated setup.

## 2. Current State Analysis
*   **Provider**: Hardcoded to DeepSeek (`https://api.deepseek.com/v1/chat/completions`).
*   **Model**: Hardcoded to `deepseek-chat`.
*   **Authentication**: Relies solely on the `DEEPSEEK_API_KEY` environment variable.
*   **Integration**: Direct HTTP calls within `Program.cs`.
*   **Metadata**: Lacks standardized AI-readable documentation (e.g., `llms.txt`).

## 3. Proposed Architecture

### 3.1. Configuration Management (`ConfigManager`)
*   **Storage**: JSON file located at `~/.yitpush/config.json`.
*   **Structure**:
    ```json
    {
      "DefaultProvider": "OpenAI",
      "Providers": {
        "OpenAI": { "ApiKey": "...", "Model": "gpt-4o", "IsActive": true },
        "Anthropic": { "ApiKey": "...", "Model": "claude-3-5-sonnet", "IsActive": false },
        "Google": { "ApiKey": "...", "Model": "gemini-1.5-pro", "IsActive": false },
        "DeepSeek": { "ApiKey": "...", "Model": "deepseek-chat", "IsActive": false }
      }
    }
    ```
*   **Environment Variables**: Continue to support `*_API_KEY` as overrides for the config file.

### 3.2. AI Provider Abstraction (`IAiProvider`)
*   **Interface**:
    ```csharp
    public interface IAiProvider {
        string Name { get; }
        Task<string> GenerateResponse(string prompt, string model, string apiKey);
        Task<List<string>> ListModels(string apiKey);
    }
    ```
*   **Implementations**:
    - `OpenAiProvider`: Uses standard Bearer token and `v1/chat/completions`.
    - `AnthropicProvider`: Uses `x-api-key` and `v1/messages`.
    - `GoogleGeminiProvider`: Uses `x-goog-api-key` or query param and Google's specific JSON structure.
    - `DeepSeekProvider`: Maintains current logic but integrated into the abstraction.

### 3.3. Interactive Setup Flow
*   **Trigger**: Explicitly via `yp setup` or automatically if no provider is configured.
*   **Steps**:
    1. Select Provider (Anthropic, Google, OpenAI, DeepSeek).
    2. Enter API Key (validated via a simple "List Models" call).
    3. Select Model from a list (fetched from the provider's API).
    4. Set as Default.

## 4. AI-Friendly Assets (Skills & Discovery)

To ensure AI agents like Gemini CLI, Cursor, and Claude can use `yp` effectively, we will implement:

| Asset | Target Agent | Description |
| :--- | :--- | :--- |
| `llms.txt` | Universal (Cursor, RAG) | Project map and documentation links. |
| `llms-full.txt` | Universal (RAG) | Complete documentation bundle for easy ingestion. |
| `SKILL.md` | Gemini CLI | Instructions, tool definitions, and usage examples. |
| `.cursorrules` | Cursor / Windsurf | Project-specific rules and coding standards for the agent. |
| `PROJECT_CONTEXT.md`| Claude / Custom Agents | Deep context on the project's architecture and intent. |

## 5. Implementation Checklist

### Phase 1: Configuration & Persistence
- [ ] Define `Config` and `ProviderConfig` models.
- [ ] Implement `ConfigManager` for JSON serialization/deserialization.
- [ ] Create `~/.yitpush` directory and handle permissions.

### Phase 2: Provider Abstraction
- [ ] Create `IAiProvider` interface.
- [ ] Implement `OpenAiProvider`.
- [ ] Implement `AnthropicProvider`.
- [ ] Implement `GoogleGeminiProvider`.
- [ ] Implement `DeepSeekProvider` (refactored from existing code).
- [ ] Add support for "API vs Plan" selection where applicable (e.g., custom endpoints).

### Phase 3: Interactive CLI
- [ ] Implement `yp setup` command using `Spectre.Console`.
- [ ] Implement `yp config` for quick changes (e.g., `yp config set-model gpt-4o`).
- [ ] Update `CommitCommand` and `PrCommand` to use the `ConfigManager` and `IAiProvider`.
- [ ] Add logic to force setup if no provider is configured.

### Phase 4: AI-Friendly Features
- [ ] Generate `llms.txt` with project summary and key command reference.
- [ ] Generate `llms-full.txt` (concatenated README, QUICKSTART, and key docs).
- [ ] Create `SKILL.md` specifically for Gemini CLI skills system.
- [ ] Create `.cursorrules` for repository-wide AI guidance.
- [ ] Create `PROJECT_CONTEXT.md` for Claude Projects.

### Phase 5: Testing & Documentation
- [ ] Test setup flow for each provider.
- [ ] Verify error handling (invalid keys, rate limits).
- [ ] Update `README.md` and `QUICKSTART.md` with multi-provider info.
- [ ] Final validation of "AI-friendliness" by loading the project in Gemini CLI and Cursor.

---
*Created by Gemini CLI Assistant*
