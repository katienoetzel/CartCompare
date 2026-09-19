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
  beforeEach,
  describe,
  expect,
  it,
} from 'vitest'

import App from './App'
import { server } from './test/server'

const API_BASE_URL =
  import.meta.env.VITE_API_BASE_URL

const testItem = {
  id: 10,
  name: 'Whole Milk',
  brand: 'Test Brand',
  size: '1 gallon',
  category: 'Dairy',
  isActive: true,
}

const testListItem = {
  id: 100,
  itemId: 10,
  name: 'Whole Milk',
  brand: 'Test Brand',
  size: '1 gallon',
  category: 'Dairy',
  quantity: 3,
}

function useBaseHandlers(
  groceryListHandler
) {
  server.use(
    http.get(
      `${API_BASE_URL}/api/retailers`,
      () => {
        return HttpResponse.json([])
      }
    ),

    http.get(
      `${API_BASE_URL}/api/items`,
      () => {
        return HttpResponse.json([
          testItem,
        ])
      }
    ),

    http.get(
      `${API_BASE_URL}/api/grocery-list`,
      groceryListHandler
    )
  )
}

describe('App grocery list', () => {
  beforeEach(() => {
    sessionStorage.setItem(
      'cartcompare_token',
      'existing-token'
    )
  })

  it(
    'rejects a quantity that is not a positive whole number',
    async () => {
      // Arrange
      let putWasCalled = false

      useBaseHandlers(() => {
        return HttpResponse.json([])
      })

      server.use(
        http.put(
          `${API_BASE_URL}/api/grocery-list/10`,
          () => {
            putWasCalled = true

            return HttpResponse.json({})
          }
        )
      )

      const user =
        userEvent.setup()

      render(<App />)

      await screen.findByText(
        'Whole Milk'
      )

      const quantityInput =
        screen.getByLabelText(
          'Quantity'
        )

      // Act
      await user.clear(
        quantityInput
      )

      await user.type(
        quantityInput,
        '0'
      )

      await user.click(
        screen.getByRole(
          'button',
          {
            name:
              'Add / Update',
          }
        )
      )

      // Assert
      expect(
        screen.getByText(
          'Error: Quantity must be a positive whole number.'
        )
      ).toBeInTheDocument()

      expect(
        putWasCalled
      ).toBe(false)
    }
  )

  it(
    'adds an item and refreshes the grocery list',
    async () => {
      // Arrange
      let groceryListRequestCount =
        0

      useBaseHandlers(() => {
        groceryListRequestCount += 1

        if (
          groceryListRequestCount ===
          1
        ) {
          return HttpResponse.json(
            []
          )
        }

        return HttpResponse.json([
          testListItem,
        ])
      })

      server.use(
        http.put(
          `${API_BASE_URL}/api/grocery-list/10`,
          async ({ request }) => {
            expect(
              request.headers.get(
                'Authorization'
              )
            ).toBe(
              'Bearer existing-token'
            )

            const body =
              await request.json()

            expect(body).toEqual({
              quantity: 3,
            })

            return HttpResponse.json({
              itemId: 10,
              quantity: 3,
            })
          }
        )
      )

      const user =
        userEvent.setup()

      render(<App />)

      await screen.findByText(
        'Whole Milk'
      )

      const quantityInput =
        screen.getByLabelText(
          'Quantity'
        )

      // Act
      await user.clear(
        quantityInput
      )

      await user.type(
        quantityInput,
        '3'
      )

      await user.click(
        screen.getByRole(
          'button',
          {
            name:
              'Add / Update',
          }
        )
      )

      // Assert
      expect(
        await screen.findByText(
          'Grocery list updated.'
        )
      ).toBeInTheDocument()

      expect(
        await screen.findByText(
          /Quantity:\s*3/
        )
      ).toBeInTheDocument()

      expect(
        groceryListRequestCount
      ).toBe(2)
    }
  )

  it(
    'removes an item and refreshes the grocery list',
    async () => {
      // Arrange
      let groceryListRequestCount =
        0

      let deleteWasCalled =
        false

      useBaseHandlers(() => {
        groceryListRequestCount += 1

        if (
          groceryListRequestCount ===
          1
        ) {
          return HttpResponse.json([
            testListItem,
          ])
        }

        return HttpResponse.json([])
      })

      server.use(
        http.delete(
          `${API_BASE_URL}/api/grocery-list/10`,
          ({ request }) => {
            deleteWasCalled =
              true

            expect(
              request.headers.get(
                'Authorization'
              )
            ).toBe(
              'Bearer existing-token'
            )

            return new HttpResponse(
              null,
              {
                status: 204,
              }
            )
          }
        )
      )

      const user =
        userEvent.setup()

      render(<App />)

      const removeButton =
        await screen.findByRole(
          'button',
          {
            name: 'Remove',
          }
        )

      // Act
      await user.click(
        removeButton
      )

      // Assert
      expect(
        await screen.findByText(
          'Item removed from grocery list.'
        )
      ).toBeInTheDocument()

      expect(
        screen.getByText(
          'Your grocery list is empty.'
        )
      ).toBeInTheDocument()

      expect(
        deleteWasCalled
      ).toBe(true)

      expect(
        groceryListRequestCount
      ).toBe(2)
    }
  )
})