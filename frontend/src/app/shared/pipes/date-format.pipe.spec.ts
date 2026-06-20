import { LOCALE_ID } from '@angular/core';
import { TestBed } from '@angular/core/testing';

import { DateFormatPipe } from './date-format.pipe';

/**
 * Unit tests for {@link DateFormatPipe} — the null-safe, locale-aware date
 * formatter that generalizes the legacy DotNetNuke `FormatExpiryDate` admin-grid
 * helper (Website/admin/Portal/Portals.ascx.vb ~L250-260), which returned
 * `String.Empty` for null/sentinel dates and otherwise `DateTime.ToShortDateString`.
 *
 * Test design constraints (all deliberate — do not relax):
 * - The pipe resolves its locale via `inject(LOCALE_ID)`, so it MUST be
 *   constructed inside an Angular injection context. We provide the standalone
 *   pipe in the TestBed `providers` and resolve it with `TestBed.inject(...)`;
 *   calling `new DateFormatPipe()` outside an injection context would throw.
 * - `LOCALE_ID` is pinned to `'en-US'` so assertions are deterministic across
 *   environments. Under en-US, Angular's `'shortDate'` renders as `M/d/yy`
 *   (e.g. `1/15/24`). en-US locale data is bundled by default, so no
 *   `registerLocaleData` call is required.
 * - Date fixtures are TIMEZONE-SAFE: built with the LOCAL `Date` constructor
 *   (`new Date(2024, 0, 15)` — month is 0-indexed → January) or a local datetime
 *   string WITHOUT a `Z`/offset (`'2024-01-15T12:00:00'`). A bare ISO date string
 *   like `'2024-01-15'` parses as UTC midnight and can roll back a day in
 *   negative-offset timezones, making the suite flaky.
 */
describe('DateFormatPipe', () => {
  let pipe: DateFormatPipe;

  beforeEach(() => {
    // Provide the standalone pipe and pin the locale so `inject(LOCALE_ID)`
    // resolves deterministically to 'en-US' inside the injection context.
    TestBed.configureTestingModule({
      providers: [DateFormatPipe, { provide: LOCALE_ID, useValue: 'en-US' }],
    });
    // Resolving via TestBed constructs the pipe within an injection context.
    pipe = TestBed.inject(DateFormatPipe);
  });

  it('creates an instance', () => {
    expect(pipe).toBeTruthy();
  });

  it('returns an empty string for null', () => {
    expect(pipe.transform(null)).toBe('');
  });

  it('returns an empty string for undefined', () => {
    expect(pipe.transform(undefined)).toBe('');
  });

  it('returns an empty string for an empty string', () => {
    expect(pipe.transform('')).toBe('');
  });

  it('returns an empty string for an invalid date', () => {
    expect(pipe.transform('not-a-date')).toBe('');
  });

  it('formats a Date with the default short-date format (en-US)', () => {
    // Local constructor: 2024-01-15 in the local timezone (month 0 = January).
    expect(pipe.transform(new Date(2024, 0, 15))).toBe('1/15/24');
  });

  it('formats a local ISO datetime string', () => {
    // Noon local time has no `Z`/offset, so it stays Jan 15 in every timezone.
    expect(pipe.transform('2024-01-15T12:00:00')).toBe('1/15/24');
  });

  it('honors an explicit format argument', () => {
    expect(pipe.transform(new Date(2024, 0, 15), 'yyyy-MM-dd')).toBe('2024-01-15');
  });

  it('formats a numeric epoch timestamp', () => {
    // Same instant the local Date represents, expressed as epoch milliseconds.
    const ts: number = new Date(2024, 0, 15).getTime();
    expect(pipe.transform(ts)).toBe('1/15/24');
  });
});
