import { describe, expect, it } from 'vitest'
import { localDate, localTime } from '../api/worktime.js'

describe('calendar request formatting', () => {
  it('uses local calendar day, not UTC', () => {
    expect(localDate(new Date(2026, 10, 13, 9, 3))).toBe('2026-11-13')
  })
  it('uses HH:mm without seconds', () => {
    expect(localTime(new Date(2026, 10, 13, 9, 3))).toBe('09:03')
  })
})
