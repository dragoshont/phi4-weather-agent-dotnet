# High-Priority Requirements Review

**Created**: 2025-11-16  
**Purpose**: Address 22 high-priority gaps identified in requirements-quality.md before Phase 2 task decomposition  
**Status**: 🔴 In Review

---

## 🔴 Category 1: Loading & Empty States (CHK003-005)

### CHK003 - Loading State Requirements ❌ MISSING

**Current State**: No requirements specified for loading UI during:
- Model inference (Phi-4 processing query)
- Geocoding API calls
- Weather/allergen API calls

**Impact**: Implementation ambiguity - developers won't know expected UX during async operations

**Recommendation - Add to spec.md Functional Requirements**:
```markdown
FR-029: System MUST display loading indicators during asynchronous operations:
  - Chat message: "Thinking..." spinner while Phi-4 processes query
  - Weather card: Skeleton loader while fetching OpenMeteo data
  - Geocoding: "Searching for location..." text during geocoding API call
  
FR-030: Loading indicators MUST include ARIA live region announcements:
  - aria-live="polite" for non-critical updates
  - aria-label="Loading weather data" for screen reader users
```

**Acceptance Criteria Addition**:
```markdown
SC-013: Loading states appear within 200ms of user action and include accessible labels
SC-014: Loading indicators disappear immediately upon data arrival or error
```

---

### CHK004 - Empty/Zero-State Requirements ❌ MISSING

**Current State**: No requirements for scenarios with no data:
- Geocoding returns zero results (typo, non-existent location)
- Weather API returns no forecast data
- Allergen API returns no pollen data

**Impact**: Undefined behavior - developers may implement inconsistent error vs empty state handling

**Recommendation - Add to spec.md Edge Cases**:
```markdown
- What happens when **geocoding returns zero results**? → Display empty state: "No locations found for '[query]'. Please check spelling or try nearby city." with example suggestions
- What happens when **weather data unavailable for valid location**? → Display: "Weather data unavailable for [location]. This may be due to remote location or temporary API issue."
- What happens when **allergen data missing for location**? → Display: "Pollen data unavailable for [location]. Try nearby city: [suggestions from geocoding]"
```

**Functional Requirement Addition**:
```markdown
FR-031: System MUST distinguish between API errors (network failure, timeout) and empty results (valid response, zero data):
  - Empty results: Display helpful message with suggestions
  - API errors: Display error with retry action (per FR-026)
```

---

### CHK005 - System Initialization Requirements ❌ MISSING

**Current State**: No requirements for:
- Aspire AppHost startup sequence
- Phi-4 model loading time
- First query initialization (cold start)

**Impact**: Users may experience long first-query delays without explanation

**Recommendation - Add to spec.md Functional Requirements**:
```markdown
FR-032: System MUST display initialization status during first startup:
  - Aspire Dashboard link: "View telemetry at https://localhost:15888"
  - Model status: "Phi-4 model loaded" (or loading progress if detectable)
  - First query warning: "First query may take 5-10 seconds (cold start)"

FR-033: System MUST complete initialization checks before accepting user input:
  - Verify Foundry Local/Ollama endpoint reachable
  - Verify OpenMeteo API connectivity (health check)
  - Display error if prerequisites not met: "AI model not available. Run setup script."
```

**Success Criteria Addition**:
```markdown
SC-015: Initialization status visible to user within 1 second of application start
SC-016: First query completes within 10 seconds (cold start), subsequent queries <5s (warm)
```

---

## 🔴 Category 2: Recovery & Concurrency (CHK039-040)

### CHK039 - Recovery Flow Requirements ❌ MISSING

**Current State**: 
- FR-009 specifies Polly retry (3 attempts, exponential backoff)
- FR-026 specifies "immediate error" on failure
- **Gap**: No requirements for user-initiated retry or context restoration

**Impact**: After error, users have no clear path to recover without page refresh

**Recommendation - Update spec.md Edge Cases & Add FR**:
```markdown
Edge Case Update:
- What happens when **user wants to retry after API failure**? → Display "Retry" button in error message; clicking retries same query with preserved context

FR-034: System MUST provide user-initiated retry after errors:
  - Error messages include "Retry" button
  - Retry preserves original query and location context
  - Maximum 3 manual retries before suggesting page refresh

FR-035: System MUST restore conversation context after transient errors:
  - Location from previous successful query retained in-memory
  - Follow-up queries work after error recovery
  - Context cleared only on page refresh or explicit user action
```

