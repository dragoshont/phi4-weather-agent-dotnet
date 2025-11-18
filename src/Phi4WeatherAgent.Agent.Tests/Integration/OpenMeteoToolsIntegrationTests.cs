using FluentAssertions;
using Phi4WeatherAgent.Tools;

namespace Phi4WeatherAgent.Agent.Tests.Integration;

/// <summary>
/// Integration tests for Open-Meteo API tools.
/// Tests real HTTP calls with proper error handling and data validation.
/// </summary>
public class OpenMeteoToolsIntegrationTests
{
    [Fact]
    public async Task GetWeather_ValidCoordinates_ReturnsCurrentWeather()
    {
        // Arrange
        var tools = new WeatherTools();
        var latitude = 47.6062; // Seattle
        var longitude = -122.3321;

        // Act
        var result = await Task.Run(() => tools.GetWeather(latitude, longitude));

        // Assert
        result.Should().NotBeNull();
        result.Should().Contain("temperature");
        result.Should().Contain("weather_description");
        // Should NOT contain error indicators
        result.Should().NotContain("error");
        result.Should().NotContain("failed");
    }

    [Fact]
    public async Task GetForecast_ValidCoordinates_ReturnsForecastArray()
    {
        // Arrange
        var tools = new WeatherTools();
        var latitude = 47.6062;
        var longitude = -122.3321;

        // Act
        var result = await Task.Run(() => tools.GetForecast(latitude, longitude, 7));

        // Assert
        result.Should().NotBeNull();
        result.Should().Contain("forecast");
        result.Should().Contain("temperature_max");
        result.Should().Contain("temperature_min");
        // Should have 7 days of forecast
        var forecastCount = System.Text.RegularExpressions.Regex.Matches(result, "\"date\"").Count;
        forecastCount.Should().Be(7);
    }

    [Fact]
    public async Task GeocodeLocation_ValidCity_ReturnsCoordinates()
    {
        // Arrange
        var tools = new GeocodingTools();

        // Act
        var result = await Task.Run(() => tools.GeocodeLocation("Seattle", 5, "en"));

        // Assert
        result.Should().NotBeNull();
        result.Should().Contain("results");
        result.Should().Contain("latitude");
        result.Should().Contain("longitude");
        result.Should().Contain("Seattle");
    }

    [Fact]
    public async Task GeocodeLocation_NonExistentCity_ReturnsNoResults()
    {
        // Arrange
        var tools = new GeocodingTools();

        // Act
        var result = await Task.Run(() => tools.GeocodeLocation("Xyzabc123NonExistent", 5, "en"));

        // Assert
        result.Should().Contain("No results found");
    }

    [Fact]
    public async Task GetAirQuality_ValidCoordinates_ReturnsAQIData()
    {
        // Arrange
        var tools = new AirQualityTools();
        var latitude = 47.6062; // Seattle
        var longitude = -122.3321;

        // Act
        var result = await Task.Run(() => tools.GetAirQuality(latitude, longitude));

        // Assert
        result.Should().NotBeNull();
        result.Should().Contain("us_aqi");
        result.Should().Contain("european_aqi");
        result.Should().Contain("pm2_5");
        result.Should().Contain("pm10");
    }

    [Fact]
    public async Task GetPollenForecast_EuropeanCoordinates_ReturnsPollenData()
    {
        // Arrange
        var tools = new AirQualityTools();
        var latitude = 52.5200; // Berlin, Germany (Europe)
        var longitude = 13.4050;

        // Act
        var result = await Task.Run(() => tools.GetPollenForecast(latitude, longitude, 4));

        // Assert
        result.Should().NotBeNull();
        result.Should().Contain("pollen_forecast");
        // Should have pollen types
        (result.Contains("alder") || result.Contains("birch") || result.Contains("grass")).Should().BeTrue();
    }

    [Fact]
    public async Task GetPollenForecast_NonEuropeanCoordinates_ReturnsError()
    {
        // Arrange
        var tools = new AirQualityTools();
        var latitude = 47.6062; // Seattle (not Europe)
        var longitude = -122.3321;

        // Act
        var result = await Task.Run(() => tools.GetPollenForecast(latitude, longitude, 4));

        // Assert
        result.Should().Contain("only available for Europe");
    }

    [Fact]
    public async Task GetWeather_InvalidCoordinates_HandlesGracefully()
    {
        // Arrange
        var tools = new WeatherTools();
        var latitude = 999.0; // Invalid latitude (must be -90 to 90)
        var longitude = 0.0;

        // Act & Assert - Should throw or return error, not crash
        try
        {
            var result = await Task.Run(() => tools.GetWeather(latitude, longitude));
            result.Should().Contain("error", "API should reject invalid coordinates");
        }
        catch (Exception ex)
        {
            ex.Should().NotBeOfType<NullReferenceException>("Should handle API errors gracefully");
        }
    }

    [Fact(Skip = "Rate limiting test - run manually to avoid hitting API limits")]
    public async Task RateLimiting_MultipleRequests_RespectsLimits()
    {
        // Open-Meteo free tier: 60 requests/minute
        // This test verifies we don't exceed limits

        // Arrange
        var tools = new WeatherTools();
        var tasks = Enumerable.Range(0, 70).Select(i =>
            Task.Run(() => tools.GetWeather(47.6 + i * 0.01, -122.3))
        );

        // Act & Assert - Should complete without 429 errors
        var results = await Task.WhenAll(tasks);
        results.Should().OnlyContain(r => !r.Contains("429") && !r.Contains("rate limit"));
    }
}
