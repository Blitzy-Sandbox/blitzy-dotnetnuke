import { TruncatePipe } from './truncate.pipe';

describe('TruncatePipe', () => {
  let pipe: TruncatePipe;

  beforeEach(() => {
    pipe = new TruncatePipe();
  });

  it('creates an instance', () => {
    expect(pipe).toBeTruthy();
  });

  it('returns the original string when under the limit', () => {
    expect(pipe.transform('short', 50)).toBe('short');
  });

  it('returns the original string when exactly at the limit', () => {
    expect(pipe.transform('exactly-ten', 11)).toBe('exactly-ten');
  });

  it('truncates and appends the default ellipsis when over the limit', () => {
    expect(pipe.transform('abcdefghij', 5)).toBe('abcde…');
  });

  it('supports a custom ellipsis', () => {
    expect(pipe.transform('abcdefghij', 5, '...')).toBe('abcde...');
  });

  it('cuts at a word boundary when requested', () => {
    expect(pipe.transform('the quick brown fox', 12, '…', true)).toBe('the quick…');
  });

  it('returns "" for null, undefined and empty input', () => {
    expect(pipe.transform(null)).toBe('');
    expect(pipe.transform(undefined)).toBe('');
    expect(pipe.transform('')).toBe('');
  });

  it('returns just the ellipsis for a non-positive limit', () => {
    expect(pipe.transform('abc', 0)).toBe('…');
  });
});
