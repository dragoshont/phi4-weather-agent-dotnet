using Microsoft.Playwright;
using Xunit;

namespace LocalAIAgent.Integration.Tests;

/// <summary>
/// CRITICAL smoke test that verifies functools are not displayed in the UI.
/// This test would have caught the ChatOptionsBuilder bug where raw functools
/// format was showing in the chat instead of being executed.
///
/// Test Requirements:
/// - Web app must be running (dotnet run --project src/LocalAIAgent.Web)
/// - Model must be available (Foundry Local or Ollama)
/// - Default URL: http://localhost:5089
///
/// Usage:
/// - Run before PR approval to verify functools execution works
/// - Run in CI to catch configuration regressions
/// </summary>
[Collection("E2E")]
public class FunctoolsSmokeTest : IAsyncLifetime
{
    private IPlaywright? _playwright;
    private IBrowser? _browser;
    private const string BaseUrl = "http://localhost:5089";
    private const int TimeoutSeconds = 120; // Allow time for model inference

    public async Task InitializeAsync()
    {
        _playwright = await Playwright.CreateAsync();
        _browser = await _playwright.Chromium.LaunchAsync(new()
        {
            Headless = true
        });
    }

    public async Task DisposeAsync()
    {
        if (_browser != null)
        {
            await _browser.CloseAsync();
            await _browser.DisposeAsync();
        }
        _playwright?.Dispose();
    }

    [Fact(Skip = "Requires running web app - manual execution only")]
    public async Task WeatherQuery_ShouldNotShowFunctoolsInResponse()
    {
        // Arrange
        var page = await _browser!.NewPageAsync();

        try
        {
            await page.GotoAsync(BaseUrl);
            await page.WaitForLoadStateAsync(LoadState.NetworkIdle);

            // Act - Send a weather query that requires tool invocation
            var inputSelector = "textarea[placeholder*='Ask']";
            await page.WaitForSelectorAsync(inputSelector, new() { Timeout = 5000 });

            await page.FillAsync(inputSelector, "What's the weather in Seattle?");
            await page.PressAsync(inputSelector, "Enter");

            // Wait for response - increase timeout for model inference
            await page.WaitForTimeoutAsync(2000); // Initial delay for request processing

            // Find the response message
            var messagesSelector = "[data-testid='chat-message'], .message, [role='article']";
            await page.WaitForSelectorAsync(messagesSelector, new()
            {
                Timeout = TimeoutSeconds * 1000,
                State = WaitForSelectorState.Visible
            });

            // Get all message content
            var messages = await page.Locator(messagesSelector).AllTextContentsAsync();
            var lastMessage = messages.LastOrDefault() ?? string.Empty;

            // Assert - CRITICAL: Verify functools are NOT in the response
            Assert.NotNull(lastMessage);
            Assert.NotEmpty(lastMessage);

            // This assertion would have failed with the ChatOptionsBuilder bug
            Assert.DoesNotContain("functools[", lastMessage, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("\"name\":", lastMessage); // JSON function call structure
            Assert.DoesNotContain("GeocodeLocation", lastMessage); // Internal tool names
            Assert.DoesNotContain("GetWeather", lastMessage);
            Assert.DoesNotContain("GetCurrentWeather", lastMessage);

            // Positive assertion: Response should contain actual weather data
            var hasWeatherKeywords =
                lastMessage.Contains("temperature", StringComparison.OrdinalIgnoreCase) ||
                lastMessage.Contains("weather", StringComparison.OrdinalIgnoreCase) ||
                lastMessage.Contains("Seattle", StringComparison.OrdinalIgnoreCase) ||
                lastMessage.Contains("degrees", StringComparison.OrdinalIgnoreCase) ||
                lastMessage.Contains("forecast", StringComparison.OrdinalIgnoreCase);

            Assert.True(hasWeatherKeywords,
                $"Response should contain weather information. Got: {lastMessage}");
        }
        finally
        {
            await page.CloseAsync();
        }
    }

    [Fact(Skip = "Requires running web app - manual execution only")]
    public async Task ChatPage_LoadsSuccessfully()
    {
        // Arrange
        var page = await _browser!.NewPageAsync();

        try
        {
            // Act
            var response = await page.GotoAsync(BaseUrl);

            // Assert
            Assert.NotNull(response);
            Assert.True(response.Ok, $"Failed to load page: {response.Status} {response.StatusText}");

            // Verify critical UI elements are present
            await page.WaitForSelectorAsync("textarea[placeholder*='Ask']", new() { Timeout = 5000 });

            var title = await page.TitleAsync();
            Assert.Contains("Phi-4", title, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            await page.CloseAsync();
        }
    }

    [Fact(Skip = "Requires running web app - manual execution only")]
    public async Task ToolRegistration_LogsAppearInConsole()
    {
        // Arrange
        var page = await _browser!.NewPageAsync();
        var consoleMessages = new List<string>();

        page.Console += (_, msg) => consoleMessages.Add(msg.Text);

        try
        {
            // Act
            await page.GotoAsync(BaseUrl);
            await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
            await page.WaitForTimeoutAsync(1000); // Wait for tool discovery logs

            // Assert - Verify tool discovery logs appear
            var hasToolLogs = consoleMessages.Any(m =>
                m.Contains("[FOUNDRY]", StringComparison.OrdinalIgnoreCase) &&
                m.Contains("ToolDiscoveryService", StringComparison.OrdinalIgnoreCase));

            Assert.True(hasToolLogs,
                $"Expected tool discovery logs. Console messages: {string.Join(", ", consoleMessages)}");

            // Verify tools were registered
            var registeredToolsLog = consoleMessages.FirstOrDefault(m =>
                m.Contains("Registered", StringComparison.OrdinalIgnoreCase) &&
                m.Contains("tools", StringComparison.OrdinalIgnoreCase));

            Assert.NotNull(registeredToolsLog);
            Assert.Contains("GeocodeLocation", registeredToolsLog);
        }
        finally
        {
            await page.CloseAsync();
        }
    }
}
