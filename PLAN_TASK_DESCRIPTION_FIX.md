# Plan: Fix Task Description Formatting (v2.1.8)

## Context

`yp` (YitPush) is a .NET CLI tool for AI-powered git workflow and Azure DevOps management. The command `yp azure-devops hu task` creates child tasks under a User Story (HU).

### The Problem

When creating tasks with `--description` containing Markdown (e.g., piped from a `.md` file), the description appears as raw unformatted text in Azure DevOps — no line breaks, no headers rendered, no list formatting.

**Example command that fails:**
```bash
yp azure-devops hu task MyOrg "MyProj" 19362 \
  --description "$(cat /path/to/desc.md)" --effort 40
```

### Root Cause

Azure DevOps work item descriptions are plain text stored in `System.Description`. Multiple issues compound:

1. **Shell newline stripping**: When passing multi-line text via `--fields "System.Description=..."` in the AZ CLI, shell whitespace handling strips newlines, collapsing everything into one line.

2. **`--description` flag**: AZ CLI's `--description` flag also strips newlines when passed as a shell argument (even with `UseShellExecute=false`).

3. **No Markdown/HTML rendering**: `System.Description` is plain text — Azure DevOps does not render Markdown or HTML there. The `Microsoft.VSTS.Common.DescriptionHtml` field **does not exist** in this project's Azure DevOps org (confirmed by TF51535 error).

### Solution: REST API with Plain Text

- Use Azure DevOps REST API (`az rest`) to create work items
- The REST API JSON body preserves newlines natively (no shell interpretation)
- Store plain text (converted from Markdown) in `System.Description`
- Use `MarkdownToPlainText()` to convert Markdown → readable plain text:
  - `## Heading` → `**Heading**` (bold markers)
  - `- item` → `• item` (bullet)
  - Preserves paragraph breaks with newlines

---

## Previous Attempts (History)

| Attempt | Approach | Result |
|---------|----------|--------|
| v2.1.5 | Replace `\n` with space in `System.Description` via `--fields` | Everything collapsed to one line, headings lost |
| v2.1.6 | Documented `--task-titles` flag | Docs only, didn't fix description issue |
| v2.1.7 | Use `--description` flag with `MarkdownToPlainText` | Still no newlines — shell strips them |
| v2.1.8 (current) | REST API + `Microsoft.VSTS.Common.DescriptionHtml` | TF51535: field doesn't exist in this project |

---

## Current Code State

### `AzureDevOpsHelpers.cs` — Task creation flow (lines 407–421)

```csharp
// Use REST API when description is provided (supports HTML)
if (!string.IsNullOrEmpty(description))
{
    var htmlDescription = MarkdownToHtml(description);
    (createJson, createError) = await CreateWorkItemViaRestApi(
        orgUrl, project, "Task", title, areaPath, iterationPath,
        fields, htmlDescription, userEmail);
}
else
{
    (createJson, createError) = await RunAzCaptureWithError(
        $"boards work-item create ...");
}
```

### `CreateWorkItemViaRestApi` method (lines ~1910–1960)

Uses `az rest --method post` to call Azure DevOps REST API. Currently uses `/fields/Microsoft.VSTS.Common.DescriptionHtml` which **does not exist** in the target org.

### Error handling (lines 423–473)

```csharp
if (createJson != null) { success = true; }
else if (createError != null && (createError.Contains("TF401320") || createError.Contains("TF51535")))
{
    // Handles missing mandatory fields and unknown field names
    // TF51535 prompts user to re-enter fields interactively
}
else
{
    // Generic "Unexpected error" — shows error and breaks
    // AADSTS50173 falls through here with NO helpful message
}
```

### Dead code (lines ~1862–1980)

`MarkdownToHtml()`, `EscapeHtmlForMarkdown()`, `ProcessInlineMarkdown()` — no longer used after the fix below.

---

## Remaining Bugs

### Bug 1: `Microsoft.VSTS.Common.DescriptionHtml` doesn't exist

**Error:** `TF51535: Cannot find field Microsoft.VSTS.Common.DescriptionHtml`

**Fix:** 
- Change field path to `/fields/System.Description`
- Pass `MarkdownToPlainText(description)` instead of `MarkdownToHtml(description)`
- Remove dead `MarkdownToHtml` methods

