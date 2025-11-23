# Remediation Implementation Guide
**Feature**: 003-model-abstraction
**Analysis Date**: 2025-11-23
**Status**: 13 findings identified, 6 documentation fixes applied

---

## ✅ Completed Remediations (Findings A01, A05, A07, A09, A10, A12, A13)

- [X] **A01 (CRITICAL)**: Updated spec.md, plan.md to use `LocalAIAgent.*` consistently
- [X] **A05 (HIGH)**: Clarified FR-009 uses `LocalAIAgent.*` namespace pattern
- [X] **A07 (MEDIUM)**: Added explicit test migration requirement to spec In Scope
- [X] **A09 (MEDIUM)**: Updated User Story 4 acceptance scenarios to past tense
- [X] **A10 (MEDIUM)**: Standardized terminology to "AI agent capabilities"
- [X] **A12 (MEDIUM)**: Aligned FR-001 version requirement with constitution (1.0.0-preview.251001.1+)
- [X] **A13 (LOW)**: Simplified T036a OPTIONAL note

---

## 🔴 CRITICAL - Required for Feature Completion

### A02: Complete Agent Framework Migration Tests (T052-T054)

**Objective**: Validate that all existing tests work with ChatClientAgent and AgentThread patterns.

**Tasks**:

#### T052: Update Existing Tests to Use ChatClientAgent
**Location**: `tests/LocalAIAgent.Agent.Tests/`
**Action**: Search for `IChatClient` references, replace with `ChatClientAgent` pattern

```powershell
# Find tests using IChatClient
Get-ChildItem -Path tests/LocalAIAgent.Agent.Tests -Recurse -Filter *.cs |
  Select-String -Pattern "IChatClient" |
  Select-Object -Property Path -Unique
```

**Example Pattern**:
```csharp
// OLD (IChatClient)
var mockClient = new Mock<IChatClient>();
mockClient.Setup(x => x.GetResponseAsync(...)).ReturnsAsync(...);

// NEW (ChatClientAgent)
var mockChatClient = new Mock<IChatClient>();
var agent = new ChatClientAgent(mockChatClient.Object, ...);
var response = await agent.RunAsync(...);
```

**Validation**: All 172+ tests pass after migration

#### T053: Update Tests to Use AgentThread
**Location**: `tests/LocalAIAgent.Agent.Tests/`
**Action**: Replace manual conversation history management with AgentThread

```csharp
// OLD (Manual History)
var history = new List<ChatMessage>();
history.Add(new ChatMessage(ChatRole.User, "test"));
var response = await chatClient.GetResponseAsync(history);

// NEW (AgentThread)
var thread = agent.GetNewThread();
var response = await agent.RunAsync(
    new[] { new ChatMessage(ChatRole.User, "test") },
    thread
);
```

**Validation**: Conversation history maintained correctly across all tests

#### T054: Create Migration Equivalence Test
**Location**: `tests/LocalAIAgent.Agent.Tests/Integration/AgentMigrationTests.cs`
**Purpose**: Prove behavioral equivalence between old IChatClient and new ChatClientAgent patterns

```csharp
[Fact]
public async Task ChatClientAgent_ShouldProduceSameResults_AsLegacyIChatClient()
{
    // Arrange: Same input, both patterns
    var userMessage = "What's the weather in Seattle?";

    // Act: Execute with both patterns
    var legacyResult = await ExecuteLegacyPattern(userMessage);
    var agentResult = await ExecuteAgentPattern(userMessage);

    // Assert: Results equivalent (tool calls, message structure)
    Assert.Equal(legacyResult.ToolCallCount, agentResult.ToolCallCount);
    Assert.Contains("Seattle", agentResult.FinalResponse);
}
```

**Validation**: Test passes, proving migration correctness

---

## 🟡 HIGH - Required for Success Criteria

### A03: Complete Accessibility Tests (T069-T071)

**Objective**: Validate WCAG 2.1 AA compliance for model dropdown (SC-013)

#### T069: bUnit Dropdown Behavior Tests
**Location**: `tests/LocalAIAgent.Web.Tests/Components/ChatDropdownTests.cs`

