# Testing Strategy: Functools Invocation Layer

## Problem Statement

**Issue Discovered**: In the web app, functools blocks (`functools[{...}]`) were being displayed directly to users instead of being parsed and executed by the `FunctoolsChatClient` decorator.

**Root Cause**: The `Chat.razor` component was using `ChatOptionsBuilder` which attempted to configure native function calling (via `ChatOptions.Tools`). Since Phi-4 does NOT support native function calling, this bypassed the functools parsing mechanism entirely.

**Why Tests Didn't Catch It**: 
- Unit tests for `FunctoolsParser`, `ToolInvoker`, and `FunctoolsChatClient` all passed ✅
- Integration test `FunctoolsChatClientIntegrationTests.PollenQuery_Austin_ShouldNotShowFunctoolsInResponse` passed ✅
- **Missing**: E2E test that exercises the actual web UI with real DI configuration

## Testing Gaps Identified

### 1. No E2E Tests (Critical Gap)

**Current State**: 
- Spec calls for Playwright E2E tests (T084-T086, T112-T113)
- CI workflow has placeholder for E2E tests
- **Zero E2E tests actually exist**

**What E2E Tests Would Catch**:
- ✅ Functools displayed in chat UI (this bug)
- ✅ Tools not being registered by `ToolDiscoveryService`
- ✅ DI configuration errors (wrong decorators, missing services)
- ✅ System prompt not being applied
- ✅ Real model responses (if using mock model endpoint)

### 2. Integration Tests Don't Test DI Container

**Current State**:
- `FunctoolsChatClientIntegrationTests` manually constructs services:
  ```csharp
  var parser = new FunctoolsParser();
  var registry = new ToolRegistry();
  var invoker = new ToolInvoker(registry, ...);
  var functoolsClient = new FunctoolsChatClient(mockClient, parser, invoker, logger);
  ```

**Problem**: This doesn't test the actual `Program.cs` service registration

**What's Missing**:
- ✅ Test using `WebApplicationFactory<Program>` to test real DI configuration
- ✅ Test that `ChatOptionsBuilder` is NOT interfering with functools
- ✅ Test that `ToolDiscoveryService` runs and registers tools at startup

### 3. No Component Tests for Chat.razor

**Current State**:
- No bUnit tests for Blazor components
- Spec calls for component tests but none exist

**What Component Tests Would Catch**:
- ✅ Chat component properly uses injected `IChatClient`
- ✅ Messages are rendered correctly (no functools in DOM)
- ✅ System prompt is added to conversation

## Recommended Testing Strategy

### Phase 1: Add WebApplicationFactory Integration Tests (Immediate)

Create `Phi4WeatherAgent.Web.Tests` project with tests that:

1. **Test Real DI Configuration**
   ```csharp
   [Fact]
   public async Task ChatClient_ShouldBeFunctoolsChatClient()
   {
       var factory = new WebApplicationFactory<Program>();
       var client = factory.Services.GetRequiredService<IChatClient>();
       
       // Verify it's wrapped by FunctoolsChatClient
       Assert.IsType<FunctoolsChatClient>(client);
   }
   ```

2. **Test Tool Discovery at Startup**
   ```csharp
   [Fact]
   public async Task Startup_ShouldRegisterTools()
   {
       var factory = new WebApplicationFactory<Program>();
       
       // Wait for ToolDiscoveryService to complete
       await Task.Delay(2000);
       
       var registry = factory.Services.GetRequiredService<IToolRegistry>();
       Assert.True(registry.TryGet("GeocodeLocation", out _));
       Assert.True(registry.TryGet("GetWeather", out _));
       Assert.True(registry.TryGet("GetForecast", out _));
   }
   ```

3. **Test Functools NOT in ChatOptions**
   ```csharp
   [Fact]
   public void ChatOptions_ShouldNotContainNativeTools()
   {
       var factory = new WebApplicationFactory<Program>();
       var chatOptions = new ChatOptions(); // As used by Chat.razor
       
       // ChatOptions.Tools should be null/empty for functools format
       Assert.Null(chatOptions.Tools);
   }
   ```

### Phase 2: Add Playwright E2E Tests (High Priority)

Create `Phi4WeatherAgent.E2E.Tests` project:

**Critical Test** (Would have caught this bug):
```csharp
[Test]
public async Task WeatherQuery_ShouldNotShowFunctools()
{
    // Arrange
    await Page.GotoAsync("http://localhost:5000");
    
    // Act - Type "What's the weather in Seattle?"
    await Page.FillAsync("input[placeholder='Ask about the weather...']", 
        "What's the weather in Seattle?");
    await Page.PressAsync("input", "Enter");
    
    // Wait for response
    await Page.WaitForSelectorAsync(".chat-message.assistant");
    
    // Assert - Response should NOT contain "functools["
    var response = await Page.TextContentAsync(".chat-message.assistant");
    Assert.That(response, Does.Not.Contain("functools["));
    Assert.That(response, Does.Not.Contain("GeocodeLocation"));
    
    // Response SHOULD contain weather data or tool execution result
    Assert.That(response, Does.Match(@"(weather|temperature|conditions|celsius|sunny|cloudy)"));
}
```

**Additional E2E Tests** (Per Spec):
- T084: Basic weather query for Seattle
- T085: Ambiguous location (Springfield)
- T086: API timeout with retry
- T112: Pollen query for Austin
- T113: Europe-only constraint error
- T115: Multi-day planning query
- T116: Weekend comparison

