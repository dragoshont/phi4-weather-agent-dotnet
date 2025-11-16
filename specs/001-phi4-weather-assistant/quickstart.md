# Developer Quick Start

**Project**: Phi-4 Weather Assistant  
**Date**: 2025-11-16  
**Audience**: Developers setting up local environment

---

## Prerequisites

- **.NET 10 SDK** (10.0.100+) with preview features enabled
- **Foundry Local** (Windows/macOS) OR **Ollama** (Linux) for Phi-4 model hosting
- **Git** (for repository cloning)
- **VS Code** or **Visual Studio 2025** (recommended)
- **Node.js 20+** (for Playwright E2E tests)

---

## Quick Setup

### 1. Clone Repository

```bash
git clone https://github.com/dragoshont/phi4-weather-agent-dotnet.git
cd phi4-weather-agent-dotnet
git checkout 001-phi-weather-assistant
```

### 2. Run Platform Setup Script

**Windows**:
```powershell
.\scripts\setup-windows.ps1
```

**macOS**:
```bash
chmod +x scripts/setup-macos.sh
./scripts/setup-macos.sh
```

**Linux**:
```bash
chmod +x scripts/setup-linux.sh
./scripts/setup-linux.sh
```

### 3. Restore Dependencies

```bash
dotnet restore
```

### 4. Run Application

```bash
dotnet run --project src/Phi4WeatherAgent.AppHost
```

**Expected Output**:
- Aspire Dashboard launches at `http://localhost:15000`
- Web UI available at `http://localhost:5000` (or random port)
- Phi-4 model loads (first run may take 1-2 minutes)

### 5. Try Example Queries

Open browser to web UI, type one of these:

- "What's the weather in Seattle?"
- "Will it rain tomorrow in Portland?"
- "What are the pollen levels in Paris?"
- "Plan my weekend in Denver"

---

## Example Natural Language Queries

### Location-Based Queries

**Pattern**: "What's the weather in {location}?"

```text
✅ "What's the weather in Seattle?"
✅ "Current temperature in Paris, France"
✅ "How's the weather in Tokyo?"
✅ "Weather for 98101" (postal code)
```

**Agent Behavior**:
1. Geocodes location name using `GeocodeTool`
2. Retrieves forecast using `WeatherTool`
3. Displays current conditions + 7-day forecast in `WeatherCard`

---

### Time-Based Queries

**Pattern**: "Will it {condition} {time} in {location}?"

```text
✅ "Will it rain tomorrow in Portland?"
✅ "Is it going to snow this weekend in Denver?"
✅ "What's the weather next Monday in Austin?"
✅ "Should I bring an umbrella today?"
```

**Agent Behavior**:
1. Geocodes location (or uses conversation context)
2. Retrieves 7-day forecast
3. Filters relevant day(s) from `DailyForecast`
4. Highlights precipitation probability and conditions

---

### Multi-Day Queries

**Pattern**: "Plan my {event} in {location}" OR "Weather this {time range}"

```text
✅ "Plan my weekend in Denver"
✅ "What's the weather this week in Seattle?"
✅ "Best day for hiking next week in Yosemite?"
✅ "Compare Monday and Tuesday weather in Boston"
```

**Agent Behavior**:
1. Geocodes location
2. Retrieves full 7-day forecast
3. Uses `WeatherComparison` component for side-by-side comparison
4. Recommends best day(s) based on precipitation, temperature, wind

---

### Allergen/Pollen Queries

**Pattern**: "What are the pollen levels in {location}?" OR "Allergy forecast"

```text
✅ "What are the pollen levels in Paris?"
✅ "Allergy forecast for London"
✅ "Should I take allergy medicine today?" (with location context)
✅ "Grass pollen count in Berlin"
```

**Agent Behavior**:
1. Geocodes location
2. Retrieves allergen data using `AllergenTool`
3. Displays `AllergenCard` with pollen types (grass, birch, ragweed, etc.)
4. Shows severity badges (Low/Moderate/High/VeryHigh)
5. **Europe only**: Gracefully handles non-EU queries with "Pollen data available for Europe only"

---

### Planning Queries

**Pattern**: "Should I {activity} based on weather?"