**User Story 1 Scenario Addition**:
```markdown
4. **Given** OpenMeteo API fails with timeout, **When** user clicks "Retry" button in error message, **Then** system re-attempts geocoding and weather API calls with preserved location query
```

---

### CHK040 - Concurrent User Interaction Requirements ❌ MISSING

**Current State**: No requirements for:
- User submits second query before first completes
- Rapid successive queries (typing fast)
- Overlapping API calls

**Impact**: Race conditions possible - responses may arrive out of order, confusing UX

**Recommendation - Add to spec.md Functional Requirements**:
```markdown
FR-036: System MUST handle concurrent queries gracefully:
  - New query submission cancels in-flight query (CancellationToken)
  - Only most recent query response displayed
  - Loading indicator reflects current (not previous) query

FR-037: System SHOULD debounce rapid query submissions:
  - 300ms delay after user stops typing before submitting
  - Submit immediately on Enter key press
  - Cancel previous debounced submission if new query started
```

**Edge Case Addition**:
```markdown
- What happens when **user submits query while previous query processing**? → System cancels previous query via CancellationToken, displays loading state for new query
- What happens when **API responses arrive out of order** (slow query A finishes after fast query B)? → System discards stale responses, displays only most recent query result
```

---

## 🔴 Category 3: Edge Case Details (CHK044-052)

### CHK044 - Partial API Response Requirements ❌ MISSING

**Current State**: OpenMeteo API contracts in research.md show full responses, but no handling for partial data

**Example Scenarios**:
- Weather API returns current conditions but missing daily forecast
- Daily forecast has 3 days instead of 7
- Allergen API returns grass pollen but missing tree/weed data

**Recommendation - Add to spec.md Edge Cases**:
```markdown
- What happens when **weather API returns partial forecast** (e.g., 3 days instead of 7)? → Display available days only; show message "Forecast available for next [N] days"
- What happens when **allergen API returns incomplete pollen data**? → Display available categories only; hide missing categories (no "N/A" placeholders)
- What happens when **current weather missing but forecast available**? → Display forecast only; show "Current conditions unavailable"
```

**Functional Requirement Addition**:
```markdown
FR-038: System MUST gracefully handle partial API responses:
  - Validate required fields (latitude, longitude, timestamp)
  - Display available data without failing entire query
  - Log warning for missing optional fields (telemetry only, not user-facing)
```

---

### CHK045 - Malformed API Response Requirements ❌ MISSING

**Current State**: No requirements for JSON parsing errors or unexpected schema

**Example Scenarios**:
- OpenMeteo returns 500 error with HTML instead of JSON
- JSON schema change (new fields, removed fields)
- Invalid data types (string instead of number)

**Recommendation - Add to spec.md Edge Cases & FR**:
```markdown
Edge Cases:
- What happens when **OpenMeteo returns non-JSON response** (HTML error page)? → Catch JsonException, display error: "Weather service unavailable. Try again later."
- What happens when **API schema changes unexpectedly**? → Use nullable properties in DTOs, validate required fields, fail gracefully with error message

FR-039: System MUST validate API responses before processing:
  - Catch JsonException for malformed JSON
  - Validate required fields present (latitude, longitude, temperature, weather_code)
  - Log schema validation errors to telemetry for debugging
  - Display user-friendly error: "Unable to process weather data. Service may be experiencing issues."
```

---

### CHK046 - Unsupported Location Requirements ❌ MISSING

**Current State**: Edge case mentions "non-existent location" but doesn't cover:
- International locations (non-English names)
- Coordinates instead of names (e.g., "47.6062, -122.3321")
- Landmarks instead of cities (e.g., "Eiffel Tower")

**Recommendation - Add to spec.md Edge Cases**:
```markdown
- What happens when **user provides coordinates** (e.g., "47.6062, -122.3321")? → System detects coordinate pattern (regex), passes directly to weather API, skips geocoding
- What happens when **user asks for landmark/POI** (e.g., "weather at Eiffel Tower")? → Geocoding API resolves to Paris, France; system displays resolved location name
- What happens when **user provides non-English location name** (e.g., "北京")? → OpenMeteo Geocoding API supports Unicode; system handles UTF-8 transparently
```

