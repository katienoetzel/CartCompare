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
  beforeEach,
  describe,
  expect,
  it,
} from 'vitest'

import App from './App'
import { server } from './test/server'

const API_BASE_URL =
  import.meta.env.VITE_API_BASE_URL

const groceryList = [
  {
    id: 100,
    itemId: 10,
    name: 'Whole Milk',
    brand: 'Test Brand',
    size: '1 gallon',
    category: 'Dairy',
    quantity: 2,
  },
]

function useBaseHandlers() {
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
    ),

    http.get(
      `${API_BASE_URL}/api/grocery-list`,
      () => {
        return HttpResponse.json(
          groceryList
        )
      }
    ),

    http.post(
      `${API_BASE_URL}/api/store-locations/search`,
      async ({ request }) => {
        expect(
          request.headers.get(
            'Authorization'
          )
        ).toBe(
          'Bearer existing-token'
        )

        expect(await request.json()).toEqual({ postalCode: '27613' })
        return HttpResponse.json({ postalCode: '27613', failedRetailers: [], stores: [
          {
            id: 101,
            retailerId: 1,
            name: 'Kroger Downtown',
            addressLine1:
              '100 Main Street',
            city: 'Raleigh',
            state: 'NC',
            postalCode: '27601',
          },
          {
            id: 102,
            retailerId: 1,
            name: 'Kroger North',
            addressLine1:
              '200 North Street',
            city: 'Raleigh',
            state: 'NC',
            postalCode: '27609',
          },
        ] })
      }
    )
  )
}

