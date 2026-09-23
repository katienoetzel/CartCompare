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

    http.get(
      `${API_BASE_URL}/api/store-locations/retailer/1`,
      ({ request }) => {
        expect(
          request.headers.get(
            'Authorization'
          )
        ).toBe(
          'Bearer existing-token'
        )

        return HttpResponse.json([
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
        ])
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
      'loads stores when a retailer is selected',
      async () => {
        // Arrange
        const user =
          userEvent.setup()

        render(<App />)

        const retailerSelect =
          await screen.findByRole(
            'combobox',
            {
              name: 'Retailer',
            }
          )

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
        await user.selectOptions(
          retailerSelect,
          '1'
        )

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

        const retailerSelect =
          await screen.findByRole(
            'combobox',
            {
              name: 'Retailer',
            }
          )

        await user.selectOptions(
          retailerSelect,
          '1'
        )

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

        await user.selectOptions(
          await screen.findByRole(
            'combobox',
            {
              name: 'Retailer',
            }
          ),
          '1'
        )

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