### Phase 3: Add bUnit Component Tests (Medium Priority)

Create `Phi4WeatherAgent.Web.Tests` with bUnit:

```csharp
[Fact]
public async Task Chat_RendersSystemPrompt()
{
    // Arrange
    using var ctx = new TestContext();
    ctx.Services.AddSingleton<IChatClient>(Mock.Of<IChatClient>());
    
    // Act
    var cut = ctx.RenderComponent<Chat>();
    
    // Assert - System prompt should be in messages but not rendered
    var messages = cut.Instance.GetType()
        .GetField("messages", BindingFlags.NonPublic | BindingFlags.Instance)
        .GetValue(cut.Instance) as List<ChatMessage>;
    
    Assert.Contains(messages, m => m.Role == ChatRole.System);
    Assert.DoesNotContain("You are a helpful weather assistant", cut.Markup);
}
```

## Testing Checklist

### Immediate Actions (Block PR merge until complete)

- [ ] Create `Phi4WeatherAgent.Web.Tests` project
- [ ] Add `WebApplicationFactory` integration test for DI configuration
- [ ] Add test verifying `IChatClient` is `FunctoolsChatClient`
- [ ] Add test verifying tools are registered at startup
- [ ] Add test verifying `ChatOptions.Tools` is NOT used

### High Priority (Complete before release)

- [ ] Create `Phi4WeatherAgent.E2E.Tests` project  
- [ ] Install Playwright NuGet package
- [ ] Configure Playwright browsers (Chromium, Firefox, WebKit)
- [ ] Implement T084: Basic weather query E2E test
- [ ] Implement critical test: Functools NOT shown in UI
- [ ] Add E2E test to CI workflow
- [ ] Update README with E2E test instructions

### Medium Priority (Post-MVP)

- [ ] Add bUnit component tests for Chat.razor
- [ ] Add bUnit tests for ChatMessageList, ChatInput
- [ ] Implement remaining E2E tests (T085-T086, T112-T113, T115-T116)
- [ ] Add accessibility E2E tests with screen reader simulation
- [ ] Add performance E2E tests (response time < 5s per spec)

## CI/CD Integration

### Update `.github/workflows/ci.yml`

The CI workflow already has a placeholder for E2E tests:

```yaml
- name: Run E2E tests (Playwright)
  if: hashFiles('src/Phi4WeatherAgent.E2E.Tests') != ''
  run: dotnet test src/Phi4WeatherAgent.E2E.Tests --no-build --configuration Release
```

**Action Required**:
1. Change path from `src/` to `tests/` to match project structure
2. Add Playwright browser installation step:
   ```yaml
   - name: Install Playwright browsers
     if: hashFiles('tests/Phi4WeatherAgent.E2E.Tests') != ''
     working-directory: tests/Phi4WeatherAgent.E2E.Tests
     run: |
       npm install
       npx playwright install --with-deps
   ```

## Spec Updates Required

### Update `spec.md` Section 5.4

Add explicit requirement:

**FR-021-E2E**: System MUST include Playwright E2E test verifying that functools blocks are NOT displayed in chat UI. Test MUST:
- Send user query that triggers tool invocation
- Verify response does NOT contain "functools[" string
- Verify response DOES contain natural language or tool execution result

### Update `tasks.md`

Add new task:

**T300** [CRITICAL] Create WebApplicationFactory integration test verifying IChatClient is FunctoolsChatClient and not bypassed by ChatOptionsBuilder
- Priority: P0 (blocks merge)
- Location: tests/Phi4WeatherAgent.Web.Tests/Integration/ChatClientConfigurationTests.cs

**T301** [CRITICAL] Create Playwright E2E test verifying functools are not shown in chat UI
- Priority: P0 (blocks merge)
- Location: tests/Phi4WeatherAgent.E2E.Tests/FunctoolsExecutionTests.cs

## Lessons Learned

### Why This Bug Occurred

1. **Spec Compliance Gap**: E2E tests were specified (T084-T086) but not implemented
2. **Integration Test Scope**: Integration tests didn't exercise real DI configuration
3. **Manual Testing Limitation**: Manual testing may have missed the issue if tools weren't registered
4. **No Smoke Test**: No automated test for "happy path" user workflow

### Prevention Strategy

1. **Test Pyramid**:
   - Unit tests (30%): Individual components (parser, invoker, registry) ✅ Exists
   - Integration tests (50%): Service interactions, DI configuration ⚠️ Incomplete
   - E2E tests (20%): Full user workflows ❌ Missing

2. **CI Gating**:
   - Block PR merge if E2E test project doesn't exist
   - Require minimum coverage: 1 E2E test per user story
   - Smoke test MUST pass before deployment

3. **Test-First Development**:
   - Write E2E test BEFORE implementing feature
   - Ensures test actually catches regressions
   - Example: Write functools E2E test, see it fail, then fix

## References

- [Playwright for .NET](https://playwright.dev/dotnet/)
- [bUnit Documentation](https://bunit.dev/)
- [WebApplicationFactory Testing](https://learn.microsoft.com/en-us/aspnet/core/test/integration-tests)
- Spec: `specs/001-phi-weather-assistant/spec.md` Section 5.4 (FR-021)
- Tasks: `specs/001-phi-weather-assistant/tasks.md` (T084-T086, T112-T113)