**Functional Requirement Update** (FR-027):
```markdown
FR-027: System MUST validate location input before calling geocoding API:
  - Trim whitespace, check length (3-100 characters)
  - Accept Unicode characters (UTF-8)
  - Detect coordinate patterns: regex `^\s*-?\d+\.?\d*\s*,\s*-?\d+\.?\d*\s*$`
  - Pass coordinates directly to weather API (skip geocoding)
```

---

### CHK047 - Model Hallucination Beyond Domain ⚠️ AMBIGUITY

**Current State**: FR-014 says "system prompt constrains responses" with "no post-processing validation"

**Ambiguity**: What if system prompt fails? No fallback specified.

**Clarification Needed from User**: 
- **Option A**: Accept that Phi-4 may occasionally hallucinate outside domain (prompt-only approach)
- **Option B**: Add lightweight post-processing check (keyword detection for sports/politics/etc)
- **Option C**: Display disclaimer: "AI responses may occasionally be off-topic"

**Current Assessment**: ✅ **ACCEPTABLE AS-IS** - Constitution prioritizes local-first simplicity over perfect guardrails. Prompt engineering is the correct approach per clarification session.

**No spec change needed** - Already addressed in FR-014 and clarifications.

---

### CHK048 - Browser/Tab Close During Query ❌ MISSING

**Current State**: No requirements for cleanup when user navigates away mid-query

**Impact**: Resource leaks possible (HTTP requests, CancellationTokens not disposed)

**Recommendation - Add to spec.md Functional Requirements**:
```markdown
FR-040: System MUST handle browser/tab close gracefully:
  - Cancel in-flight API requests via CancellationToken
  - Blazor Server detects SignalR disconnect, cleans up resources
  - In-memory context automatically cleared (session-scoped)
  - No persistent state to clean up (per Constitution VI)
```

**Technical Constraint Addition**:
```markdown
Performance Targets:
- Resource cleanup: <1 second after SignalR disconnect
- No memory leaks after 100 query cycles (verified via dotMemory profiler)
```

---

### CHK049 - Rapid Successive Queries (Debouncing) ✅ ADDRESSED IN CHK040

See CHK040 Recovery & Concurrency section - FR-037 covers debouncing.

---

### CHK050 - Long User Input (Token Limits) ❌ MISSING

**Current State**: No requirements for:
- Query length limits
- Phi-4 context window (131K tokens input, 4K output)
- UI constraints on textarea

**Recommendation - Add to spec.md Functional Requirements**:
```markdown
FR-041: System MUST enforce input length limits:
  - Chat input: 2000 characters max (UI constraint)
  - Display character counter: "[X]/2000" below textarea
  - Disable submit button when limit exceeded
  - Phi-4 context window: 131K tokens (no user-facing limit, server-side only)

FR-042: System MUST handle token limit errors gracefully:
  - Catch exceptions from model inference
  - Display error: "Query too complex. Please simplify and try again."
  - Suggest shorter rephrasing if detected
```

**Edge Case Addition**:
```markdown
- What happens when **user submits extremely long query** (>2000 chars)? → UI prevents submission; display message "Query too long. Maximum 2000 characters."
- What happens when **combined context exceeds Phi-4 limits** (conversation history + query)? → Truncate oldest conversation turns; always include system prompt and current query
```

---

### CHK051 - Special Characters in Location Names ✅ PARTIAL COVERAGE

**Current State**: FR-027 mentions input validation but doesn't specify special character handling

**Enhancement Needed**:
```markdown
FR-027 Update: System MUST handle special characters in location input:
  - Accept Unicode (UTF-8): accents (Montréal), non-Latin scripts (北京, Москва)
  - URL-encode before API calls: Uri.EscapeDataString(locationName)
  - Trim leading/trailing whitespace, preserve internal spaces
  - Reject control characters, SQL injection patterns (basic sanitization)
```

**Edge Case Addition**:
```markdown
- What happens when **location name contains accents** (e.g., "São Paulo")? → System URL-encodes query, OpenMeteo API handles Unicode correctly
- What happens when **user attempts injection** (e.g., "Seattle'; DROP TABLE--")? → Input validation rejects non-standard characters; display error "Invalid location name"
```

