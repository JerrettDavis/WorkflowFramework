# Dashboard Finalization Plan

## Critical Bugs (Must Fix First)

### Bug 1: HandleSave discards canvas data
`WorkflowDesigner.razor` line ~282: `HandleSave` calls `getWorkflowDefinition` from JS but **ignores the result** — creates a new empty `WorkflowDefinitionDto { Name = _workflowName }` with no steps. Nothing saves.

**Fix**: Parse the JS-returned definition (nodes + edges + configs) into `WorkflowDefinitionDto` with `StepDefinitionApiDto` steps including `Config` dictionaries. This requires a `CanvasToDefinition()` converter that reconstructs the tree from flat nodes/edges.

### Bug 2: HandleValidate also ignores canvas
Same issue — validates an empty definition instead of the actual canvas content.

---

## Phase 1: Settings & Provider Configuration

### 1a. Settings API (`Dashboard.Api`)
New file: `Services/DashboardSettingsService.cs`
```
- In-memory settings store (Dictionary<string, string>)
- GET /api/settings → all settings
- PUT /api/settings → bulk update
- Sections: "providers", "general"
```

Settings model:
```csharp
public class DashboardSettings
{
    // AI Providers
    public string? OllamaUrl { get; set; }           // default: http://localhost:11434
    public string? OpenAiApiKey { get; set; }
    public string? AnthropicApiKey { get; set; }
    public string? HuggingFaceApiKey { get; set; }
    
    // Provider-specific
    public string? OpenAiBaseUrl { get; set; }        // for Azure OpenAI or proxies
    public string? DefaultProvider { get; set; }      // "ollama" | "openai" | "anthropic" | "huggingface"
    public string? DefaultModel { get; set; }
    
    // Execution
    public int DefaultTimeoutSeconds { get; set; } = 300;
    public int MaxConcurrentRuns { get; set; } = 5;
}
```

### 1b. Settings Page (`Dashboard.Web`)
New file: `Components/Pages/Settings.razor`
- Route: `/settings`
- Nav link in sidebar/header
- Sections with cards:
  - **AI Providers** — Ollama URL (text), OpenAI Key (password), Anthropic Key (password), HuggingFace Key (password), Default Provider (dropdown), Default Model (dropdown populated from provider)
  - **Execution** — Default timeout, max concurrent runs
- Save button, toast on success
- "Test Connection" button for Ollama (pings /api/tags)

### 1c. Model Discovery API
New endpoint: `GET /api/providers/{provider}/models`
- For Ollama: calls `{ollamaUrl}/api/tags` → returns model names
- For OpenAI: returns hardcoded list (gpt-4o, gpt-4o-mini, gpt-4-turbo, gpt-3.5-turbo, o1, o1-mini, o3-mini)
- For Anthropic: returns hardcoded list (claude-sonnet-4-20250514, claude-haiku-4-20250414, claude-opus-4-0520, claude-3-haiku)
- For HuggingFace: returns hardcoded popular inference models

---

## Phase 2: Rich Schema-Driven Properties Panel

### 2a. Enhanced ConfigSchema format
Current schema only has `type` and `enum`. Extend `StepTypeRegistry` schemas with:
```json
{
  "properties": {
    "provider": {
      "type": "string",
      "enum": ["ollama", "openai", "anthropic", "huggingface"],
      "uiType": "providerSelect",
      "label": "AI Provider",
      "required": true,
      "helpText": "Select the AI provider for this step"
    },
    "model": {
      "type": "string",
      "uiType": "modelSelect",
      "label": "Model",
      "required": true,
      "dependsOn": "provider",
      "helpText": "Model to use (populated from provider)"
    },
    "prompt": {
      "type": "string",
      "uiType": "textarea",
      "label": "Prompt",
      "rows": 6,
      "helpText": "The prompt template. Use {variableName} for context variables."
    },
    "temperature": {
      "type": "number",
      "uiType": "slider",
      "min": 0,
      "max": 2,
      "step": 0.1,
      "default": 0.7
    }
  }
}
```

### 2b. PropertiesPanel rich controls
Replace the current "everything is a text input" approach. Parse `uiType` from schema:

| uiType | Control |
|--------|---------|
| `providerSelect` | Dropdown: ollama/openai/anthropic/huggingface |
| `modelSelect` | Dropdown populated via API call, depends on provider selection |
| `textarea` | Multi-line textarea with configurable rows |
| `slider` | Range slider with min/max/step |
| `select` | Dropdown from enum values |
| `number` | Number input with min/max |
| `boolean` | Toggle switch |
| `password` | Password input (for inline API keys) |
| `json` | Code editor-style textarea with monospace font |
| `expression` | Text input with syntax hint |
| (default) | Text input |

Also show:
- Required indicator (red asterisk)
- Help text below field
- Default value as placeholder
- Validation state (red border if required + empty)

### 2c. Specific field improvements for each step type

