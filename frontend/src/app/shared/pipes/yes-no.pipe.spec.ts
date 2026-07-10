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

  it('returns "" for null', () => {
    expect(pipe.transform(null)).toBe('');
  });

  it('returns "" for undefined', () => {
    expect(pipe.transform(undefined)).toBe('');
  });

  it('supports custom true/false labels', () => {
    expect(pipe.transform(true, 'Active', 'Inactive')).toBe('Active');
    expect(pipe.transform(false, 'Active', 'Inactive')).toBe('Inactive');
  });

  it('supports a custom null label', () => {
    expect(pipe.transform(null, 'Active', 'Inactive', '—')).toBe('—');
  });
});
