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

const items = [
  { id: 1, name: 'Whole Milk', brand: null, size: '1 gallon', category: 'Dairy', isActive: true },
  { id: 2, name: 'Ground Coffee', brand: null, size: '12 oz', category: 'Beverages', isActive: true },
  { id: 3, name: 'Salted Butter', brand: null, size: '1 lb', category: 'Dairy', isActive: true },
]

describe('App item search', () => {
  beforeEach(() => {
    sessionStorage.setItem(
      'cartcompare_token',
      'existing-token'
    )

    server.use(
      http.get(`${API_BASE_URL}/api/retailers`, () => HttpResponse.json([])),
      http.get(`${API_BASE_URL}/api/items`, () => HttpResponse.json(items)),
      http.get(`${API_BASE_URL}/api/grocery-list`, () => HttpResponse.json([]))
    )
  })

  it(
    'filters grocery items by name and category words',
    async () => {
      // Arrange
      const user =
        userEvent.setup()

      render(<App />)

      const search =
        await screen.findByRole(
          'searchbox',
          { name: 'Search items' }
        )

      // Act
      await user.type(search, 'dairy butter')

      // Assert
      expect(
        screen.getByRole('heading', { name: 'Salted Butter' })
      ).toBeInTheDocument()
      expect(
        screen.queryByRole('heading', { name: 'Whole Milk' })
      ).not.toBeInTheDocument()
      expect(
        screen.queryByRole('heading', { name: 'Ground Coffee' })
      ).not.toBeInTheDocument()
    }
  )

  it(
    'says when no item matches the search',
    async () => {
      // Arrange
      const user =
        userEvent.setup()

      render(<App />)

      // Act
      await user.type(
        await screen.findByRole('searchbox', { name: 'Search items' }),
        'tofu'
      )

      // Assert
      expect(
        screen.getByText('No items match "tofu".')
      ).toBeInTheDocument()
    }
  )
})