**AI/Agent steps** (`AgentLoopStep`, `LlmCallStep`, `AgentDecisionStep`, `AgentPlanStep`):
- `provider` → providerSelect dropdown
- `model` → modelSelect (dynamic from API, depends on provider)
- `prompt`/`systemPrompt` → textarea (6 rows)
- `temperature` → slider 0-2
- `maxTokens` → number input
- `maxIterations` → number input with default
- `tools` → future: multi-select from registered tools; for now: textarea with comma-separated hint

**HTTP steps** (`HttpStep`, `WebhookTriggerStep`):
- `method` → select dropdown (already has enum)
- `url` → text with URL validation
- `headers` → json textarea
- `body` → json textarea
- `contentType` → select (application/json, text/plain, application/xml, multipart/form-data)

**Human Tasks** (`HumanTaskStep`, `ApprovalStep`):
- `priority` → select dropdown (already has enum)
- `assignee` → text
- `description`/`message` → textarea

**Data steps**:
- `sourceFormat`/`targetFormat` → select (already has enum)
- `mappings`/`schema` → json textarea

---

## Phase 3: Save/Load Round-Trip Fix

### 3a. Canvas → Definition converter
New method in `WorkflowDesigner.razor`: `CanvasToDefinition()`
- Calls `workflowEditor.getWorkflowDefinition()` which returns `{nodes: [...], edges: [...]}`
- Builds a tree from the flat graph:
  1. Find root nodes (no incoming edges)
  2. Walk edges to build sequential chain
  3. Detect container patterns (Conditional with then/else edges, Parallel with fan-out, etc.)
  4. Convert each node's `config` dict back to `StepDefinitionApiDto.Config`
- Returns `WorkflowDefinitionDto`

### 3b. Fix HandleSave
```csharp
private async Task HandleSave()
{
    var definition = await CanvasToDefinition();
    definition.Name = _workflowName;
    var request = new CreateWorkflowRequest { Definition = definition };
    // ... save via API
}
```

### 3c. Fix HandleValidate
Same — validate the actual canvas definition.

---

## Phase 4: Execution Engine (Make "Run" Work)

### 4a. Real workflow execution
`WorkflowRunService.StartRunAsync` currently stubs "Completed" immediately. Replace with:
- Build an `IWorkflow` from the `WorkflowDefinitionDto` using config
- Resolve AI providers from settings (Ollama URL, API keys)
- Execute with real `WorkflowContext`
- Stream step status updates via SignalR
- Track actual duration, errors, output

This is the biggest piece — the `WorkflowDefinitionDto` → `IWorkflow` bridge. Need a `WorkflowDefinitionCompiler` that:
1. Reads each `StepDefinitionDto` 
2. Based on `Type`, creates the actual step instance
3. Wires up config (provider, model, prompt, etc.)
4. Handles structural types (Conditional, Parallel, etc.) recursively

### 4b. Provider resolution
When executing, the compiler needs to create real `IAgentProvider` instances:
- `"ollama"` → `OllamaAgentProvider(settings.OllamaUrl)`
- `"openai"` → `OpenAiAgentProvider(settings.OpenAiApiKey)`
- etc.

### 4c. Step status streaming
Already have `WorkflowExecutionHub` / `WorkflowExecutionNotifier` — just need to wire them to real execution events.

---

## Execution Order

Given token constraints, prioritize:

1. **Phase 3** (save round-trip fix) — without this, nothing works at all
2. **Phase 2a+2b** (rich properties) — makes the UI usable  
3. **Phase 1** (settings) — needed before execution
4. **Phase 4** (execution) — the ultimate goal

Each phase is a clean sub-agent task.

---

## Files to Modify

### New files:
- `src/WorkflowFramework.Dashboard.Api/Services/DashboardSettingsService.cs`
- `src/WorkflowFramework.Dashboard.Api/Services/WorkflowDefinitionCompiler.cs`
- `src/WorkflowFramework.Dashboard.Web/Components/Pages/Settings.razor`

### Modified files:
- `src/WorkflowFramework.Dashboard.Api/Services/StepTypeRegistry.cs` — enhanced schemas
- `src/WorkflowFramework.Dashboard.Api/Services/WorkflowRunService.cs` — real execution
- `src/WorkflowFramework.Dashboard.Api/DashboardApiExtensions.cs` — settings + models endpoints
- `src/WorkflowFramework.Dashboard.Web/Components/Designer/PropertiesPanel.razor` — rich controls
- `src/WorkflowFramework.Dashboard.Web/Components/Designer/WorkflowDesigner.razor` — save fix + canvas→definition
- `src/WorkflowFramework.Dashboard.Web/Models/ApiModels.cs` — settings models
- `src/WorkflowFramework.Dashboard.Web/Services/DashboardApiClient.cs` — settings + models API calls
- `src/WorkflowFramework.Dashboard.Web/wwwroot/js/workflow-editor.js` — ensure getWorkflowDefinition returns config

### Test updates:
- `tests/WorkflowFramework.Dashboard.Tests/` — settings page tests
- `tests/WorkflowFramework.Dashboard.Api.Tests/` — settings API, compiler, execution tests
