import {
  defineConfig,
  devices,
} from '@playwright/test'

export default defineConfig({
  testDir: './e2e',

  fullyParallel: false,
  retries: process.env.CI ? 2 : 0,
  workers: 1,

  reporter: 'list',

  use: {
    baseURL:
      'http://localhost:5173',

    trace:
      'on-first-retry',

    screenshot:
      'only-on-failure',
  },

  projects: [
    {
      name: 'chromium',
      use: {
        ...devices[
          'Desktop Chrome'
        ],
      },
    },
  ],

  webServer: [
    {
      command:
        'dotnet run --project ../backend/src/CartCompare.Api/CartCompare.Api.csproj --launch-profile http --no-build',

      url:
        'http://localhost:5055/openapi/v1.json',

      timeout:
        120_000,

      reuseExistingServer:
        false,

      env: {
        ...process.env,

        E2E__Enabled:
          'true',

        ASPNETCORE_ENVIRONMENT:
          'Development',
      },
    },

    {
      command:
        'npm run dev -- --host localhost',

      url:
        'http://localhost:5173',

      timeout:
        120_000,

      reuseExistingServer:
        false,

      env: {
        ...process.env,

        VITE_API_BASE_URL:
          'http://localhost:5055',
      },
    },
  ],
})