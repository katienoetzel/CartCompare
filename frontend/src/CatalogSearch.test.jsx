import {
  render,
  screen,
  within,
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
  vi,
} from 'vitest'

import CatalogSearch from './CatalogSearch'
import { server } from './test/server'

const API_BASE_URL =
  import.meta.env.VITE_API_BASE_URL

const doritos = {
  externalProductId: 'doritos',
  name: 'Doritos® Nacho Cheese Flavored Tortilla Chips',
  brand: 'Doritos',
  size: '9.25 oz',
}

function useSearchHandler(searchedUrls = [], failedRetailers = []) {
  server.use(
    http.get(`${API_BASE_URL}/api/catalog/products`, ({ request }) => {
      searchedUrls.push(new URL(request.url))

      return HttpResponse.json({
        retailers: [
          { retailerName: 'Lowes Foods', storeLocationId: 5, products: [] },
          { retailerName: 'Harris Teeter', storeLocationId: 122, products: [doritos] },
        ],
        failedRetailers,
      })
    })
  )
}

function useOptionsHandler(options) {
  server.use(
    http.get(`${API_BASE_URL}/api/catalog/products/doritos/options`, () =>
      HttpResponse.json({ product: doritos, ...options })
    )
  )
}

async function pickDoritos(user, onItemCreated = vi.fn()) {
  render(<CatalogSearch query=" doritos " onItemCreated={onItemCreated} />)
  await user.click(screen.getByRole('button', { name: 'Search stores for "doritos"' }))
  await user.click(await screen.findByRole('button', { name: 'Use this' }))
}

describe('CatalogSearch', () => {
  it(
    'searches every retailer without asking for a location',
    async () => {
      const searchedUrls = []
      useSearchHandler(searchedUrls, ['Kroger'])
      const user = userEvent.setup()

      render(<CatalogSearch query=" doritos " onItemCreated={vi.fn()} />)
      await user.click(screen.getByRole('button', { name: 'Search stores for "doritos"' }))

      expect(await screen.findByText('Harris Teeter · 9.25 oz')).toBeInTheDocument()
      expect(screen.getByText('Could not search: Kroger.')).toBeInTheDocument()
      expect(screen.queryByText(/\$/)).not.toBeInTheDocument()
      expect(searchedUrls).toHaveLength(1)
      expect(Object.fromEntries(searchedUrls[0].searchParams)).toEqual({ query: 'doritos' })
    }
  )

  it(
    'creates the chosen kind of item without any typed name',
    async () => {
      // Arrange
      let createBody = null
      useSearchHandler()
      useOptionsHandler({
        existingItems: [],
        newItems: [
          { kind: 'Branded', name: 'Nacho Cheese Flavored Tortilla Chips', brand: 'Doritos', size: '9.25 oz' },
          { kind: 'Generic', name: 'Nacho Cheese Flavored Tortilla Chips', brand: null, size: '9.25 oz' },
        ],
      })
      server.use(
        http.post(`${API_BASE_URL}/api/catalog/items`, async ({ request }) => {
          createBody = await request.json()
          return HttpResponse.json({
            item: { id: 11, name: 'Nacho Cheese Flavored Tortilla Chips', brand: 'Doritos', size: '9.25 oz' },
            created: true,
          })
        })
      )
      const onItemCreated = vi.fn()
      const user = userEvent.setup()

      // Act
      await pickDoritos(user, onItemCreated)
      await user.click(await screen.findByRole('button', { name: 'Add as Doritos' }))

      // Assert
      expect(screen.queryByRole('textbox')).not.toBeInTheDocument()
      expect(createBody).toEqual({ storeLocationId: 122, externalProductId: 'doritos', kind: 'Branded' })
      expect(onItemCreated).toHaveBeenCalledWith(expect.objectContaining({ id: 11 }), true)
    }
  )

  it(
    'offers the existing item a product already counts as',
    async () => {
      // Arrange
      useSearchHandler()
      useOptionsHandler({
        existingItems: [{ id: 7, name: 'Nacho Cheese Tortilla Chips', brand: null, size: '9.25 oz' }],
        newItems: [],
      })
      const onItemCreated = vi.fn()
      const user = userEvent.setup()

      // Act
      await pickDoritos(user, onItemCreated)
      const existing = await screen.findByRole('region', { name: 'Already in the catalog' })
      expect(within(existing).getByText('Any brand · 9.25 oz')).toBeInTheDocument()
      await user.click(within(existing).getByRole('button', { name: 'Add to my list' }))

      // Assert
      expect(screen.queryByRole('region', { name: 'Add a new item' })).not.toBeInTheDocument()
      expect(onItemCreated).toHaveBeenCalledWith(expect.objectContaining({ id: 7 }), false)
    }
  )

  it(
    'says when a product cannot be added automatically',
    async () => {
      useSearchHandler()
      useOptionsHandler({ existingItems: [], newItems: [] })
      const user = userEvent.setup()

      await pickDoritos(user)

      expect(
        await screen.findByText(/can't be added automatically/)
      ).toBeInTheDocument()
    }
  )
})
