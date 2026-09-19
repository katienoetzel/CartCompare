import react from '@vitejs/plugin-react'
import { defineConfig } from 'vitest/config'

export default defineConfig({
  plugins: [react()],

  test: {
    environment: 'jsdom',
    setupFiles: './src/test/setup.js',

    // Vitest should only run our frontend unit/component tests.
    // Playwright owns everything in e2e/.
    include: [
      'src/**/*.test.{js,jsx}',
    ],
  },
})