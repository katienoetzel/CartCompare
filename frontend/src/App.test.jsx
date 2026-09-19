import {
  render,
  screen,
} from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import {
  http,
  HttpResponse,
} from 'msw'
import {
  describe,
  expect,
  it,
} from 'vitest'

import App from './App'
import { server } from './test/server'

const API_BASE_URL =
  import.meta.env.VITE_API_BASE_URL

function usePublicDataHandlers() {
  server.use(
    http.get(
      `${API_BASE_URL}/api/retailers`,
      () => {
        return HttpResponse.json([
          {
            id: 1,
            name: 'Kroger',
            supportsMembership: true,
            isActive: true,
          },
        ])
      }
    ),

    http.get(
      `${API_BASE_URL}/api/items`,
      () => {
        return HttpResponse.json([
          {
            id: 10,
            name: 'Whole Milk',
            brand: 'Test Brand',
            size: '1 gallon',
            category: 'Dairy',
            isActive: true,
          },
        ])
      }
    )
  )
}

describe('App authentication', () => {
  it(
    'logs in and loads the authenticated grocery list',
    async () => {
      // Arrange
      usePublicDataHandlers()

      server.use(
        http.post(
          `${API_BASE_URL}/api/auth/login`,
          async ({ request }) => {
            const body =
              await request.json()

            if (
              body.email !==
                'katie@example.com' ||
              body.password !==
                'Password123!'
            ) {
              return HttpResponse.json(
                {
                  message:
                    'Invalid email or password.',
                },
                {
                  status: 401,
                }
              )
            }

            return HttpResponse.json({
              token:
                'fake-integration-jwt',
              expiresAt:
                '2099-01-01T00:00:00Z',
              userId: 1,
              email:
                'katie@example.com',
              firstName: 'Katie',
              lastName: 'Tester',
            })
          }
        ),

        http.get(
          `${API_BASE_URL}/api/grocery-list`,
          ({ request }) => {
            expect(
              request.headers.get(
                'Authorization'
              )
            ).toBe(
              'Bearer fake-integration-jwt'
            )

            return HttpResponse.json([
              {
                id: 100,
                itemId: 10,
                name: 'Whole Milk',
                brand: 'Test Brand',
                size: '1 gallon',
                category: 'Dairy',
                quantity: 2,
              },
            ])
          }
        )
      )

      const user =
        userEvent.setup()

      render(<App />)

      // Act
      await user.type(
        screen.getByLabelText(
          'Email'
        ),
        'katie@example.com'
      )

      await user.type(
        screen.getByLabelText(
          'Password'
        ),
        'Password123!'
      )

      await user.click(
        screen.getByRole(
          'button',
          {
            name: 'Login',
          }
        )
      )

      // Assert
      expect(
        await screen.findByText(
          'Logged in successfully.'
        )
      ).toBeInTheDocument()

      expect(
        screen.getByText(
          'You are logged in.'
        )
      ).toBeInTheDocument()

      expect(
        await screen.findByText(
          /Quantity:\s*2/
        )
      ).toBeInTheDocument()

      expect(
        screen.getByRole(
          'option',
          {
            name: 'Kroger',
          }
        )
      ).toBeInTheDocument()

      expect(
        sessionStorage.getItem(
          'cartcompare_token'
        )
      ).toBe(
        'fake-integration-jwt'
      )
    }
  )

  it(
    'shows the API error when login fails',
    async () => {
      // Arrange
      usePublicDataHandlers()

      server.use(
        http.post(
          `${API_BASE_URL}/api/auth/login`,
          () => {
            return HttpResponse.json(
              {
                message:
                  'Invalid email or password.',
              },
              {
                status: 401,
              }
            )
          }
        )
      )

      const user =
        userEvent.setup()

      render(<App />)

      // Act
      await user.type(
        screen.getByLabelText(
          'Email'
        ),
        'wrong@example.com'
      )

      await user.type(
        screen.getByLabelText(
          'Password'
        ),
        'wrong-password'
      )

      await user.click(
        screen.getByRole(
          'button',
          {
            name: 'Login',
          }
        )
      )

      // Assert
      expect(
        await screen.findByText(
          'Error: Invalid email or password.'
        )
      ).toBeInTheDocument()

      expect(
        screen.getByRole(
          'heading',
          {
            name: 'Login',
          }
        )
      ).toBeInTheDocument()

      expect(
        sessionStorage.getItem(
          'cartcompare_token'
        )
      ).toBeNull()
    }
  )

  it(
    'logs out and removes the stored token',
    async () => {
      // Arrange
      sessionStorage.setItem(
        'cartcompare_token',
        'existing-token'
      )

      usePublicDataHandlers()

      server.use(
        http.get(
          `${API_BASE_URL}/api/grocery-list`,
          () => {
            return HttpResponse.json([])
          }
        )
      )

      const user =
        userEvent.setup()

      render(<App />)

      expect(
        await screen.findByText(
          'You are logged in.'
        )
      ).toBeInTheDocument()

      // Act
      await user.click(
        screen.getByRole(
          'button',
          {
            name: 'Logout',
          }
        )
      )

      // Assert
      expect(
        screen.getByText(
          'Logged out.'
        )
      ).toBeInTheDocument()

      expect(
        screen.getByRole(
          'heading',
          {
            name: 'Login',
          }
        )
      ).toBeInTheDocument()

      expect(
        sessionStorage.getItem(
          'cartcompare_token'
        )
      ).toBeNull()
    }
  )
})