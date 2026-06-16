import { LOCALE_ID } from '@angular/core';
import { TestBed } from '@angular/core/testing';

import { DateFormatPipe } from './date-format.pipe';

/**
 * Unit tests for {@link DateFormatPipe}.
 *
 * `DateFormatPipe` resolves its active locale through `inject(LOCALE_ID)`, so it MUST be
 * constructed inside an Angular injection context. The pipe is therefore registered as a
 * provider on the TestBed and obtained via `TestBed.inject(DateFormatPipe)`; constructing it
 * with `new DateFormatPipe()` outside an injection context would throw.
 *
 * `LOCALE_ID` is pinned to `'en-US'` so the formatted output is deterministic across machines:
 * Angular's `'shortDate'` alias renders as `M/d/yy` for `'en-US'` (e.g. `1/15/24`). The `'en-US'`
 * locale data is bundled by default, so no `registerLocaleData` call is required.
 *
 * All date fixtures are timezone-safe: they are built with the LOCAL `Date` constructor
 * (`new Date(2024, 0, 15)` — month is 0-indexed, hence January) or a LOCAL datetime string with no
 * `Z`/offset suffix (`'2024-01-15T12:00:00'`). `formatDate` renders in the host's local timezone by
 * default, so these fixtures resolve to January 15 regardless of the CI machine's timezone — a bare
 * `'2024-01-15'` string would parse as UTC midnight and could roll back a day in negative-offset
 * zones, making the suite flaky.
 */
describe('DateFormatPipe', () => {
  let pipe: DateFormatPipe;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [DateFormatPipe, { provide: LOCALE_ID, useValue: 'en-US' }],
    });
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
    expect(pipe.transform(new Date(2024, 0, 15))).toBe('1/15/24');
  });

  it('formats a local ISO datetime string', () => {
    expect(pipe.transform('2024-01-15T12:00:00')).toBe('1/15/24');
  });

  it('honors an explicit format argument', () => {
    expect(pipe.transform(new Date(2024, 0, 15), 'yyyy-MM-dd')).toBe('2024-01-15');
  });

  it('formats a numeric epoch timestamp', () => {
    const ts = new Date(2024, 0, 15).getTime();
    expect(pipe.transform(ts)).toBe('1/15/24');
  });
});
