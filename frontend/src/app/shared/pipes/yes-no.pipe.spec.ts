// MIGRATION: Net-new Jasmine unit tests (Gate 4) for the net-new YesNoPipe. Pure pipe tested via
// direct instantiation (no TestBed/providers required).
import { YesNoPipe } from './yes-no.pipe';

describe('YesNoPipe', () => {
  let pipe: YesNoPipe;

  beforeEach(() => {
    pipe = new YesNoPipe();
  });

  it('creates an instance', () => {
    expect(pipe).toBeTruthy();
  });

  it('returns "Yes" for true', () => {
    expect(pipe.transform(true)).toBe('Yes');
  });

  it('returns "No" for false', () => {
    expect(pipe.transform(false)).toBe('No');
  });

  it('returns "No" for null', () => {
    expect(pipe.transform(null)).toBe('No');
  });

  it('returns "No" for undefined', () => {
    expect(pipe.transform(undefined)).toBe('No');
  });

  it('honours custom affirmative text for true', () => {
    expect(pipe.transform(true, 'Active', 'Inactive')).toBe('Active');
  });

  it('honours custom negative text for false', () => {
    expect(pipe.transform(false, 'Active', 'Inactive')).toBe('Inactive');
  });

  it('returns the custom negative text for null', () => {
    expect(pipe.transform(null, 'Active', 'Inactive')).toBe('Inactive');
  });
});
