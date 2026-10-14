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
  vi,
} from 'vitest'

import CatalogSearch from './CatalogSearch'
import { server } from './test/server'

const API_BASE_URL =
  import.meta.env.VITE_API_BASE_URL

const stores = [
  { id: 5, retailerId: 2, retailerName: 'Lowes Foods', name: 'Brier Creek' },
  { id: 6, retailerId: 2, retailerName: 'Lowes Foods', name: 'Strickland' },
  { id: 122, retailerId: 3, retailerName: 'Harris Teeter', name: 'Corners' },
]

describe('CatalogSearch', () => {
  it(
    'asks for nearby stores before it can search their products',
    () => {
      render(<CatalogSearch query="peanut butter" stores={[]} onItemCreated={vi.fn()} />)

      expect(
        screen.getByText(/Find nearby stores in Step 2/)
      ).toBeInTheDocument()
    }
  )

  it(
    'searches one store per retailer and creates an item from the picked product',
    async () => {
      // Arrange
      const searchedStores = []
      let createBody = null

      server.use(
        http.get(`${API_BASE_URL}/api/catalog/products`, ({ request }) => {
          const url = new URL(request.url)
          const storeLocationId = Number(url.searchParams.get('storeLocationId'))
          searchedStores.push(storeLocationId)

          return HttpResponse.json({
            storeLocationId,
            retailerName: storeLocationId === 5 ? 'Lowes Foods' : 'Harris Teeter',
            products: storeLocationId === 5
              ? [{
                  externalProductId: 'pb',
                  name: 'Lowes Foods Creamy Peanut Butter 16 oz',
                  brand: null,
                  size: '16 oz',
                  shelfPrice: 2.99,
                  suggestedName: 'Creamy Peanut Butter',
                }]
              : [],
          })
        }),
        http.post(`${API_BASE_URL}/api/catalog/items`, async ({ request }) => {
          createBody = await request.json()

          return HttpResponse.json({
            item: { id: 11, name: 'Creamy Peanut Butter', size: '16 oz', category: 'Pantry' },
            created: true,
          })
        })
      )

      const onItemCreated = vi.fn()
      const user = userEvent.setup()

      render(<CatalogSearch query=" peanut butter " stores={stores} onItemCreated={onItemCreated} />)

      // Act
      await user.click(screen.getByRole('button', { name: 'Search stores for "peanut butter"' }))
      await user.click(await screen.findByRole('button', { name: 'Use this' }))
      await user.type(screen.getByLabelText('Category (optional)'), 'Pantry')
      await user.click(screen.getByRole('button', { name: 'Add item to my list' }))

      // Assert
      expect(searchedStores.sort((a, b) => a - b)).toEqual([5, 122])
      expect(createBody).toEqual({
        storeLocationId: 5,
        externalProductId: 'pb',
        name: 'Creamy Peanut Butter',
        size: '16 oz',
        category: 'Pantry',
      })
      expect(onItemCreated).toHaveBeenCalledWith(
        expect.objectContaining({ id: 11 }),
        true
      )
    }
  )

  it(
    'shows why the server refused an edited name',
    async () => {
      // Arrange
      server.use(
        http.get(`${API_BASE_URL}/api/catalog/products`, () =>
          HttpResponse.json({
            retailerName: 'Lowes Foods',
            products: [{
              externalProductId: 'pb',
              name: 'Creamy Peanut Butter',
              size: '16 oz',
              shelfPrice: null,
              suggestedName: 'Creamy Peanut Butter',
            }],
          })
        ),
        http.post(`${API_BASE_URL}/api/catalog/items`, () =>
          HttpResponse.json(
            { message: 'The name and size must still describe the product you picked.' },
            { status: 400 }
          )
        )
      )

      const user = userEvent.setup()
      render(<CatalogSearch query="peanut butter" stores={[stores[0]]} onItemCreated={vi.fn()} />)

      // Act
      await user.click(screen.getByRole('button', { name: /Search stores/ }))
      await user.click(await screen.findByRole('button', { name: 'Use this' }))
      await user.clear(screen.getByLabelText('Item name'))
      await user.type(screen.getByLabelText('Item name'), 'Crunchy Peanut Butter')
      await user.click(screen.getByRole('button', { name: 'Add item to my list' }))

      // Assert
      expect(
        await screen.findByRole('alert')
      ).toHaveTextContent('must still describe the product you picked')
    }
  )
})
