import { render, screen } from '@testing-library/react'
import { describe, expect, it } from 'vitest'

function SmokeComponent() {
  return (
    <h1>
      CartCompare test environment
    </h1>
  )
}

describe('frontend test setup', () => {
  it('renders a React component', () => {
    render(
      <SmokeComponent />
    )

    expect(
      screen.getByRole(
        'heading',
        {
          name:
            'CartCompare test environment',
        }
      )
    ).toBeInTheDocument()
  })
})