# Model Abstraction Quickstart Guide

## Overview

This guide helps you work with the Model Abstraction feature, which enables zero-recompilation model switching through configuration-driven architecture.

## Adding New Models

### Local Models (Ollama)

1. **Install Ollama** (Linux/macOS):
   ```bash
   curl -fsSL https://ollama.com/install.sh | sh
   ```

2. **Download a model**:
   ```bash
   ollama pull qwen2.5-vl:3b-instruct
   ```

3. **Add to configuration** (`appsettings.json`):
   ```json
   {
     "AI": {
       "DefaultModel": "qwen-vl-3b",
       "Models": [
         {
           "Name": "qwen-vl-3b",
           "Provider": "Ollama",
           "Endpoint": "http://localhost:11434",
           "ModelId": "qwen2.5-vl:3b-instruct",
           "ToolInvocationStrategy": "Functools"
         }
       ]
     }
   }
   ```

### Local Models (Azure Foundry Local)

1. **Install Foundry Local** (Windows/macOS):
   ```powershell
   winget install Microsoft.FoundryLocal
   ```

2. **Download Phi-4 Mini**:
   ```bash
   foundry model download phi-4-mini
   foundry service start
   ```

3. **Add to configuration**:
   ```json
   {
     "AI": {
       "Models": [
         {
           "Name": "phi-4-mini",
           "Provider": "FoundryLocal",
           "Endpoint": "http://localhost:5272",
           "ModelId": "Phi-4-mini-instruct-generic-cpu",
           "ToolInvocationStrategy": null
         }
       ]
     }
   }
   ```

### Cloud Models (Azure OpenAI)

1. **Set up Azure OpenAI resource** in Azure Portal

2. **Get endpoint and key** from Azure Portal

3. **Add to configuration**:
   ```json
   {
     "AI": {
       "Models": [
         {
           "Name": "gpt-4o",
           "Provider": "AzureOpenAI",
           "Endpoint": "https://<resource>.openai.azure.com/",
           "ModelId": "gpt-4o",
           "ApiKey": "<your-key>",
           "ToolInvocationStrategy": null
         }
       ]
     }
   }
   ```

   **Security Note**: Use User Secrets for API keys:
   ```bash
   dotnet user-secrets set "AI:Models:0:ApiKey" "your-key-here"
   ```

## Adding New Tool Handlers

Tool handlers enable model-specific tool invocation parsing (e.g., Functools format for Qwen models).

### 1. Implement IToolInvocationHandler

Create a new handler in `src/LocalAIAgent.Agent/Handlers/`:

```csharp
using Microsoft.Extensions.AI;
using LocalAIAgent.Agent.Interfaces;

namespace LocalAIAgent.Agent.Handlers;

/// <summary>
/// Handler for models using ReAct JSON format for tool invocation.
/// </summary>
public class ReActJSONHandler : IToolInvocationHandler
{
    public IChatClient CreateHandler(IChatClient innerClient)
    {
        // Wrap innerClient with custom parsing logic
        return new ReActJSONClient(innerClient);
    }
}

public class ReActJSONClient : IChatClient
{
    private readonly IChatClient _innerClient;

    public ReActJSONClient(IChatClient innerClient)
    {
        _innerClient = innerClient;
    }

    // Implement IChatClient methods with custom parsing
    // See FunctoolsHandler.cs for reference implementation
}
```

### 2. Register Handler as Keyed Service

In `src/LocalAIAgent.Agent/Program.cs`:

```csharp
// Register handler with keyed service pattern
builder.Services.AddKeyedSingleton<IToolInvocationHandler, ReActJSONHandler>("ReActJSON");
```

### 3. Use Handler in Configuration

```json
{
  "AI": {
    "Models": [
      {
        "Name": "my-react-model",
        "Provider": "Ollama",
        "Endpoint": "http://localhost:11434",
        "ModelId": "deepseek-r1:latest",
        "ToolInvocationStrategy": "ReActJSON"
      }
    ]
  }
}
```

## Testing Configuration

### 1. Validate Configuration File

```bash
dotnet run --project src/LocalAIAgent.Agent -- --validate-config
```

### 2. Test Model Connectivity

```bash
# Test Ollama
curl http://localhost:11434/api/tags

# Test Foundry Local
foundry service status

# Test Azure OpenAI
curl -X GET "https://<resource>.openai.azure.com/openai/deployments?api-version=2023-05-15" \
  -H "api-key: <your-key>"
```

### 3. Run Integration Tests

```bash
dotnet test tests/LocalAIAgent.Agent.Tests --filter "Category=Integration"
```

## Accessibility Testing (T071)

### Automated Testing with axe DevTools

1. **Install axe DevTools Extension**:
   - Chrome: https://chrome.google.com/webstore/detail/axe-devtools-web-accessibility/lhdoppojpmngadmnindnejefpokejbdd
   - Firefox: https://addons.mozilla.org/en-US/firefox/addon/axe-devtools/

2. **Run Analysis**:
   - Open chat UI in browser: http://localhost:5000
   - Open browser DevTools (F12)
   - Navigate to "axe DevTools" tab
   - Click "Scan All of My Page"