describe(
  'App store comparison',
  () => {
    beforeEach(() => {
      sessionStorage.setItem(
        'cartcompare_token',
        'existing-token'
      )

      useBaseHandlers()
    })

    it(
      'loads nearby stores after a ZIP code search',
      async () => {
        // Arrange
        const user =
          userEvent.setup()

        render(<App />)


        expect(
          screen.getByText(
            'Selected stores: 0'
          )
        ).toBeInTheDocument()

        expect(
          screen.getByRole(
            'button',
            {
              name:
                'Compare Stores',
            }
          )
        ).toBeDisabled()

        // Act
        await user.type(screen.getByRole('textbox', { name: 'ZIP code' }), '27613')
        await user.click(screen.getByRole('button', { name: 'Find Stores' }))

        // Assert
        expect(
          await screen.findByRole(
            'checkbox',
            {
              name:
                /Kroger Downtown/,
            }
          )
        ).toBeInTheDocument()

        expect(
          screen.getByRole(
            'checkbox',
            {
              name:
                /Kroger North/,
            }
          )
        ).toBeInTheDocument()
      }
    )

    it('shows per-lb items by the pound in the list and the results', async () => {
      server.use(
        http.get(`${API_BASE_URL}/api/items`, () =>
          HttpResponse.json([{ id: 3, name: 'Bananas', size: 'per lb', category: 'Produce', isActive: true }])
        ),
        http.get(`${API_BASE_URL}/api/grocery-list`, () =>
          HttpResponse.json([{ id: 200, itemId: 3, name: 'Bananas', size: 'per lb', quantity: 3 }])
        ),
        http.post(`${API_BASE_URL}/api/comparisons/stores`, () =>
          HttpResponse.json({
            completeStoreCount: 1,
            incompleteStoreCount: 0,
            missingStoreLocationIds: [],
            stores: [{
              storeLocationId: 101, storeName: 'Kroger Downtown', rank: 1, knownSubtotal: 1.95,
              isComplete: true, missingItemCount: 0,
              items: [{ itemId: 3, itemName: 'Bananas', quantity: 3, isAvailable: true, unitPrice: 0.65, lineTotal: 1.95 }],
            }],
          })
        )
      )
      const user = userEvent.setup()
      render(<App />)

      expect(await screen.findByText('per lb · Quantity: 3 lb')).toBeInTheDocument()

      await user.type(screen.getByRole('textbox', { name: 'ZIP code' }), '27613')
      await user.click(screen.getByRole('button', { name: 'Find Stores' }))
      await user.click(await screen.findByRole('checkbox', { name: /Kroger Downtown/ }))
      await user.click(screen.getByRole('button', { name: 'Compare Stores' }))

      expect(await screen.findByText('Bananas — 3 lb — $0.65/lb — $1.95 total')).toBeInTheDocument()
    })

    it('stops at 10 selected stores, the API limit', async () => {
      server.use(http.post(`${API_BASE_URL}/api/store-locations/search`, () =>
        HttpResponse.json({
          postalCode: '27613',
          failedRetailers: [],
          stores: Array.from({ length: 11 }, (_, index) => ({
            id: 500 + index, retailerId: 1, retailerName: 'Kroger', name: `Store ${index + 1}`,
          })),
        })
      ))
      const user = userEvent.setup()
      render(<App />)
      await user.type(screen.getByRole('textbox', { name: 'ZIP code' }), '27613')
      await user.click(screen.getByRole('button', { name: 'Find Stores' }))
      await screen.findByRole('checkbox', { name: /Store 11/ })

      for (let index = 1; index <= 11; index++) {
        await user.click(screen.getByRole('checkbox', { name: new RegExp(`Store ${index}\\b`) }))
      }

      expect(screen.getByText('Selected stores: 10')).toBeInTheDocument()
      expect(screen.getByText(/You can compare up to 10 stores at a time./)).toBeInTheDocument()
      expect(screen.getByRole('checkbox', { name: /Store 11/ })).not.toBeChecked()
    })

    it('fills the suggested ZIP when Tab is pressed in the empty box', async () => {
      const user = userEvent.setup()
      render(<App />)
      const zip = screen.getByRole('textbox', { name: 'ZIP code' })

      await user.click(zip)
      await user.tab()

      expect(zip).toHaveValue('27613')
      expect(zip).toHaveFocus()
      expect(screen.queryByText('Tab to use 27613')).not.toBeInTheDocument()

      // With text in the box, Tab moves on as usual.
      await user.tab()
      expect(zip).not.toHaveFocus()
    })

    it('keeps returned stores when another retailer fails', async () => {
      server.use(http.post(`${API_BASE_URL}/api/store-locations/search`, () =>
        HttpResponse.json({
          postalCode: '27613',
          stores: [{ id: 101, retailerId: 1, retailerName: 'Kroger', name: 'Kroger Downtown' }],
          failedRetailers: [{ id: 2, name: 'Lowes Foods' }],
        })
      ))
      const user = userEvent.setup()
      render(<App />)
      await user.type(screen.getByRole('textbox', { name: 'ZIP code' }), '27613')
      await user.click(screen.getByRole('button', { name: 'Find Stores' }))
      expect(await screen.findByRole('checkbox', { name: /Kroger Downtown/ })).toBeInTheDocument()
      expect(screen.getByText('Could not update stores for: Lowes Foods.')).toBeInTheDocument()
    })

    it(
      'compares selected stores and renders complete and incomplete results',
      async () => {
        // Arrange
        server.use(
          http.post(
            `${API_BASE_URL}/api/comparisons/stores`,
            async ({
              request,
            }) => {
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
                storeLocationIds: [
                  101,
                  102,
                ],
              })

              return HttpResponse.json({
                completeStoreCount: 1,
                incompleteStoreCount: 1,
                missingStoreLocationIds:
                  [],
                stores: [
                  {
                    storeLocationId:
                      101,
                    storeName:
                      'Kroger Downtown',
                    rank: 1,
                    knownSubtotal:
                      7.5,
                    isComplete: true,
                    missingItemCount:
                      0,
                    items: [
                      {
                        itemId: 10,
                        itemName:
                          'Whole Milk',
                        quantity: 2,
                        isAvailable:
                          true,
                        unitPrice:
                          3.75,
                        lineTotal:
                          7.5,
                      },
                    ],
                  },
                  {
                    storeLocationId:
                      102,
                    storeName:
                      'Kroger North',
                    rank: null,
                    knownSubtotal:
                      0,
                    isComplete:
                      false,
                    missingItemCount:
                      1,
                    items: [
                      {
                        itemId: 10,
                        itemName:
                          'Whole Milk',
                        quantity: 2,
                        isAvailable:
                          false,
                        priceStatus: 'ambiguous_match',
                        priceMessage: 'Several products could match; a product selection is needed.',
                        unitPrice: null,
                        lineTotal: null,
                      },
                    ],
                  },
                ],
              })
            }
          )
        )

        const user =
          userEvent.setup()

        render(<App />)


        await user.type(screen.getByRole('textbox', { name: 'ZIP code' }), '27613')
        await user.click(screen.getByRole('button', { name: 'Find Stores' }))

        const downtownCheckbox =
          await screen.findByRole(
            'checkbox',
            {
              name:
                /Kroger Downtown/,
            }
          )

        const northCheckbox =
          screen.getByRole(
            'checkbox',
            {
              name:
                /Kroger North/,
            }
          )

        // Act
        await user.click(
          downtownCheckbox
        )

        await user.click(
          northCheckbox
        )

        expect(
          screen.getByText(
            'Selected stores: 2'
          )
        ).toBeInTheDocument()

        const compareButton =
          screen.getByRole(
            'button',
            {
              name:
                'Compare Stores',
            }
          )

        expect(
          compareButton
        ).toBeEnabled()

        await user.click(
          compareButton
        )

        // Assert
        expect(
          await screen.findByRole(
            'heading',
            {
              name:
                'Comparison Results',
            }
          )
        ).toBeInTheDocument()

        expect(
          screen.getByText(
            'Complete stores: 1'
          )
        ).toBeInTheDocument()

        expect(
          screen.getByText(
            'Incomplete stores: 1'
          )
        ).toBeInTheDocument()

        expect(screen.getByText(
          'Several products could match; a product selection is needed.'
        )).toBeInTheDocument()

        const completeHeading =
          screen.getByRole(
            'heading',
            {
              name:
                '#1 Kroger Downtown',
            }
          )

        const incompleteHeading =
          screen.getByRole(
            'heading',
            {
              name:
                'Kroger North',
            }
          )

        expect(
          completeHeading
        ).toBeInTheDocument()

        expect(
          incompleteHeading
        ).toBeInTheDocument()

        const completeCard =
          completeHeading.closest(
            'article'
          )

        const incompleteCard =
          incompleteHeading.closest(
            'article'
          )

        expect(
          completeCard
        ).not.toBeNull()

        expect(
          incompleteCard
        ).not.toBeNull()

        expect(
          within(
            completeCard
          ).getByText(
            'Status: Complete'
          )
        ).toBeInTheDocument()

        expect(
          within(
            incompleteCard
          ).getByText(
            'Status: Incomplete'
          )
        ).toBeInTheDocument()

        expect(
          within(
            incompleteCard
          ).getByText(
            'Missing items: 1'
          )
        ).toBeInTheDocument()

        expect(
          within(
            incompleteCard
          ).getByText(
            'Price unavailable'
          )
        ).toBeInTheDocument()
      }
    )

    it(
      'shows an API error when comparison fails',
      async () => {
        // Arrange
        server.use(
          http.post(
            `${API_BASE_URL}/api/comparisons/stores`,
            () => {
              return HttpResponse.json(
                {
                  message:
                    'Comparison could not be completed.',
                },
                {
                  status: 500,
                }
              )
            }
          )
        )

        const user =
          userEvent.setup()

        render(<App />)

        await user.type(screen.getByRole('textbox', { name: 'ZIP code' }), '27613')
        await user.click(screen.getByRole('button', { name: 'Find Stores' }))

        await user.click(
          await screen.findByRole(
            'checkbox',
            {
              name:
                /Kroger Downtown/,
            }
          )
        )

        // Act
        await user.click(
          screen.getByRole(
            'button',
            {
              name:
                'Compare Stores',
            }
          )
        )

        // Assert
        expect(
          await screen.findByText(
            'Error: Comparison could not be completed.'
          )
        ).toBeInTheDocument()

        expect(
          screen.getByRole(
            'button',
            {
              name:
                'Compare Stores',
            }
          )
        ).toBeEnabled()
      }
    )
  }
)
