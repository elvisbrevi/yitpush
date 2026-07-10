# Known Bugs — YitPush

Catálogo de bugs conocidos en `yp` (YitPush). Mantener actualizado a medida que se descubren o arreglan.

**Leyenda de severidad:**
- 🟥 **HIGH** — bloquea funcionalidad o corrompe datos
- 🟧 **MEDIUM** — degrada UX o requiere workaround manual
- 🟨 **LOW** — cosmético / warning ruidoso sin impacto funcional

---

## 🟥 BUG-001: `--effort-real` flag apunta a un field que no existe en proyectos localizados

**Descubierto:** 2026-06-22, durante el cierre de HU 21699
**Versión afectada:** v2.2.1, v2.2.2
**Severidad:** HIGH (el PATCH entero falla con `TF401320 Rule Error`)

### Síntoma

Al ejecutar `yp azure-devops task update <org> <id> --effort-real "5"`, Azure DevOps rechaza el PATCH con:

```
TF401320: Rule Error for field Esfuerzo Real. Error code: Required, InvalidEmpty.
```

El campo `Custom.EsfuerzoRealHH` (que es lo que yp envía) **no existe** en el proyecto "Soluciones Transversales". El campo real es `Custom.EsfuerzoReal`. La regla required del workflow exige que `Custom.EsfuerzoReal` esté no vacío, y como yp no lo setea (envía a uno que no existe), el PATCH entero falla con rollback.

### Causa raíz

Constante hardcodeada en `Program.cs:23`:

```csharp
internal const string AzFieldEffortRealHH = "Custom.EsfuerzoRealHH";  // ❌ no existe en este proyecto
```

El nombre correcto en el proyecto "Soluciones Transversales" es `Custom.EsfuerzoReal` (sin sufijo `HH`).

### Verificación

```bash
TOKEN=$(az account get-access-token --query accessToken -o tsv)
curl -s "https://dev.azure.com/SubdepartamentoSolucionesTI/Soluciones%20Transversales/_apis/wit/workitemtypes/Task/fields?api-version=7.0" \
  -H "Authorization: Bearer $TOKEN" | python3 -c "
import json, sys
d = json.load(sys.stdin)
for f in d.get('value', []):
    if 'esfuerzo' in f.get('name', '').lower():
        print(f'{f[\"name\"]:<40} => {f[\"referenceName\"]}')"
```

Output:
```
Esfuerzo Estimado HH  =>  Custom.EsfuerzoEstimadoHH
Esfuerzo Real         =>  Custom.EsfuerzoReal   ← este es el que yp debería usar
```

### Workaround (v2.2.2)

```bash
# En vez de:
yp azure-devops task update <org> <id> --effort-real "5"

# Usar:
yp azure-devops task update <org> <id> --field "Custom.EsfuerzoReal=5"
```

### Fix plan (v2.2.3)

1. Cambiar la constante en `Program.cs`:
   ```csharp
   internal const string AzFieldEffortRealHH = "Custom.EsfuerzoReal";
   ```
2. Mismo problema potencial para `AzFieldRemainingWork` y `AzFieldEffortHH` — verificar contra el work item type real del proyecto antes de release.
3. Considerar lookup dinámico de fields por nombre (como ya hicimos con `AzDevOpsFieldRefNameResolver` para `Evidencias de finalización`).

---

## 🟨 BUG-002: `ValidAzureStates` hardcodeado a nombres en inglés

**Descubierto:** 2026-06-22, durante el cierre de HU 21699
**Versión afectada:** v2.2.2 y todas las anteriores
**Severidad:** LOW (cosmético; el PATCH funciona igual)

### Síntoma

Al pasar `--state "Done"` en un proyecto con workflow en español, yp imprime:

```
⚠️  Warning: 'Done' may not be a valid state.
Common states: To Do, Doing, Active, In Progress, Resolved, Done, Closed, Removed
```

El PATCH igual funciona, pero el warning es ruido cosmético confuso. Si el usuario pasa un nombre en español (`"En revisión"`), el warning se dispara falsamente.

### Causa raíz

Lista hardcodeada en `AzureDevOpsHelpers.cs:1727`:

```csharp
private static readonly string[] ValidAzureStates = { "To Do", "Doing", "Active", "In Progress", "Resolved", "Done", "Closed", "Removed" };
```

El workflow real de "Soluciones Transversales" tiene:
```
En espera [Proposed]
En progreso [InProgress]
En revisión [Resolved]
Done [Completed]
Removido [Removed]
```

Solo `Done` y `Removido` matchean.

### Workaround

Ignorar el warning — el PATCH funciona.

### Fix plan (v2.2.3)

1. Cargar la lista dinámicamente via `GET /wit/workitemtypes/{type}/states?api-version=7.0`
2. Cachear por work item type
3. Si la red falla, fallar open (no warning) en vez de la lista hardcodeada

---

## 🟧 BUG-003: `Evidencias de finalización` (Custom.b505c83e-...) es required para transicionar a Done

**Descubierto:** 2026-06-22, durante el cierre de HU 21699
**Versión afectada:** workflow de "Soluciones Transversales", no es bug de yp
**Severidad:** MEDIUM (bloquea cierre de task si el campo está vacío)

### Síntoma

