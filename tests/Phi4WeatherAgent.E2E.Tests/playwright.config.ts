import { defineConfig, devices } from '@playwright/test';

/**
 * Playwright configuration for Phi-4 Weather Assistant E2E tests.
 * 
 * Test Scenarios:
 * - T045: Basic weather query ("What's the weather in Seattle?")
 * - T053: Allergen advisory ("What are the pollen levels in Austin?")
 * - T059: Weekend planner ("Plan my weekend in Denver")
 * - T065: Keyboard + screen reader workflow (accessibility)
 * 
 * Target: AppHost orchestrated Blazor Server app (default: http://localhost:5000)
 */
export default defineConfig({
  testDir: './',
  
  /* Run tests in files in parallel */
  fullyParallel: true,
  
  /* Fail the build on CI if you accidentally left test.only in the source code */
  forbidOnly: !!process.env.CI,
  
  /* Retry on CI only */
  retries: process.env.CI ? 2 : 0,
  
  /* Opt out of parallel tests on CI */
  workers: process.env.CI ? 1 : undefined,
  
  /* Reporter to use */
  reporter: process.env.CI ? 'github' : 'html',
  
  /* Shared settings for all the projects below */
  use: {
    /* Base URL for the Aspire-orchestrated app (configure via env or use default) */
    baseURL: process.env.BASE_URL || 'http://localhost:5000',
    
    /* Collect trace on failure for debugging */
    trace: 'on-first-retry',
    
    /* Screenshot on failure */
    screenshot: 'only-on-failure',
    
    /* Video on failure */
    video: 'retain-on-failure',
    
    /* Default timeout for each action */
    actionTimeout: 30000,
  },

  /* Configure projects for major browsers */
  projects: [
    {
      name: 'chromium',
      use: { 
        ...devices['Desktop Chrome'],
        viewport: { width: 1920, height: 1080 },
      },
    },

    {
      name: 'firefox',
      use: { 
        ...devices['Desktop Firefox'],
        viewport: { width: 1920, height: 1080 },
      },
    },

    {
      name: 'webkit',
      use: { 
        ...devices['Desktop Safari'],
        viewport: { width: 1920, height: 1080 },
      },
    },

    /* Test against mobile viewports (optional for weather assistant) */
    // {
    //   name: 'Mobile Chrome',
    //   use: { ...devices['Pixel 5'] },
    // },
    // {
    //   name: 'Mobile Safari',
    //   use: { ...devices['iPhone 12'] },
    // },
  ],

  /* Run local Aspire AppHost before starting the tests */
  webServer: {
    command: 'dotnet run --project ../../../src/Phi4WeatherAgent.AppHost/Phi4WeatherAgent.AppHost.csproj',
    url: 'http://localhost:5000',
    reuseExistingServer: !process.env.CI,
    stdout: 'pipe',
    stderr: 'pipe',
    timeout: 120000, // 2 minutes for Aspire to start (includes model container pull)
  },
});
