# Azure DevOps Task Management Enhancements

This document summarizes the improvements made to the `azure-devops hu list` command to support the creation and linking of tasks within the specific constraints of the project's Azure DevOps configuration.

## 🛠 Fixes and Improvements

### 1. Decoupled Task Creation and Linking
*   **Issue:** The `az boards work-item create` command did not support the `--parent` argument in the local environment, causing an "unrecognized arguments" error.
*   **Solution:** The process was split into two atomic steps:
    1.  Create the task using `az boards work-item create` and capture the resulting ID from the JSON output.
    2.  Link the new task to its parent User Story using `az boards work-item relation add`.

### 2. Mandatory Field Handling
*   **Issue:** The project uses a process template with several mandatory fields (`Remaining Work`, `EsfuerzoEstimadoHH`, `Mes`), causing task creation to fail with error `TF401320`.
*   **Solution:** 
    *   **Context Inheritance:** New tasks now automatically inherit the `AreaPath` and `IterationPath` from their parent User Story.
    *   **Safe Defaults:** Added automatic defaults for known mandatory fields:
        *   `Microsoft.VSTS.Scheduling.RemainingWork`: Defaults to `0`.
        *   `Custom.EsfuerzoEstimadoHH`: Defaults to `1`.
        *   `Custom.Mes`: Automatically detected and set to the current month in Spanish (e.g., "Febrero").

### 3. Robust Retry Loop & Error Detection
*   **Regex-Powered Diagnosis:** Implemented a retry loop that uses Regular Expressions to parse Azure DevOps error messages.
*   **Interactive Recovery:** If a mandatory field is missing, the tool now identifies the field name and prompts the user for its value in real-time.
*   **Field Mapping:** The tool intelligently maps common field names to their internal Azure DevOps identifiers (e.g., prefixing with `Custom.` or `Microsoft.VSTS.Common.`).
*   **TF51535 Handling:** Added specific handling for "Field not found" errors, allowing users to correct field-value pairs without restarting the process.

## 🚀 Current Workflow
1.  Run `yitpush azure-devops hu list`.
2.  Select a User Story.
3.  Choose "Create standard tasks".
4.  Enter task titles.
5.  **The tool autonomously handles creation, defaults, and linking.**
6.  If the project requires a unique mandatory field, the tool will ask for it once and retry automatically.

---
*Date: February 26, 2026*
