import { defineConfig, devices } from "@playwright/test";

// Starts the real API (which also serves the built SPA from wwwroot) and drives it in Chromium.
export default defineConfig({
  testDir: "./tests",
  retries: process.env.CI ? 1 : 0,
  use: { baseURL: "http://localhost:5080", trace: "retain-on-failure" },
  projects: [{ name: "chromium", use: { ...devices["Desktop Chrome"] } }],
  webServer: {
    command: "dotnet run --project ../backend/FractionPlanner/FractionPlanner.csproj --urls http://localhost:5080",
    url: "http://localhost:5080/api/courses",
    reuseExistingServer: !process.env.CI,
    timeout: 180_000,
  },
});
