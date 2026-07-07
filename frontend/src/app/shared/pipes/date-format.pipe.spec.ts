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
});
