import { formatDate } from '@angular/common';
import { LOCALE_ID } from '@angular/core';
import { TestBed } from '@angular/core/testing';

import { DateFormatPipe } from './date-format.pipe';

/**
 * Unit tests for {@link DateFormatPipe}.
 *
 * The pipe resolves its locale through `inject(LOCALE_ID)`, so it must be instantiated inside
 * an injection context. The pipe is therefore provided through the TestBed and retrieved via
 * `TestBed.inject(...)` (a fixed `'en-US'` locale keeps the expected output deterministic).
 * Expected values are computed with the same `formatDate` the pipe uses, which avoids hard
 * coding locale-specific separators while still asserting exact equality.
 */
describe('DateFormatPipe', () => {
  const locale = 'en-US';
  let pipe: DateFormatPipe;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [DateFormatPipe, { provide: LOCALE_ID, useValue: locale }],
    });
    pipe = TestBed.inject(DateFormatPipe);
  });

  it('creates an instance', () => {
    expect(pipe).toBeTruthy();
  });

  describe('null-safe degradation (returns an empty string)', () => {
    it('returns "" for null', () => {
      expect(pipe.transform(null)).toBe('');
    });

    it('returns "" for undefined', () => {
      expect(pipe.transform(undefined)).toBe('');
    });

    it('returns "" for an empty string', () => {
      expect(pipe.transform('')).toBe('');
    });

    it('returns "" for an unparseable date string', () => {
      expect(pipe.transform('not-a-date')).toBe('');
    });

    it('returns "" for NaN', () => {
      expect(pipe.transform(NaN)).toBe('');
    });
  });

  describe('formatting valid values with the default "shortDate" format', () => {
    it('formats a Date instance', () => {
      const date = new Date(2024, 0, 15);
      expect(pipe.transform(date)).toBe(formatDate(date, 'shortDate', locale));
    });

    it('formats a parseable ISO date string', () => {
      const iso = '2024-01-15T08:30:00';
      expect(pipe.transform(iso)).toBe(formatDate(new Date(iso), 'shortDate', locale));
    });

    it('formats an epoch-millisecond number', () => {
      const ms = new Date(2024, 0, 15).getTime();
      expect(pipe.transform(ms)).toBe(formatDate(new Date(ms), 'shortDate', locale));
    });

    it('produces a non-empty string for a valid date', () => {
      expect(pipe.transform(new Date(2024, 0, 15)).length).toBeGreaterThan(0);
    });
  });

  describe('honoring an explicit format argument', () => {
    it('applies a custom pattern', () => {
      const date = new Date(2024, 0, 15);
      expect(pipe.transform(date, 'yyyy-MM-dd')).toBe('2024-01-15');
    });

    it('applies a named Angular format alias', () => {
      const date = new Date(2024, 0, 15);
      expect(pipe.transform(date, 'mediumDate')).toBe(formatDate(date, 'mediumDate', locale));
    });
  });

  describe('robustness', () => {
    it('never throws regardless of input', () => {
      expect(() => pipe.transform(null)).not.toThrow();
      expect(() => pipe.transform(undefined)).not.toThrow();
      expect(() => pipe.transform('not-a-date')).not.toThrow();
      expect(() => pipe.transform(NaN)).not.toThrow();
      expect(() => pipe.transform(new Date(2024, 0, 15))).not.toThrow();
      expect(() => pipe.transform(new Date(2024, 0, 15), 'yyyy-MM-dd')).not.toThrow();
    });
  });
});