```csharp
[Fact]
public void ModelDropdown_ShouldPopulateFromConfiguration()
{
    // Arrange: Configure multiple models
    var config = new AIConfiguration {
        DefaultModel = "phi-4-mini",
        Models = new[] {
            new Model { Id = "phi-4-mini", Provider = "Foundry" },
            new Model { Id = "qwen2.5-vl-3b", Provider = "Ollama" }
        }
    };

    // Act: Render Chat component
    using var ctx = new TestContext();
    ctx.Services.AddSingleton(config);
    var cut = ctx.RenderComponent<Chat>();

    // Assert: Dropdown shows both models
    var dropdown = cut.Find("select[aria-label='Select AI model']");
    var options = dropdown.QuerySelectorAll("option");
    Assert.Equal(2, options.Length);
    Assert.Contains("Foundry: phi-4-mini (Local)", options[0].TextContent);
}

[Fact]
public void ModelDropdown_ShouldDisableAfterFirstMessage()
{
    // Arrange: Render chat, send message
    using var ctx = new TestContext();
    var cut = ctx.RenderComponent<Chat>();

    // Act: Send first message
    var input = cut.Find("textarea");
    input.Input("Hello");
    var sendButton = cut.Find("button[type='submit']");
    sendButton.Click();

    // Assert: Dropdown disabled
    var dropdown = cut.Find("select");
    Assert.True(dropdown.HasAttribute("disabled"));
}
```

#### T070: bUnit Accessibility Tests
**Location**: `tests/LocalAIAgent.Web.Tests/Components/ChatAccessibilityTests.cs`

```csharp
[Fact]
public void ModelDropdown_ShouldHaveAriaLabels()
{
    using var ctx = new TestContext();
    var cut = ctx.RenderComponent<Chat>();

    var dropdown = cut.Find("select");
    Assert.Equal("Select AI model", dropdown.GetAttribute("aria-label"));
    Assert.NotNull(dropdown.GetAttribute("aria-describedby"));
}

[Fact]
public void ModelDropdown_ShouldSupportKeyboardNavigation()
{
    using var ctx = new TestContext();
    var cut = ctx.RenderComponent<Chat>();

    var dropdown = cut.Find("select");

    // Tab to dropdown
    dropdown.KeyDown("Tab");
    Assert.Equal(dropdown, ctx.JSInterop.Invocations.Last().Arguments[0]);

    // Arrow keys navigate options
    dropdown.KeyDown("ArrowDown");
    // Assert: Selection changes
}
```

#### T071: Manual axe DevTools Validation
**Process**:
1. Run application: `.\Start-AspireHost.ps1`
2. Open browser to `http://localhost:5000/chat`
3. Install axe DevTools browser extension
4. Run accessibility scan
5. **Target**: Zero violations for model dropdown region
6. Document results in `specs/003-model-abstraction/quickstart.md`

---

### A04: Complete OpenMeteo Encapsulation Tests (T093, T093a)

**Objective**: Validate no `openmeteo_sdk` types leak into public API (SC-011)

#### T093: API Surface Inspection Test
**Location**: `tests/LocalAIAgent.Agent.Tests/Integration/OpenMeteoEncapsulationTests.cs`

**Approach 1: Roslyn-Based Analyzer** (Recommended)
```csharp
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

[Fact]
public void OpenMeteoAssembly_ShouldNotExposeSDKTypes()
{
    // Arrange: Load LocalAIAgent.OpenMeteo assembly
    var assemblyPath = Path.Combine(
        Directory.GetCurrentDirectory(),
        "LocalAIAgent.OpenMeteo.dll"
    );
    var assembly = Assembly.LoadFrom(assemblyPath);

    // Act: Get all public types
    var publicTypes = assembly.GetExportedTypes();

    // Assert: No openmeteo_sdk types exposed
    foreach (var type in publicTypes)
    {
        var methods = type.GetMethods(BindingFlags.Public | BindingFlags.Instance);
        foreach (var method in methods)
        {
            // Check return type
            Assert.DoesNotContain(
                "OpenMeteo",
                method.ReturnType.Namespace ?? "",
                StringComparison.OrdinalIgnoreCase
            );

            // Check parameters
            foreach (var param in method.GetParameters())
            {
                Assert.DoesNotContain(
                    "OpenMeteo",
                    param.ParameterType.Namespace ?? "",
                    StringComparison.OrdinalIgnoreCase
                );
            }
        }
    }
}
```

**Approach 2: Manual ILSpy Inspection** (Alternative)
1. Install ILSpy: `dotnet tool install -g ilspy`
2. Decompile: `ilspy src/LocalAIAgent.OpenMeteo/bin/Release/net10.0/LocalAIAgent.OpenMeteo.dll`
3. Review public API surface, verify no `OpenMeteo.*` SDK types
4. Document findings in test comments

