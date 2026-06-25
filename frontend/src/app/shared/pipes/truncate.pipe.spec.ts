// MIGRATION: Net-new Jasmine unit tests (Gate 4) for the net-new TruncatePipe. Pure pipe tested via
// direct instantiation (no TestBed/providers required).
import { TruncatePipe } from './truncate.pipe';

describe('TruncatePipe', () => {
  let pipe: TruncatePipe;

  beforeEach(() => {
    pipe = new TruncatePipe();
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

  it('returns the value unchanged when shorter than the default limit', () => {
    expect(pipe.transform('short text')).toBe('short text');
  });

  it('returns the value unchanged when exactly at the default limit (50)', () => {
    const exactly50 = 'a'.repeat(50);
    expect(pipe.transform(exactly50)).toBe(exactly50);
  });

  it('truncates and appends the default ellipsis when longer than the default limit', () => {
    const longText = 'a'.repeat(51);
    expect(pipe.transform(longText)).toBe('a'.repeat(50) + '…');
  });

  it('honours a custom limit', () => {
    expect(pipe.transform('abcdef', 3)).toBe('abc…');
  });

  it('returns the value unchanged when its length equals a custom limit', () => {
    expect(pipe.transform('abc', 3)).toBe('abc');
  });

  it('honours a custom trail string', () => {
    expect(pipe.transform('abcdef', 3, '...')).toBe('abc...');
  });
});
