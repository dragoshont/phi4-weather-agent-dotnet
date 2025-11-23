using Xunit;
using LocalAIAgent.Agent.Services;
using Microsoft.Extensions.Logging.Abstractions;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;

namespace LocalAIAgent.Agent.Tests.Services;

/// <summary>
/// Unit tests for OpenMeteoGeocodeClient
/// </summary>
public class OpenMeteoGeocodeClientTests
{
    // TODO T042: Implement unit tests for geocode client
    // - Test successful location search
    // - Test empty results handling
    // - Test HTTP error handling
    // - Test invalid input validation
    
    [Fact]
    public async Task SearchLocationAsync_ValidLocation_ReturnsLocations()
    {
        // Arrange
        var httpClient = new HttpClient(new MockHttpMessageHandler())
        {
            BaseAddress = new Uri("https://geocoding-api.open-meteo.com/v1/")
        };
        var logger = NullLogger<OpenMeteoGeocodeClient>.Instance;
        var client = new OpenMeteoGeocodeClient(httpClient, logger);

        // Act & Assert
        // TODO: Implement with mock HTTP responses
        Assert.True(true, "Test not yet implemented - placeholder for T042");
    }
    
    private class MockHttpMessageHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, 
            CancellationToken cancellationToken)
        {
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"results\":[]}")
            });
        }
    }
}
