import {
  http,
  HttpResponse,
} from 'msw'
import {
  afterEach,
  describe,
  expect,
  it,
  vi,
} from 'vitest'

import { server } from '../test/server'
import {
  apiRequest,
  SESSION_EXPIRED_EVENT,
} from './apiClient'

const API_BASE_URL =
  import.meta.env.VITE_API_BASE_URL

describe('apiRequest errors', () => {
  afterEach(() => {
    sessionStorage.clear()
  })

  it('explains when the API cannot be reached', async () => {
    server.use(http.get(`${API_BASE_URL}/api/items`, () => HttpResponse.error()))

    await expect(apiRequest('/api/items')).rejects.toThrow(
      "Can't reach CartCompare right now. Check your connection and try again."
    )
  })

  it('hides server error details behind a friendly message', async () => {
    server.use(http.get(`${API_BASE_URL}/api/items`, () =>
      new HttpResponse('Internal Server Error', { status: 500 })
    ))

    await expect(apiRequest('/api/items')).rejects.toThrow(
      'Something went wrong on our side. Please try again in a moment.'
    )
  })

  it('lists model validation messages', async () => {
    server.use(http.post(`${API_BASE_URL}/api/comparisons/stores`, () =>
      HttpResponse.json(
        {
          title: 'One or more validation errors occurred.',
          errors: { StoreLocationIds: ['The field StoreLocationIds must be a maximum length of 10.'] },
        },
        { status: 400 }
      )
    ))

    await expect(apiRequest('/api/comparisons/stores', { method: 'POST' })).rejects.toThrow(
      'The field StoreLocationIds must be a maximum length of 10.'
    )
  })

  it('ends an expired session and announces it', async () => {
    sessionStorage.setItem('cartcompare_token', 'expired-token')
    server.use(http.get(`${API_BASE_URL}/api/grocery-list`, () =>
      new HttpResponse(null, { status: 401 })
    ))
    const listener = vi.fn()
    window.addEventListener(SESSION_EXPIRED_EVENT, listener)

    await expect(apiRequest('/api/grocery-list')).rejects.toThrow(
      'Your session has expired. Please log in again.'
    )

    expect(sessionStorage.getItem('cartcompare_token')).toBeNull()
    expect(listener).toHaveBeenCalledOnce()
    window.removeEventListener(SESSION_EXPIRED_EVENT, listener)
  })

  it('keeps the login error when no session existed', async () => {
    server.use(http.post(`${API_BASE_URL}/api/auth/login`, () =>
      HttpResponse.json({ message: 'Invalid email or password.' }, { status: 401 })
    ))
    const listener = vi.fn()
    window.addEventListener(SESSION_EXPIRED_EVENT, listener)

    await expect(apiRequest('/api/auth/login', { method: 'POST' })).rejects.toThrow(
      'Invalid email or password.'
    )

    expect(listener).not.toHaveBeenCalled()
    window.removeEventListener(SESSION_EXPIRED_EVENT, listener)
  })
})