---

### CHK052 - Resource Exhaustion Requirements ❌ MISSING

**Current State**: Performance target specifies <500MB memory, but no requirements for:
- Runaway inference (model loops indefinitely)
- Memory leak detection
- CPU throttling under load

**Recommendation - Add to spec.md Functional Requirements**:
```markdown
FR-043: System MUST prevent resource exhaustion:
  - Model inference timeout: 30 seconds (CancellationToken)
  - Memory monitoring: Log warning if >500MB, error if >1GB
  - CPU throttling: Limit concurrent model inferences to 1 (single-user app)
  
FR-044: System MUST log resource usage to Aspire Dashboard:
  - Memory usage per query (OpenTelemetry metrics)
  - CPU time for model inference
  - API call latency breakdown (geocoding, weather, allergen)
```

**Edge Case Addition**:
```markdown
- What happens when **model inference exceeds 30s timeout**? → Cancel inference via CancellationToken, display error: "Query timed out. Try simpler question."
- What happens when **memory usage exceeds 1GB**? → Log critical error, suggest application restart, display: "Application experiencing resource issues. Please refresh page."
```

---

## 🔴 Category 4: Non-Functional Requirements (CHK055-060)

### CHK055 - Security Requirements (XSS Prevention) ❌ MISSING

**Current State**: No security requirements despite user-generated content (chat messages)

**Risk**: XSS vulnerabilities if Phi-4 generates HTML/JavaScript in response

**Recommendation - Add to spec.md Functional Requirements**:
```markdown
FR-045: System MUST prevent XSS attacks:
  - Blazor Server escapes all output by default (use @variable, not @Html.Raw)
  - Validate location input: reject <script>, <iframe>, on* attributes
  - Content Security Policy: `script-src 'self'; object-src 'none';`
  - Sanitize Phi-4 responses: encode HTML entities before display

FR-046: System MUST implement input sanitization:
  - Location names: alphanumeric, spaces, hyphens, accents only
  - Query text: reject control characters, limit length (2000 chars)
  - No SQL/command injection risk (API calls are HTTP GET, no database)
```

**Success Criteria Addition**:
```markdown
SC-017: Application passes OWASP ZAP security scan (no high/critical findings)
SC-018: XSS attempts in query input are escaped/rejected (verified via E2E test)
```

---

### CHK056 - Observability Requirements Beyond Dashboard ❌ MISSING

**Current State**: FR-018 says "auto-launch Aspire Dashboard" but lacks telemetry details

**Gap**: What specific metrics/traces/logs are required?

**Recommendation - Update FR-018 & Add Details**:
```markdown
FR-018 Update: System MUST export telemetry to Aspire Dashboard:
  - Traces: All HTTP requests (OpenMeteo APIs), model inferences (Phi-4), Blazor Server events
  - Metrics: Query latency (p50/p95/p99), API success rate, memory usage, token count per query
  - Logs: Structured logging (JSON), log levels (Debug/Info/Warning/Error), correlation IDs

FR-047: System MUST use OpenTelemetry for instrumentation:
  - HTTP client instrumentation: automatic via AddHttpClient
  - Custom spans: Agent Framework query processing, MCP tool invocation
  - Correlation: TraceId/SpanId propagated across service boundaries
```

**Success Criteria Update**:
```markdown
SC-011 Update: Aspire Dashboard displays:
  - HTTP request traces with status codes, latency, retry attempts
  - Model inference spans with token count, temperature, prompt preview
  - Custom spans for geocoding, weather API, allergen API with coordinates
```

---

### CHK057 - Maintainability Requirements ❌ MISSING

**Current State**: No requirements for:
- Code organization principles
- Documentation standards
- Refactoring constraints

**Recommendation - Add to spec.md Technical Constraints**:
```markdown
Code Quality Requirements:
- C# coding standards: Follow Microsoft .NET conventions (CA rules enforced)
- XML documentation: Public APIs require XML doc comments (<summary>, <param>)
- Unit test naming: [MethodName]_[Scenario]_[ExpectedResult]
- Component organization: One component per file, max 300 lines per file
- Dependency injection: Constructor injection only (no service locator pattern)
```