3. **Expected Results**:
   - **Target**: Zero accessibility violations
   - **Model dropdown must pass**:
     - ✅ ARIA labels present (`aria-label="Select AI model"`)
     - ✅ Keyboard navigation (Tab, Enter, Arrow keys)
     - ✅ Focus indicators visible (3:1 contrast minimum)
     - ✅ Disabled state has multiple cues (color + icon)
     - ✅ Error states use `role="alert"`

4. **Document Results**:
   - Save axe report (Export as HTML/JSON)
   - File issues for any violations found
   - Update this section with test date and result summary

**Last Test**: [Not yet performed]
**Result**: [Pending]
**Violations**: [N/A]

### Manual Testing Checklist

- [ ] Keyboard navigation works (Tab to dropdown, Arrow keys to select, Enter to confirm)
- [ ] Screen reader announces dropdown label and current selection
- [ ] Focus indicator is visible with 3:1 contrast
- [ ] Disabled state is perceivable without color alone
- [ ] Error messages are announced by screen readers

## Troubleshooting

### Model Not Found Error

**Symptom**: `Model 'xyz' not found in configuration`

**Solution**:
1. Check `DefaultModel` in appsettings.json matches a model Name
2. Verify models array contains the model
3. Restart application after config changes

### Tool Discovery Failed

**Symptom**: `No tools registered` in logs

**Solution**:
1. Check ToolDiscoveryService logs for assembly loading errors
2. Verify [Tool] attributes are present on methods
3. Ensure OpenMeteo assembly is referenced by Agent project

### Handler Not Applied

**Symptom**: Tools not invoked despite `ToolInvocationStrategy` set

**Solution**:
1. Verify handler is registered as keyed service
2. Check strategy name matches keyed service key (case-sensitive)
3. Review ChatClientFactory logs for handler resolution

### Connection Refused

**Symptom**: `Connection refused to http://localhost:11434`

**Solution**:
```bash
# For Ollama
ollama serve

# For Foundry Local
foundry service start

# Check status
foundry service status
curl http://localhost:11434/api/tags
```

## Architecture Reference

### Component Flow

```
User Request
    ↓
ChatAgentService (with ChatClientAgent)
    ↓
ChatClientFactory.CreateChatClient(modelKey)
    ↓
1. Load ModelConfiguration from config
2. Create base IChatClient (Ollama/Foundry/Azure)
3. If ToolInvocationStrategy != null:
   a. Resolve IToolInvocationHandler via GetKeyedService
   b. Wrap IChatClient with handler
4. Return wrapped/unwrapped IChatClient
    ↓
Agent Framework (Microsoft.Agents.AI)
    ↓
Tool Execution (discovered via ToolDiscoveryService)
```

## Accessibility Validation

### Manual Testing with axe DevTools (T071)

The model dropdown and chat interface MUST meet WCAG 2.1 AA accessibility standards (SC-013). Automated testing has been validated through test suite, but manual validation is required for visual contrast and screen reader compatibility.

**Prerequisites**:
- Chrome/Edge browser
- [axe DevTools Extension](https://chrome.google.com/webstore/detail/axe-devtools-web-accessib/lhdoppojpmngadmnindnejefpokejbdd)

**Validation Steps**:

1. **Start the application**:
   ```powershell
   .\Start-AspireHost.ps1
   ```

2. **Open browser dev tools** (F12) and navigate to **axe DevTools** tab

3. **Run full page scan** focusing on:
   - Model dropdown (`#model-selector`)
   - Dropdown label (`#model-selector-label`)
   - Help text (`#model-selector-help`)

4. **Verify zero violations** in these categories:
   - ✅ **ARIA labels**: Dropdown has `aria-label="Select AI model"`
   - ✅ **ARIA described-by**: Dropdown references `#model-selector-help`
   - ✅ **Label association**: Label `for` attribute matches dropdown `id`
   - ✅ **Color contrast**: Text meets 4.5:1 ratio, UI components meet 3:1 ratio
   - ✅ **Keyboard navigation**: Tab, Enter, Arrow keys, Escape all functional
   - ✅ **Focus indicators**: Visible focus outline on all interactive elements
   - ✅ **Screen reader**: Option text is descriptive (not just keys like "phi4")

5. **Test disabled state**:
   - Send first message → dropdown should disable
   - Verify help text changes to "Model locked for this conversation"
   - Verify `disabled` attribute present for assistive tech

6. **Test new chat reset**:
   - Click "New Chat" button
   - Verify dropdown re-enables
   - Verify help text resets to "Select model before sending first message"

**Expected Result**: Zero axe DevTools violations in all categories

**Documentation**: Screenshot any violations and file as issues before release

### Configuration Schema

```json
{
  "AI": {
    "DefaultModel": "string (required, must match a Models[].Name)",
    "Models": [
      {
        "Name": "string (required, unique)",
        "Provider": "Ollama|FoundryLocal|FoundryCloud|AzureOpenAI|OpenAI|Gemini",
        "Endpoint": "string (required, HTTP(S) URL)",
        "ModelId": "string (required, provider-specific model name)",
        "ApiKey": "string (optional, for cloud providers)",
        "ToolInvocationStrategy": "string|null (optional, keyed service key)"
      }
    ]
  }
}
```

## Next Steps

- Review `plan.md` for full architectural context
- See `research.md` for technical decisions and constraints
- Check `tasks.md` for implementation status
- Explore `contracts/` for API specifications and test requirements
