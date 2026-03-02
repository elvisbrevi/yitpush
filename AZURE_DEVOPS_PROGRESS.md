# Azure DevOps Integration

## Commands

| Command | Description |
|---------|-------------|
| `hu task` | Create tasks for a User Story (interactive) |
| `hu task <org> <proj> <hu-id>` | Create tasks (quick mode) |
| `hu list` | List tasks of a User Story (interactive) |
| `hu list <org> <proj> <hu-id>` | List tasks (quick mode) |
| `link` | Add link (branch/commit/PR) to any work item |
| `link <org> <proj> <wi-id>` | Add link (quick mode) |
| `repo new` | Create a new repository |
| `repo checkout` | Clone a repository |
| `variable-group list` | Browse variable groups |

## Task Creation Details

### Decoupled Task Creation and Linking
The process is split into two steps:
1. Create the task via `az boards work-item create` and capture the ID.
2. Link to parent User Story via `az boards work-item relation add`.

### Mandatory Field Handling
- Tasks inherit `AreaPath` and `IterationPath` from parent User Story.
- Auto-defaults: `RemainingWork=0`, `EsfuerzoEstimadoHH=1`, `Mes=current month (Spanish)`.
- Retry loop with regex-based error parsing for missing fields (`TF401320`, `TF51535`).
- Interactive recovery: prompts for missing field values and retries automatically.

### Link Types
- **Branch**: Links to a git branch in Azure Repos
- **Commit**: Links to a specific commit hash
- **Pull Request**: Links to a PR by ID

Links work for both User Stories and Tasks (any work item ID).

---
*Last updated: March 2, 2026*