**Success Criteria Addition**:
```markdown
SC-019: Code analysis produces zero warnings (enforce via <TreatWarningsAsErrors>true</TreatWarningsAsErrors>)
SC-020: All public APIs documented with XML comments (verified via docfx or similar)
```

---

### CHK058 - Deployment Requirements ❌ MISSING

**Current State**: Setup scripts exist (FR-020) but no deployment/distribution requirements

**Gap**: How do users run the app after setup? What's the deployment model?

**Recommendation - Add to spec.md Functional Requirements**:
```markdown
FR-048: System MUST support local development deployment only:
  - Run via `dotnet run --project Phi4WeatherAgent.AppHost`
  - Aspire Dashboard auto-launches at https://localhost:15888
  - Web UI accessible at https://localhost:5001 (or dynamically assigned port)
  - No production deployment (local-first exercise per Constitution)

FR-049: System SHOULD provide convenience scripts for running:
  - Windows: `run.ps1` (PowerShell script calling dotnet run)
  - macOS/Linux: `run.sh` (Bash script calling dotnet run)
  - Scripts detect if setup completed, prompt to run setup if missing
```

**Out-of-Scope (Document in Spec)**:
```markdown
Explicitly Out of Scope:
- Docker containerization (Constitution IV: No containers)
- Azure/cloud deployment (Constitution VI: Zero cloud costs)
- CI/CD pipelines (local-first development only)
- Release binaries (source code distribution only)
```

---

### CHK059 - Upgrade/Migration Requirements ❌ MISSING

**Current State**: No requirements for:
- Updating Phi-4 model to newer version
- Upgrading .NET 10 preview to RTM
- Migrating to stable Agent Framework (post-preview)

**Recommendation - Add to spec.md Technical Constraints**:
```markdown
Upgrade Strategy:
- Model updates: Re-run setup script; foundry model download phi4-mini pulls latest version
- .NET 10 SDK: Update global.json version, run dotnet restore, verify compatibility
- Agent Framework preview → stable: Update package versions, address breaking changes in separate task
- No data migration needed (no persistent state per Constitution VI)

FR-050: Setup scripts MUST check for existing installations:
  - Detect if Phi-4 model already cached (skip re-download)
  - Detect .NET 10 SDK version, warn if preview version outdated
  - Provide `--force-update` flag to re-download model/dependencies
```

---

### CHK060 - Backup/Recovery Requirements ✅ OUT OF SCOPE

**Current State**: No backup requirements mentioned

**Decision**: **EXPLICITLY OUT OF SCOPE** per Constitution Principle VI (Zero Cost, Stateless)

**Recommendation - Document in Spec**:
```markdown
Explicitly Out of Scope:
- Data backup (no persistent user data; conversation context in-memory only)
- Disaster recovery (local-first app; users can re-run setup if needed)
- State restoration (session-scoped context resets on page refresh)
```

**No functional requirement needed** - This is an intentional architectural decision.

---

## 🔴 Category 5: Ambiguities & Conflicts (CHK069-075)

### CHK069 - Validation Conflict: FR-014 vs FR-027 ⚠️ POTENTIAL CONFLICT

**Issue**:
- FR-014: "No post-processing validation" for Phi-4 responses
- FR-027: "Validate location input before calling geocoding API"

**Conflict Analysis**:
- These are **DIFFERENT validation types**:
  - FR-014: AI response validation (intentionally skipped)
  - FR-027: User input validation (required for API calls)
- **Not a true conflict**, but terminology could be clearer

**Recommendation - Clarify FR-014**:
```markdown
FR-014 Update: System MUST constrain Phi-4 responses to weather domain via system prompt only (no AI response post-processing or content filtering). Note: This does NOT affect input validation (FR-027) or API response validation (FR-038-039).
```

---

### CHK070 - Multi-Tab Context: FR-013 Ambiguity ⚠️ AMBIGUITY

**Issue**: FR-013 says "in-memory, session-scoped" but doesn't clarify multi-tab behavior

**Scenarios**:
- User opens two browser tabs: Do they share context?
- User opens private/incognito tab: Fresh context?

**Blazor Server Behavior** (Technical Reality):
- Each tab = separate SignalR connection = separate circuit = **isolated context**
- Context is per-circuit, not per-browser