```text
✅ "Should I go outside with allergies?"
✅ "Is it a good day for a picnic in Austin?"
✅ "Can I bike to work tomorrow?" (with location context)
✅ "Best time to visit SF this week?"
```

**Agent Behavior**:
1. Retrieves weather + allergen data
2. Analyzes conditions for requested activity
3. Provides recommendation with reasoning (e.g., "High pollen levels, consider indoor activities")

---

### Ambiguous Location Handling

**Pattern**: User provides location with multiple matches

```text
User: "What's the weather in Springfield?"
Agent: "I found multiple locations named Springfield:
  1. Springfield, Illinois, United States
  2. Springfield, Massachusetts, United States
  3. Springfield, Missouri, United States
  Which one would you like weather for?"

User: "Illinois"
Agent: [Shows weather for Springfield, IL]
```

**Implementation**: `GeocodeTool` returns multiple results, agent asks for clarification

---

## System Prompt Template

The agent uses this system prompt to constrain responses:

```text
You are a weather assistant powered by local AI. You provide weather forecasts and pollen/allergen information.

**Available Data**:
- Current weather conditions
- 7-day weather forecast (temperature, precipitation, wind, clouds)
- Pollen levels (grass, birch, alder, ragweed, mugwort, olive) - Europe only, 4-day forecast

**Tools You Can Use**:
- geocode_location: Convert location name to coordinates
- get_weather_forecast: Get weather data for coordinates
- get_allergen_levels: Get pollen data for coordinates (Europe only)

**Instructions**:
1. ALWAYS ask for location if user doesn't specify it
2. For ambiguous locations (e.g., "Springfield"), ask which one they mean
3. For pollen queries outside Europe, politely explain data is Europe-only and offer weather instead
4. Use natural, conversational language - avoid overly formal responses
5. Highlight key details: temperature, precipitation chance, pollen severity
6. For multi-day queries, compare days side-by-side
7. For planning questions, provide recommendations based on weather conditions

**Format**:
- Use structured weather cards for forecast data
- Include relevant icons/emojis for weather codes
- Display temperatures in user's preferred units (default °F for US, °C for others)
```

---

## Conversation Context Strategy

**Implementation**: In-memory `List<ChatMessage>` per SignalR session

```csharp
// AgentService.cs
private readonly Dictionary<string, List<ChatMessage>> _conversations = new();

public async Task<ChatMessage> SendMessageAsync(string sessionId, string userMessage)
{
    // Get or create conversation history
    if (!_conversations.TryGetValue(sessionId, out var history))
    {
        history = new List<ChatMessage>();
        _conversations[sessionId] = history;
    }
    
    // Add user message
    history.Add(new ChatMessage
    {
        Id = Guid.NewGuid(),
        Role = ChatRole.User,
        Content = userMessage,
        Timestamp = DateTimeOffset.UtcNow
    });
    
    // Send to agent with full history for context
    var response = await _chatClient.CompleteAsync(history);
    
    // Store assistant response
    history.Add(response);
    
    return response;
}
```

**Key Points**:
- No persistence (per zero-cost principle)
- Context cleared on SignalR disconnect
- Supports follow-up questions like "What about tomorrow?" (remembers location)

---

## Architecture Diagram