Al intentar mover una task a `Done` con `Custom.b505c83e-3745-4d8b-b76b-b3086a0c4c71` (Evidencias de finalización) vacío:

```
TF401320: Rule Error for field Evidencias de finalización. Error code: Required, InvalidEmpty.
```

### Causa raíz

Regla del workflow de Task en el proyecto "Soluciones Transversales" (no es bug de yp). El campo `Evidencias de finalización` es requerido en la transición a `Done`.

### Workaround (v2.2.2)

```bash
# Si la evidence está vacía, poblarla antes de cambiar a Done:
yp azure-devops task update <org> <id> \
  --evidence "Curl en desarrollo: GET /api/v1/comunas retorna JSON con { data: [...] }" \
  --state "Done"
```

`--evidence` resuelve el refname del field y lo popula antes de aplicar el state (es una sola operación atómica).

### Fix plan

No es bug de yp. Documentar el rule del workflow y considerar agregar un check en yp que advierta al usuario si va a transicionar a `Done` con `Evidencias` vacío.

---

## Histórico: BUG-000 (RESUELTO en v2.2.1)

**Severidad:** HIGH
**Título:** `--effort`, `--effort-real`, `--task-titles` flags ignorados silenciosamente
**Archivo:** `BUGFIX-effort-flags.md` (eliminado en v2.2.1)
**Causa:** Bloque de parser duplicado en `AzureDevOpsCommand.cs:55-89` que omitía los 3 flags.
**Fix:** Commit `dd00b3c` en v2.2.1.

---

## Bugs a investigar antes de v2.2.3

- [x] BUG-001: routear `--effort-real` / `--effort` a través de `AzDevOpsFieldRefNameResolver` (HIGH) → **Fixed en v2.3.0 (issue #18)**
- [x] BUG-002: lookup dinámico de states (LOW) → **Fixed en v2.3.0 (issue #18)**
- [x] BUG-003: pre-flight warning con display name + refname + hint de `--evidence` (MEDIUM) → **Helper entregado en v2.3.0 (issue #18); check completo llega en #17**

---

## ✅ Resuelto en v2.3.0 (issue #18)

- [x] **BUG-001 — `--effort-real` routea a través de `AzDevOpsFieldRefNameResolver`.** `TaskUpdateOperationsBuilder` ahora acepta `effortRefName` / `effortRealRefName` opcionales y los usa cuando se los pasa. `UpdateWorkItem` los resuelve en runtime desde el field cache (`~/.yitpush/field-cache.json`, TTL 24h) usando los display names "Esfuerzo Estimado HH" y "Esfuerzo Real". El PATCH ahora apunta a `Custom.EsfuerzoReal` en "Soluciones Transversales" y a `Custom.EsfuerzoRealHH` en "Cobro Pago y Tarifas" según corresponda. Las constantes históricas `Program.AzFieldEffortHH` / `Program.AzFieldEffortRealHH` siguen existiendo (con `[Obsolete]`) como fallback de último recurso. PR asociado al cierre de la issue #18.

- [x] **BUG-002 — Validación de state dinámica.** Nueva clase `AzDevOpsStateCache` (`AzureDevOps/AzDevOpsStateCache.cs`) que consulta `GET /wit/workitemtypes/{type}/states?api-version=7.0`, cachea en memoria por (org, project, type), y expone `IsLikelyValidStateAsync` con semántica **fail open** (devuelve `true` si la API falla para nunca bloquear el PATCH). El warning de "may not be a valid state" ahora sólo se imprime si la cache confirma que el state no pertenece al workflow. `--state "En revisión"` ya no dispara el warning falso. La lista hardcodeada `ValidAzureStates` fue removida de `AzureDevOpsHelpers.cs`; el `SelectionPrompt` interactivo ahora carga los states reales al entrar a la pantalla (con fallback a una lista pequeña si la red falla).

- [x] **BUG-003 — Pre-flight warning message.** Nuevo helper `TaskUpdatePreFlight.BuildMissingEvidenceMessage(displayName, refName)` (en `AzureDevOps/TaskUpdatePreFlight.cs`) genera el mensaje de error con el display name localizado ("Evidencias de finalización") y el refname resuelto (`Custom.b505c83e-3745-4d8b-b76b-b3086a0c4c71`), y apunta al usuario a `--evidence`. El check completo (GET del work item, verificación de required fields, exit 2 sin enviar el PATCH) se entrega en la issue #17 (v1: Pre-flight check for Done transition).

## Resuelto en v2.3.0 (issue #5)

- [x] **Field resolver genérico con cache persistente** — `AzDevOpsFieldRefNameResolver` ahora persiste el mapa de fields en `~/.yitpush/field-cache.json` con TTL de 24h, expone `ResolveByDisplayNameAsync` + `RefreshAsync`, y se usa desde los nuevos subcomandos `yp azure-devops resolve-field` y `yp azure-devops refresh-fields`. El trabajo de "verificar `AzFieldRemainingWork` y `AzFieldEffortHH` contra work item type real" se hace ahora con:

  ```bash
  yp azure-devops resolve-field <org> <project> Task "Remaining Work"
  yp azure-devops resolve-field <org> <project> Task "Esfuerzo Real"
  ```

  El refname devuelto se puede pasar a `--field` mientras se consolidan los fixes de BUG-001.