**Recommendation - Clarify FR-013**:
```markdown
FR-013 Update: System MUST maintain conversation context per Blazor Server circuit (one context per browser tab/window):
  - Context isolated between tabs (no cross-tab sharing)
  - Context cleared on page refresh or tab close
  - Context includes: last queried location, conversation history (last 5 messages)
  - No persistent storage (in-memory Dictionary<CircuitId, ConversationContext>)
```

---

### CHK071 - Error vs Retry Conflict: FR-009 vs FR-026 ⚠️ POTENTIAL CONFLICT

**Issue**:
- FR-009: "Polly 3 retries with exponential backoff"
- FR-026: "Display immediate error message"

**Question**: Are these sequential (retry 3x, then immediate error) or conflicting?

**Clarification**: 
- **Sequential, not conflicting** - Polly retries happen **before** user-facing error
- Timeline: Request → Retry 1 → Retry 2 → Retry 3 → **Then** display error (FR-026)

**Recommendation - Clarify FR-026**:
```markdown
FR-026 Update: System MUST display error message immediately after Polly retry policy exhausts (3 failed attempts):
  - Loading indicator visible during retries (user sees "Loading..." not retry count)
  - Error appears after final retry failure
  - Error message: "Weather service unavailable. Please try again." with Retry button (FR-034)
```

---

### CHK072 - Zero Cost vs OpenMeteo Rate Limits ⚠️ AMBIGUITY

**Issue**: 
- Constitution VI: "Zero cloud costs"
- OpenMeteo: Free tier = 10,000 requests/day
- **What happens at 10,001 requests?**

**Clarification Needed**: 
- OpenMeteo free tier has no API key → Cannot track per-user limits
- 10K requests/day is per-IP (dev machine running locally)
- Single-user app unlikely to hit limit (278 queries/hour sustained)

**Recommendation - Document Assumption**:
```markdown
Assumptions (Add to Spec):
- OpenMeteo free tier (10K requests/day) sufficient for single-user local development
- Rate limit risk: Low (requires 278 queries/hour sustained for 24 hours)
- If rate limit hit: Treated as API error per FR-026, displays error message
- No mitigation beyond error handling (Constitution VI: No paid upgrades, no caching layer)
```

**Edge Case Addition**:
```markdown
- What happens when **OpenMeteo rate limit exceeded** (10K requests/day)? → API returns 429 status; system displays error: "Weather service rate limit reached. Try again tomorrow." No automatic retry (per FR-026 clarification).
```

---

### CHK073 - Vector Store Removal: FR-017 Clarity ✅ CLEAR

**Current State**: FR-017 says "remove vector store, document ingestion, semantic search"

**Question**: Does this mean:
- A) Delete code completely?
- B) Disable/comment out code?
- C) Keep code but don't use it?

**Recommendation - Clarify FR-017**:
```markdown
FR-017 Update: System MUST remove (delete) vector store, document ingestion, and semantic search components from aichatweb template:
  - Delete files: SemanticSearch.cs, Ingestion/, wwwroot/Data/
  - Remove dependencies: Microsoft.SemanticKernel.Connectors.Memory, vector DB packages
  - Remove UI: Document upload components, search bar
  - Verify build succeeds with zero warnings about missing types
```

---

### CHK074 - Template Base Conflict: Keep vs Remove ⚠️ AMBIGUITY

**Issue**: Architecture Decisions say:
- ✅ Keep: ChatInput.razor, ChatMessageList.razor, etc.
- ❌ Remove: SemanticSearch.cs, Ingestion/, etc.
- ✨ Customize: ChatMessageItem.razor, Chat.razor
- ➕ Add: AgentService, WeatherCard.razor, etc.

**Ambiguity**: What if some "Keep" components depend on "Remove" components?

**Example**: ChatMessageList.razor might import SemanticSearch.cs

**Recommendation - Add Dependency Analysis Task**:
```markdown
Phase 1 Task (Before Implementation):
- Analyze aichatweb template dependencies
- Identify components marked "Keep" that reference "Remove" components
- Create migration plan: Refactor "Keep" components to remove dependencies
- Document breaking changes in implementation plan
```

**No spec change needed** - This is implementation detail for Phase 2 task decomposition.

---

### CHK075 - Cross-Platform vs Platform-Specific Model Hosting ✅ NOT A CONFLICT

**Issue**: 
- FR-019: "Cross-platform (Windows, macOS, Linux)"
- FR-001: Platform-specific model hosting (Foundry vs Ollama)