#### T093a: Migrate Weather Tool Tests
**Action**: Move tests from `LocalAIAgent.Agent.Tests/Tools/` to `LocalAIAgent.OpenMeteo.Tests/`

```powershell
# Find weather tool tests
Get-ChildItem -Path tests/LocalAIAgent.Agent.Tests/Tools/ -Filter *Tools.cs |
  Where-Object { $_.Name -match "Geocoding|Weather|AirQuality" }

# Expected files to migrate:
# - GeocodingToolsTests.cs
# - WeatherToolsTests.cs
# - AirQualityToolsTests.cs
```

**Process**:
1. Copy test files to `tests/LocalAIAgent.OpenMeteo.Tests/Tools/`
2. Update namespace: `LocalAIAgent.Agent.Tests` → `LocalAIAgent.OpenMeteo.Tests`
3. Verify tests pass: `dotnet test tests/LocalAIAgent.OpenMeteo.Tests/`
4. Delete original files from `LocalAIAgent.Agent.Tests/Tools/`

---

## 🟠 MEDIUM - Constitution Compliance

### A08: Complete Cross-Platform Scripts (T096-T097, T101)

#### T096: Qwen Download for Linux/macOS
**Location**: `scripts/setup-environment.sh`

```bash
# Add after Ollama installation check
echo "Downloading Qwen 2.5 VL 3B model..."
ollama pull qwen2.5-vl:3b-instruct

if [ $? -eq 0 ]; then
    echo "✓ Qwen 2.5 VL 3B downloaded successfully"
else
    echo "✗ Failed to download Qwen model"
    exit 1
fi
```

#### T097: Phi-4 Mini Verification for Linux/macOS
**Location**: `scripts/setup-environment.sh`

```bash
# Foundry Local not available on Linux - document Ollama alternative
echo "Verifying Phi-4 Mini availability..."
if command -v ollama &> /dev/null; then
    ollama pull phi4-mini-instruct
    echo "✓ Phi-4 Mini configured via Ollama"
else
    echo "⚠ Ollama not found. Install: curl https://ollama.ai/install.sh | sh"
fi
```

#### T101: Model Validation for Linux/macOS
**Location**: `scripts/start-aspire-host.sh`

```bash
#!/bin/bash
# Validate default model availability

DEFAULT_MODEL=$(grep -Po '"DefaultModel":\s*"\K[^"]+' appsettings.json)

if [ -z "$DEFAULT_MODEL" ]; then
    echo "✗ DefaultModel not found in configuration"
    exit 1
fi

echo "Validating model: $DEFAULT_MODEL"
ollama list | grep -q "$DEFAULT_MODEL"

if [ $? -eq 0 ]; then
    echo "✓ Model available"
else
    echo "✗ Model $DEFAULT_MODEL not found. Run: ./scripts/setup-environment.sh"
    exit 1
fi
```

---

### A11: Complete Documentation Updates (T104, T106)

#### T104: README Model Selection UI Workflow
**Location**: `README.md`

Add section after "Architecture":

```markdown
## Model Selection

The Local AI Agent supports multiple AI models with per-conversation selection:

### Supported Models

- **Phi-4 Mini** (Default): Microsoft's 3.8B parameter model, optimized for CPU inference
- **Qwen 2.5 VL 3B**: Alibaba's vision-language model with functools support

### UI Workflow

1. **Navigate to Chat**: Open `http://localhost:5000/chat`
2. **Select Model**: Choose from dropdown before starting conversation
   - Format: `Provider: model-name (endpoint-type)`
   - Example: `Foundry: phi-4-mini (Local)`
3. **Send Message**: Model locked for session after first message
4. **New Session**: Clear chat to re-enable model selection

### Configuration

Edit `appsettings.json`:

```json
{
  "AI": {
    "DefaultModel": "phi-4-mini",
    "Models": [
      {
        "Id": "phi-4-mini",
        "Provider": "Foundry",
        "Endpoint": "http://localhost:5272",
        "ToolInvocationStrategy": "Functools"
      }
    ]
  }
}
```

### Troubleshooting

- **Model not available**: Run bootstrap script: `.\scripts\Setup-Environment.ps1`
- **Dropdown disabled**: Start new chat session to change models
- **Endpoint refused**: Verify Foundry/Ollama running with `.\Start-AspireHost.ps1`
```

#### T106: Architecture Diagrams Update
**Location**: Create `docs/architecture-diagrams.md`

```markdown
# Architecture: Agent Framework Integration