### Bug 2: AADSTS50173 not handled helpfully

**Error:** Token expired (user changed password). Falls through to generic "Unexpected error" with no guidance.

**Fix:**
- Add detection for `"AADSTS50173"` or `"grant has expired"` in error output
- Display: `❌ Authentication expired. Run: az logout && az login`
- `break` out of retry loop (retrying won't help)

---

## Implementation Plan

### Step 1: Fix field name in `CreateWorkItemViaRestApi` (~line 1930)

**File:** `AzureDevOps/AzureDevOpsHelpers.cs`

**Before:**
```csharp
if (!string.IsNullOrEmpty(htmlDescription))
{
    operations.Add($"{{\"op\": \"add\", \"path\": \"/fields/Microsoft.VSTS.Common.DescriptionHtml\", \"value\": \"{EscapeJson(htmlDescription)}\"}}");
}
```

**After:**
```csharp
if (!string.IsNullOrEmpty(htmlDescription))
{
    operations.Add($"{{\"op\": \"add\", \"path\": \"/fields/System.Description\", \"value\": \"{EscapeJson(htmlDescription)}\"}}");
}
```

Also rename `htmlDescription` parameter to `descriptionText` for clarity.

### Step 2: Fix call site (lines 410–415)

**Before:**
```csharp
var htmlDescription = MarkdownToHtml(description);
(createJson, createError) = await CreateWorkItemViaRestApi(
    orgUrl, project, "Task", title, areaPath, iterationPath,
    fields, htmlDescription, userEmail);
```

**After:**
```csharp
var plainDescription = MarkdownToPlainText(description);
(createJson, createError) = await CreateWorkItemViaRestApi(
    orgUrl, project, "Task", title, areaPath, iterationPath,
    fields, plainDescription, userEmail);
```

### Step 3: Add AADSTS50173 error handling (~line 468)

**Before:**
```csharp
else
{
    AnsiConsole.MarkupLine($"[red]❌ Unexpected error creating task:[/] [dim]{Markup.Escape(createError ?? "Unknown error")}[/]");
    break;
}
```

**After (insert between TF51535 block and the generic else):**
```csharp
else if (createError != null && (createError.Contains("AADSTS50173") || createError.Contains("grant has expired")))
{
    AnsiConsole.MarkupLine("[red]❌ Authentication expired.[/]");
    AnsiConsole.MarkupLine($"[dim]{Markup.Escape(createError.Trim())}[/]");
    AnsiConsole.MarkupLine("\n[yellow]Run the following to re-authenticate:[/]");
    AnsiConsole.MarkupLine("  az logout");
    AnsiConsole.MarkupLine("  az login");
    break;
}
```

### Step 4: Remove dead code (~lines 1862–1980)

Remove `MarkdownToHtml()`, `EscapeHtmlForMarkdown()`, `ProcessInlineMarkdown()` methods — they are no longer called.

### Step 5: Update CHANGELOG.md

Update v2.1.8 entry to reflect actual fix.

---

## Verification

```bash
# Build
dotnet build

# Test (after re-authenticating if needed)
dotnet run -- azure-devops hu task SubdepartamentoSolucionesTI "Cobro Pago y Tarifas" 19362 \
  --description "$(cat /Users/elvisbrevi/Code/sag/tmp-task-desc.md)" --effort 8 --no-link
```

Expected: tasks created with readable plain-text descriptions (proper line breaks, `**headers**`, `• bullet points`).

---

## Files to Modify

| File | Lines | Change |
|------|-------|--------|
| `AzureDevOps/AzureDevOpsHelpers.cs` | ~1930 | `DescriptionHtml` → `System.Description` |
| `AzureDevOps/AzureDevOpsHelpers.cs` | 410–415 | `MarkdownToHtml` → `MarkdownToPlainText` |
| `AzureDevOps/AzureDevOpsHelpers.cs` | ~468 | Add AADSTS50173 error handler |
| `AzureDevOps/AzureDevOpsHelpers.cs` | ~1862–1980 | Remove dead code |
| `CHANGELOG.md` | ~10–18 | Update v2.1.8 entry |