**Analysis**: 
- **Not a conflict** - Code is cross-platform, deployment is platform-aware
- Aspire AppHost detects OS at runtime (OperatingSystem.IsLinux())
- IChatClient abstraction hides platform differences

**Recommendation - Clarify FR-019**:
```markdown
FR-019 Update: System MUST support cross-platform development with platform-aware configuration:
  - Source code: 100% cross-platform (no #if WINDOWS directives)
  - Runtime detection: Aspire AppHost detects OS, configures Foundry Local (Windows/macOS) or Ollama (Linux)
  - Setup scripts: Platform-specific (PowerShell for Windows, Bash for macOS/Linux)
  - User experience: Identical across platforms (IChatClient abstraction)
```

---

## 📋 Summary: Required Spec Updates

### Immediate Actions (Add to spec.md)

**New Functional Requirements (17 additions)**:
- FR-029: Loading indicators with ARIA labels
- FR-030: Loading state timing requirements
- FR-031: Empty state handling
- FR-032: Initialization status display
- FR-033: Startup prerequisite checks
- FR-034: User-initiated retry
- FR-035: Context restoration after errors
- FR-036: Concurrent query handling
- FR-037: Query debouncing
- FR-038: Partial API response handling
- FR-039: Malformed response validation
- FR-040: Browser close cleanup
- FR-041: Input length limits
- FR-042: Token limit error handling
- FR-043: Resource exhaustion prevention
- FR-044: Resource usage logging
- FR-045: XSS prevention
- FR-046: Input sanitization
- FR-047: OpenTelemetry instrumentation
- FR-048: Local deployment model
- FR-049: Convenience run scripts
- FR-050: Upgrade strategy

**New Success Criteria (8 additions)**:
- SC-013: Loading state timing
- SC-014: Loading indicator removal
- SC-015: Initialization status visibility
- SC-016: Cold start performance
- SC-017: Security scan passing
- SC-018: XSS test passing
- SC-019: Zero code warnings
- SC-020: XML documentation coverage

**Edge Case Additions (15 additions)**:
- Geocoding zero results
- Weather data unavailable
- Allergen data missing
- User-initiated retry
- Concurrent query submission
- Out-of-order API responses
- Partial forecast data
- Incomplete allergen data
- Current conditions missing
- Non-JSON API response
- Schema validation failure
- Coordinate input format
- Landmark geocoding
- Unicode location names
- Extremely long query input
- Token limit exceeded
- Special characters in location
- Injection attempts
- Model inference timeout
- Memory exhaustion
- OpenMeteo rate limit hit

**Clarifications (7 updates)**:
- FR-014: Clarify AI response vs input validation
- FR-013: Clarify per-circuit context isolation
- FR-026: Clarify retry-then-error sequence
- FR-017: Clarify "remove" means delete
- FR-019: Clarify cross-platform with platform-aware config
- FR-027: Add Unicode and coordinate handling

**Out-of-Scope Documentation**:
- Docker containerization
- Cloud deployment
- CI/CD pipelines
- Release binaries
- Data backup/recovery

**Assumptions to Document**:
- OpenMeteo 10K/day sufficient for single-user
- Rate limit risk low (278 queries/hour)
- No persistent state requires backup

---

## 🎯 Next Steps

1. **Review this document** with stakeholder/user for approval
2. **Update spec.md** with all 22 high-priority additions
3. **Re-run checklist validation** to verify gaps closed
4. **Proceed to Phase 2** task decomposition via `/speckit.tasks`

**Estimated Effort**: 30-45 minutes to integrate all changes into spec.md

---

**Status Tracking**:
- [x] Review approved by user
- [x] Spec updated with FR-029 through FR-050 (22 new functional requirements)
- [x] Spec updated with SC-013 through SC-020 (8 new success criteria)
- [x] Edge cases expanded (27 total edge cases, 21 new additions)
- [x] Clarifications integrated (FR-013, FR-014, FR-017, FR-018, FR-019, FR-026, FR-027)
- [x] Out-of-scope documented (Docker, cloud, CI/CD, binaries, backup)
- [x] Assumptions documented (OpenMeteo rate limits, disk space, RAM, browser compatibility)
- [ ] Checklist re-validated (next step: run validation)
