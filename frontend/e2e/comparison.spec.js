import {
  expect,
  test,
} from '@playwright/test'

const API_URL =
  'http://localhost:5055'

async function readJson(
  response,
  description
) {
  const text =
    await response.text()

  expect(
    response.ok(),
    `${description} failed: ${response.status()} ${text}`
  ).toBeTruthy()

  return text
    ? JSON.parse(text)
    : null
}

test(
  'user can build a grocery list and compare two stores',
  async ({
    page,
    request,
  }) => {
    const uniqueId =
      `${Date.now()}-${process.pid}`

    const email =
      `comparison-${uniqueId}@example.com`

    const password =
      'CartCompare123'

    const retailerName =
      `E2E Grocery ${uniqueId}`

    const itemName =
      `E2E Whole Milk ${uniqueId}`

    const storeOneName =
      `E2E Downtown ${uniqueId}`

    const storeTwoName =
      `E2E North ${uniqueId}`

    // ---------------------------------
    // Arrange: create test user
    // ---------------------------------

    const registerResponse =
      await request.post(
        `${API_URL}/api/auth/register`,
        {
          data: {
            firstName:
              'Playwright',
            lastName:
              'Comparison',
            email,
            password,
          },
        }
      )

    await readJson(
      registerResponse,
      'Register user'
    )

    const loginResponse =
      await request.post(
        `${API_URL}/api/auth/login`,
        {
          data: {
            email,
            password,
          },
        }
      )

    const loginData =
      await readJson(
        loginResponse,
        'Login user'
      )

    const token =
      loginData.token ??
      loginData.accessToken

    expect(token).toBeTruthy()

    const headers = {
      Authorization:
        `Bearer ${token}`,
    }

    // ---------------------------------
    // Arrange: retailer
    // ---------------------------------

    const retailerResponse =
      await request.post(
        `${API_URL}/api/retailers`,
        {
          headers,
          data: {
            name:
              retailerName,
            supportsMembership:
              false,
          },
        }
      )

    const retailer =
      await readJson(
        retailerResponse,
        'Create retailer'
      )

    // ---------------------------------
    // Arrange: canonical item
    // ---------------------------------

    const itemResponse =
      await request.post(
        `${API_URL}/api/items`,
        {
          headers,
          data: {
            name:
              itemName,
            brand:
              'E2E Brand',
            size:
              '1 gallon',
            category:
              'Dairy',
          },
        }
      )

    const item =
      await readJson(
        itemResponse,
        'Create item'
      )

    // ---------------------------------
    // Arrange: two physical stores
    // ---------------------------------

    const storeOneResponse =
      await request.post(
        `${API_URL}/api/store-locations`,
        {
          headers,
          data: {
            retailerId:
              retailer.id,

            externalLocationId:
              `e2e-store-a-${uniqueId}`,

            name:
              storeOneName,

            addressLine1:
              '100 Main Street',

            addressLine2:
              null,

            city:
              'Raleigh',

            state:
              'NC',

            postalCode:
              '27601',

            latitude:
              null,

            longitude:
              null,
          },
        }
      )

    const storeOne =
      await readJson(
        storeOneResponse,
        'Create first store'
      )

    const storeTwoResponse =
      await request.post(
        `${API_URL}/api/store-locations`,
        {
          headers,
          data: {
            retailerId:
              retailer.id,

            externalLocationId:
              `e2e-store-b-${uniqueId}`,

            name:
              storeTwoName,

            addressLine1:
              '200 North Street',

            addressLine2:
              null,

            city:
              'Raleigh',

            state:
              'NC',

            postalCode:
              '27609',

            latitude:
              null,

            longitude:
              null,
          },
        }
      )

    await readJson(
  storeTwoResponse,
  'Create second store'
)

    // ---------------------------------
    // Arrange: retailer product
    // ---------------------------------

    const productResponse =
      await request.post(
        `${API_URL}/api/retailer-products`,
        {
          headers,
          data: {
            itemId:
              item.id,

            retailerId:
              retailer.id,

            externalProductId:
              `e2e-product-${uniqueId}`,

            name:
              itemName,

            brand:
              'E2E Brand',

            size:
              '1 gallon',

            upc:
              null,
          },
        }
      )

    const retailerProduct =
      await readJson(
        productResponse,
        'Create retailer product'
      )

    // ---------------------------------
    // Arrange: price only Store #1
    //
    // Store #1 should be COMPLETE.
    // Store #2 should be INCOMPLETE.
    // ---------------------------------

    const priceResponse =
      await request.put(
        `${API_URL}/api/prices`,
        {
          headers,
          data: {
            retailerProductId:
              retailerProduct.id,

            storeLocationId:
              storeOne.id,

            regularPrice:
              3.49,

            salePrice:
              null,

            memberPrice:
              null,

            availabilityStatus:
              1,

            sourceProvider:
              'E2E',

            sourceUpdatedAt:
              null,
          },
        }
      )

    await readJson(
      priceResponse,
      'Create price'
    )

    // ---------------------------------
    // Act: log in through the browser
    // ---------------------------------

    await page.goto('/')

    await page
      .getByLabel('Email')
      .fill(email)

    await page
      .getByLabel('Password')
      .fill(password)

    await page
      .getByRole(
        'button',
        {
          name: 'Login',
        }
      )
      .click()

    await expect(
      page.getByText(
        'You are logged in.'
      )
    ).toBeVisible()

    // ---------------------------------
    // Act: add the grocery item
    // ---------------------------------

    const catalogItem =
      page
        .getByRole('listitem')
        .filter({
          hasText:
            itemName,
        })
        .first()

    await expect(
      catalogItem
    ).toBeVisible()

    const quantityInput =
      catalogItem
        .getByLabel(
          'Quantity'
        )

    await quantityInput.fill(
      '2'
    )

    await catalogItem
      .getByRole(
        'button',
        {
          name:
            'Add / Update',
        }
      )
      .click()

    await expect(
      page.getByText(
        'Grocery list updated.'
      )
    ).toBeVisible()

    const groceryListSection =
      page
        .locator('section')
        .filter({
          has:
            page.getByRole(
              'heading',
              {
                name:
                  'My Grocery List',
              }
            ),
        })

    await expect(
      groceryListSection
        .getByText(
          itemName
        )
    ).toBeVisible()

    await expect(
      groceryListSection
        .getByText(
          /Quantity:\s*2/
        )
    ).toBeVisible()

    // ---------------------------------
    // Act: choose retailer
    // ---------------------------------

    const retailerSelect =
      page.getByRole(
        'combobox',
        {
          name:
            'Retailer',
        }
      )

    await retailerSelect
      .selectOption({
        label:
          retailerName,
      })

    // ---------------------------------
    // Act: select both stores
    // ---------------------------------

    const storeOneCheckbox =
      page.getByRole(
        'checkbox',
        {
          name:
            new RegExp(
              storeOneName
            ),
        }
      )

    const storeTwoCheckbox =
      page.getByRole(
        'checkbox',
        {
          name:
            new RegExp(
              storeTwoName
            ),
        }
      )

    await expect(
      storeOneCheckbox
    ).toBeVisible()

    await expect(
      storeTwoCheckbox
    ).toBeVisible()

    await storeOneCheckbox.click()
    await storeTwoCheckbox.click()

    await expect(
      page.getByText(
        'Selected stores: 2'
      )
    ).toBeVisible()

    // ---------------------------------
    // Act: compare
    // ---------------------------------

    await page
      .getByRole(
        'button',
        {
          name:
            'Compare Stores',
        }
      )
      .click()

    // ---------------------------------
    // Assert: comparison summary
    // ---------------------------------

    await expect(
      page.getByRole(
        'heading',
        {
          name:
            'Comparison Results',
        }
      )
    ).toBeVisible()

    await expect(
      page.getByText(
        /Complete stores:\s*1/
      )
    ).toBeVisible()

    await expect(
      page.getByText(
        /Incomplete stores:\s*1/
      )
    ).toBeVisible()

    // ---------------------------------
    // Assert: complete store
    // ---------------------------------

    const completeStore =
      page
        .locator('article')
        .filter({
          hasText:
            storeOneName,
        })

    await expect(
      completeStore
    ).toContainText(
      'Status: Complete'
    )

    await expect(
      completeStore
    ).toContainText(
      'Known subtotal: $6.98'
    )

    await expect(
      completeStore
    ).toContainText(
      '$3.49 each'
    )

    await expect(
      completeStore
    ).toContainText(
      '$6.98 total'
    )

    // ---------------------------------
    // Assert: incomplete store
    // ---------------------------------

    const incompleteStore =
      page
        .locator('article')
        .filter({
          hasText:
            storeTwoName,
        })

    await expect(
      incompleteStore
    ).toContainText(
      'Status: Incomplete'
    )

    await expect(
      incompleteStore
    ).toContainText(
      'Missing items: 1'
    )

    await expect(
      incompleteStore
    ).toContainText(
      'Price unavailable'
    )
  }
)