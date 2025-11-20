# Template Customization Scope

**Template**: `dotnet new aichatweb` (Blazor Server AI Chat Template)  
**Version**: .NET 10.0+  
**Date**: 2025-11-16

## Files to Keep (Minimal Modifications)

### Core Infrastructure
- `Program.cs` - **MODIFY**: Update IChatClient registration for Foundry Local/Ollama platform detection, remove vector search registration
- `appsettings.json` / `appsettings.Development.json` - **KEEP**: Standard ASP.NET configuration
- `Components/App.razor` - **KEEP**: Root component
- `Components/Routes.razor` - **KEEP**: Routing configuration
- `Components/Layout/MainLayout.razor` - **MODIFY**: Add skip links for accessibility (US4)
- `Components/_Imports.razor` - **KEEP**: Global using directives
- `wwwroot/css/app.css` - **MODIFY**: Add WCAG AA color contrast tokens (T031)

### Chat UI Components (Keep & Customize)
- `Components/Pages/Chat.razor` - **MODIFY**: Update system prompt for weather domain, add suggestion buttons (T040)
- `Components/Chat/ChatInput.razor` - **KEEP**: User input component
- `Components/Chat/ChatMessageItem.razor` - **MODIFY**: Add weather/allergen card rendering logic (T039, T049)
- `Components/Chat/ChatMessages.razor` - **KEEP**: Message list container

## Files to Remove

### Search/Ingestion Infrastructure (Not Needed)
- `Services/SemanticSearch.cs` - **DELETE**: Vector search not required
- `Services/Ingestion/` (entire folder) - **DELETE**: Document ingestion not needed
- `wwwroot/Data/` - **DELETE**: Sample documents not needed
- `wwwroot/lib/pdfjs-dist/` - **DELETE**: PDF viewer not required
- `Components/pdf_viewer.razor` - **DELETE**: PDF rendering not needed
- `Components/markdown_viewer.razor` - **DELETE**: Markdown rendering not needed
- `wwwroot/lib/dompurify/` - **DELETE**: DOM sanitization for markdown not needed

### Configuration Cleanup
- Remove vector search configuration from `appsettings.json`
- Remove `--provider ollama` flag usage (defer to AppHost platform detection)

## Files to Create (New Components)

### Weather UI Components
- `Components/Weather/WeatherCard.razor` (T038) - Display current + 7-day forecast
- `Components/Weather/AllergenCard.razor` (T048) - Pollen levels with severity badges
- `Components/Weather/WeatherComparison.razor` (T054) - Multi-day side-by-side comparison

### Backend Services (Agent Project)
- `src/Phi4WeatherAgent.Agent/Services/OpenMeteoGeocodeClient.cs` (T033)
- `src/Phi4WeatherAgent.Agent/Services/OpenMeteoWeatherClient.cs` (T034)
- `src/Phi4WeatherAgent.Agent/Services/OpenMeteoAllergenClient.cs` (T046)
- `src/Phi4WeatherAgent.Agent/Services/AgentService.cs` (T029) - Conversation orchestration
- `src/Phi4WeatherAgent.Agent/Tools/GeocodeTool.cs` (T036) - MCP geocoding
- `src/Phi4WeatherAgent.Agent/Tools/WeatherTool.cs` (T037) - MCP weather
- `src/Phi4WeatherAgent.Agent/Tools/AllergenTool.cs` (T047) - MCP allergen

## Customization Strategy

### Phase 1 (Setup)
1. **T013**: Scaffold template with `dotnet new aichatweb --name Phi4WeatherAgent.Web` (NO --provider flag)
2. **T014**: Delete unnecessary search/ingestion files listed above
3. **T015-T018**: Create Aspire projects (ServiceDefaults, AppHost, Agent, Tests)

### Phase 2 (Foundation)
1. **T022**: Configure AppHost for platform-specific AI model hosting
2. **T030**: Wire IChatClient in Web project based on AppHost detection

### Phase 3-6 (User Stories)
1. Add weather-specific UI components
2. Implement MCP tools in Agent project
3. Customize ChatMessageItem for structured data rendering

## Why This Approach?

- **Principle VIII**: Leverage existing template infrastructure (SignalR, Blazor Server, chat UI)
- **Zero Cloud Costs**: Remove paid dependencies (vector search, OpenAI defaults)
- **Agent Framework**: Keep MCP tool patterns, replace search with weather APIs
- **Accessibility**: Template provides semantic HTML foundation, we add ARIA/WCAG compliance
- **Minimal Rework**: Keep 70% of template code, remove 20%, add 10% weather-specific

## Template Limitations

1. **Search-First Design**: Template assumes document search use case → solution: repurpose chat flow for weather queries
2. **OpenAI/Ollama Defaults**: Hardcoded provider flags → solution: defer to AppHost platform detection
3. **No MCP Tool Examples**: Template lacks weather domain tools → solution: reference Agent Framework docs for MCP patterns
4. **No Accessibility Examples**: Template provides structure but not WCAG compliance → solution: add ARIA labels, keyboard nav, contrast ratios

## Success Criteria

- [ ] Template scaffolded without search artifacts
- [ ] All unnecessary files removed (total reduction: ~15 files, ~2000 LOC)
- [ ] Weather components integrate seamlessly with chat UI
- [ ] AppHost successfully detects platform and configures AI provider
- [ ] No Semantic Kernel dependencies (CI enforcement in T068)
