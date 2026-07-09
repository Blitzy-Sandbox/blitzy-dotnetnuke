import { LOCALE_ID } from '@angular/core';
import { TestBed } from '@angular/core/testing';

import { DateFormatPipe } from './date-format.pipe';

describe('DateFormatPipe', () => {
  let pipe: DateFormatPipe;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [{ provide: LOCALE_ID, useValue: 'en-US' }],
    });
    // DateFormatPipe calls inject(LOCALE_ID); construct it in an injection context.
    pipe = TestBed.runInInjectionContext(() => new DateFormatPipe());
  });

  it('creates an instance', () => {
    expect(pipe).toBeTruthy();
  });

  it('formats a valid ISO-8601 date string with a custom token', () => {
    expect(pipe.transform('2024-01-15T00:00:00', 'yyyy-MM-dd')).toBe('2024-01-15');
  });

  it('formats using the default mediumDate token (en-US)', () => {
    expect(pipe.transform('2024-01-15T00:00:00')).toBe('Jan 15, 2024');
  });

  it('accepts a Date instance', () => {
    expect(pipe.transform(new Date(2024, 0, 15), 'yyyy-MM-dd')).toBe('2024-01-15');
  });

  it('returns "" for null', () => {
    expect(pipe.transform(null)).toBe('');
  });

  it('returns "" for undefined', () => {
    expect(pipe.transform(undefined)).toBe('');
  });

  it('returns "" for an empty or whitespace-only string', () => {
    expect(pipe.transform('')).toBe('');
    expect(pipe.transform('   ')).toBe('');
  });

  it('returns the provided fallback for an invalid date string', () => {
    expect(pipe.transform('not-a-date', 'mediumDate', 'N/A')).toBe('N/A');
  });

  it('returns "" (default fallback) for an invalid date string', () => {
    expect(pipe.transform('not-a-date')).toBe('');
  });

  // MIGRATION (QA INFO): an unset .NET date serialises as DateTime.MinValue
  // ("0001-01-01T00:00:00"); the pipe must render it blank (legacy parity),
  // NOT the meaningless "Jan 1, 1" that DatePipe would otherwise produce.
  it('returns "" for the ISO DateTime.MinValue sentinel "0001-01-01T00:00:00"', () => {
    expect(pipe.transform('0001-01-01T00:00:00')).toBe('');
  });

  it('returns "" for the date-only MinValue sentinel "0001-01-01"', () => {
    expect(pipe.transform('0001-01-01')).toBe('');
  });

  it('returns "" for the invariant short-date MinValue sentinel "1/1/0001"', () => {
    expect(pipe.transform('1/1/0001')).toBe('');
  });

  it('honours a custom fallback for the MinValue sentinel', () => {
    expect(pipe.transform('0001-01-01T00:00:00', 'mediumDate', 'Never')).toBe('Never');
  });

  it('returns "" for a Date instance at year <= 1', () => {
    const minDate = new Date('0001-01-01T00:00:00');
    expect(pipe.transform(minDate)).toBe('');
  });

  it('still formats a genuine modern date after the MinValue guard', () => {
    expect(pipe.transform('2020-01-15T09:30:00', 'yyyy-MM-dd')).toBe('2020-01-15');
  });
});
