import {
  expect,
  test,
} from '@playwright/test'

test(
  'user can register, log in, and log out',
  async ({ page }) => {
    const uniqueId =
      `${Date.now()}-${process.pid}`

    const email =
      `e2e-${uniqueId}@example.com`

    const password =
      'CartCompare123'

    await page.goto('/')

    // -------------------------
    // Register
    // -------------------------

    await expect(
      page.getByRole(
        'heading',
        {
          name: 'Login',
        }
      )
    ).toBeVisible()

    await page.getByRole(
      'button',
      {
        name:
          'Need an account?',
      }
    ).click()

    await expect(
      page.getByRole(
        'heading',
        {
          name:
            'Create Account',
        }
      )
    ).toBeVisible()

    await page.getByLabel(
      'First name'
    ).fill('Playwright')

    await page.getByLabel(
      'Last name'
    ).fill('Tester')

    await page.getByLabel(
      'Email'
    ).fill(email)

    await page.getByLabel(
      'Password'
    ).fill(password)

    await page.getByRole(
      'button',
      {
        name: 'Register',
      }
    ).click()

    await expect(
      page.getByText(
        'Registration succeeded. You can now log in.'
      )
    ).toBeVisible()

    // Registration returns the UI
    // to login mode.
    await expect(
      page.getByRole(
        'heading',
        {
          name: 'Login',
        }
      )
    ).toBeVisible()

    // -------------------------
    // Login
    // -------------------------

    await page.getByLabel(
      'Email'
    ).fill(email)

    await page.getByLabel(
      'Password'
    ).fill(password)

    await page.getByRole(
      'button',
      {
        name: 'Login',
      }
    ).click()

    await expect(
      page.getByText(
        'Logged in successfully.'
      )
    ).toBeVisible()

    await expect(
      page.getByText(
        'You are logged in.'
      )
    ).toBeVisible()

    await expect(
      page.getByRole(
        'heading',
        {
          name:
            'Grocery Items',
        }
      )
    ).toBeVisible()

    await expect(
      page.getByRole(
        'heading',
        {
          name:
            'My Grocery List',
        }
      )
    ).toBeVisible()

    await expect(
      page.getByRole(
        'heading',
        {
          name:
            'Choose Stores',
        }
      )
    ).toBeVisible()

    // -------------------------
    // Logout
    // -------------------------

    await page.getByRole(
      'button',
      {
        name: 'Logout',
      }
    ).click()

    await expect(
      page.getByText(
        'Logged out.'
      )
    ).toBeVisible()

    await expect(
      page.getByRole(
        'heading',
        {
          name: 'Login',
        }
      )
    ).toBeVisible()
  }
)