## Component Diagram

```
┌─────────────────────────────────────────────────────────┐
│                    Blazor Server UI                      │
│  ┌───────────────────────────────────────────────────┐  │
│  │  Chat.razor (Model Dropdown + Message Input)      │  │
│  │  • ModelSelector component                        │  │
│  │  • AgentThread lifecycle management               │  │
│  └─────────────────┬───────────────────────────────┬─┘  │
│                    │                               │    │
└────────────────────┼───────────────────────────────┼────┘
                     │                               │
                     ▼                               ▼
         ┌─────────────────────┐       ┌──────────────────────┐
         │  ChatAgentService   │       │  IPromptProvider     │
         │  • RunAsync()       │◄──────┤  • GetSystemPrompt() │
         │  • CreateThread()   │       │  • ToolInvocation    │
         └──────────┬──────────┘       │    Strategy          │
                    │                  └──────────────────────┘
                    ▼
         ┌─────────────────────┐
         │  ChatClientAgent    │
         │  (Agent Framework)  │
         └──────────┬──────────┘
                    │
        ┌───────────┴───────────┐
        │  Conditional Handler  │
        │  (ToolInvocationStrategy)
        │                       │
        ▼                       ▼
┌──────────────┐      ┌─────────────────┐
│ FunctoolsHandler│    │ Native Tools    │
│ (Phi-4/Qwen) │      │ (GPT-4o/Gemini) │
└──────┬───────┘      └─────────────────┘
       │
       ▼
┌──────────────────┐
│  ToolRegistry    │
│  • OpenMeteo     │
│  • Custom Tools  │
└──────────────────┘
```

## Data Flow: User Query → Response

1. User sends message in Chat.razor
2. AgentThread maintains conversation state
3. ChatAgentService.RunAsync() invoked
4. IPromptProvider loads system prompt
5. ChatClientAgent processes with conditional handler:
   - If ToolInvocationStrategy set: Apply FunctoolsHandler
   - If null: Use native tool calling
6. Tools invoked via ToolRegistry
7. Response streamed back to UI

## Migration: IChatClient → ChatClientAgent

**Before (Direct IChatClient)**:
```
UI → IChatClient → Manual History → Tools → Response
```

**After (Agent Framework)**:
```
UI → ChatAgentService → ChatClientAgent → AgentThread →
     IToolInvocationHandler → Tools → Response
```

**Benefits**:
- ✅ Standardized agent patterns
- ✅ Built-in conversation management
- ✅ Pluggable tool invocation strategies
- ✅ Future multi-agent orchestration support
```
```

---

## 📊 Progress Tracking

### Completed
- [X] A01: Documentation consistency (spec.md, plan.md)
- [X] A05: FR-009 clarification
- [X] A07: Test migration requirement specification
- [X] A09: User Story 4 tense correction
- [X] A10: Terminology standardization
- [X] A12: Version requirement alignment
- [X] A13: Task note simplification

### In Progress
- [ ] A02: Agent migration tests (T052-T054) - **CRITICAL**
- [ ] A03: Accessibility tests (T069-T071) - **HIGH**
- [ ] A04: OpenMeteo encapsulation tests (T093, T093a) - **HIGH**
- [ ] A08: Cross-platform scripts (T096-T097, T101) - **MEDIUM**
- [ ] A11: Documentation updates (T104, T106) - **MEDIUM**

### Blocked/Low Priority
- [ ] A06: FR-020 vs Edge Cases duplication (editorial consolidation)

---

## 🎯 Validation Checklist

After completing all remediations, verify:

- [ ] All 13 findings addressed
- [ ] Build succeeds: `dotnet build --configuration Release`
- [ ] All tests pass: `dotnet test --configuration Release`
- [ ] Test count ≥ 172 (baseline + new tests)
- [ ] Accessibility scan: Zero violations in axe DevTools
- [ ] Cross-platform: All scripts execute on Windows/Linux/macOS
- [ ] Documentation: README includes model selection workflow
- [ ] Architecture diagrams reflect Agent Framework migration

---

## 📞 Support

For questions or issues during remediation:
- Review `specs/003-model-abstraction/quickstart.md` for setup guidance
- Check `CHANGELOG.md` for recent refactoring context
- Run `.\Start-AspireHost.ps1 --validate-config` to verify configuration