```
┌─────────────────────────────────────────────────────────────┐
│                    Aspire AppHost (DCP)                     │
│  ┌──────────────────┐    ┌──────────────────────────────┐  │
│  │  Foundry Local   │    │  Ollama (Linux alternative)  │  │
│  │  (Win/macOS)     │    │                              │  │
│  │  Phi-4 14B Model │    │  Phi-4 14B Model             │  │
│  └────────┬─────────┘    └──────────────┬───────────────┘  │
│           │                              │                   │
│           └──────────────┬───────────────┘                   │
│                          │ IChatClient                       │
│                ┌─────────▼────────────┐                      │
│                │   Agent Backend      │                      │
│                │   (ASP.NET Core)     │                      │
│                │                      │                      │
│                │  ┌────────────────┐  │                      │
│                │  │ AgentService   │  │                      │
│                │  │ (Orchestration)│  │                      │
│                │  └───────┬────────┘  │                      │
│                │          │            │                      │
│                │  ┌───────▼────────┐  │                      │
│                │  │ MCP Tools      │  │                      │
│                │  │ • GeocodeTool  │  │                      │
│                │  │ • WeatherTool  │  │                      │
│                │  │ • AllergenTool │  │                      │
│                │  └───────┬────────┘  │                      │
│                │          │            │                      │
│                │  ┌───────▼────────┐  │                      │
│                │  │ HTTP Clients   │◄─┼── Polly Retry        │
│                │  │ (OpenMeteo)    │  │   + Circuit Breaker  │
│                │  └────────────────┘  │                      │
│                └──────────┬───────────┘                      │
│                           │ SignalR                          │
│                ┌──────────▼───────────┐                      │
│                │   Blazor Server UI   │                      │
│                │   (Web Frontend)     │                      │
│                │                      │                      │
│                │  ┌────────────────┐  │                      │
│                │  │ Chat Interface │  │                      │
│                │  │ WeatherCard    │  │                      │
│                │  │ AllergenCard   │  │                      │
│                │  └────────────────┘  │                      │
│                └──────────────────────┘                      │
└─────────────────────────────────────────────────────────────┘
                           │
                           │ HTTPS
                           ▼
              ┌────────────────────────┐
              │   OpenMeteo APIs       │
              │  • Geocoding API       │
              │  • Weather Forecast    │
              │  • Air Quality (Pollen)│
              └────────────────────────┘
```

---

## Troubleshooting

### Issue: "Phi-4 model not found"

**Windows/macOS**:
```powershell
# Install Foundry Local
# Follow: https://foundry.ms/install
```

**Linux**:
```bash
# Install Ollama
curl -fsSL https://ollama.com/install.sh | sh

# Pull Phi-4 model
ollama pull phi4
```

### Issue: "Aspire Dashboard won't open"

**Check DCP Status**:
```bash
dotnet aspire --version  # Should show 13.0.0-preview.1+
```

**Check Port Conflicts**:
```bash
# Dashboard default: http://localhost:15000
# If port in use, AppHost assigns random port (check console output)
```

### Issue: "Pollen data returns null"

**Expected Behavior**:
- Pollen data is **Europe only**
- For US locations (e.g., Seattle), agent should explain limitation and show weather instead

**Example Response**:
```text
User: "What are the pollen levels in Seattle?"
Agent: "Pollen data is currently available for Europe only. However, I can show you the weather forecast for Seattle instead! [WeatherCard]"
```

### Issue: "OpenMeteo API timeouts"

**Check Polly Configuration**:
- Retry policy: 3 attempts with exponential backoff (2^n seconds)
- Circuit breaker: Opens after 5 consecutive failures, breaks for 1 minute

**Verify Network**:
```bash
curl https://api.open-meteo.com/v1/forecast?latitude=47.6062&longitude=-122.3321
```

---

## Testing

### Run Unit Tests

```bash
dotnet test tests/Phi4WeatherAgent.Agent.Tests
dotnet test tests/Phi4WeatherAgent.Web.Tests
```

### Run E2E Tests (Playwright)

```bash
cd tests/Phi4WeatherAgent.E2E.Tests
npm install
npx playwright install
dotnet test
```

### Manual Accessibility Testing (NVDA/JAWS)

1. **Windows**: Install NVDA (free, open-source)
2. **Start screen reader**
3. **Navigate to web UI**
4. **Test keyboard-only workflow**:
   - Tab to chat input
   - Type query
   - Press Enter
   - Tab to weather card
   - Verify screen reader reads: "Weather for Seattle: 52 degrees, partly cloudy, high 58, low 45"
5. **Expected time**: <2min for complete workflow

**Success Criteria**:
- ✅ All interactive elements reachable via Tab
- ✅ No keyboard traps
- ✅ Weather card content read aloud
- ✅ Live region announcements working for new messages

---

## Next Steps

- **Read**: [constitution.md](./constitution.md) for project principles
- **Read**: [spec.md](./spec.md) for functional requirements
- **Read**: [plan.md](./plan.md) for technical architecture
- **Read**: [data-model.md](./data-model.md) for entity definitions
- **Read**: [contracts/README.md](./contracts/README.md) for MCP tool signatures
- **Contribute**: Check [tasks.md](./tasks.md) for implementation status
