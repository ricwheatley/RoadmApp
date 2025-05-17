import { formatTenant } from '../utils/tokenUtils';

describe('formatTenant', () => {
  test('formats tenant name and id', () => {
    const tenant = { id: '123', name: 'Acme Co' };
    expect(formatTenant(tenant)).toBe('Acme Co (123)');
  });

  test('handles undefined tenant', () => {
    expect(formatTenant(undefined)).toBe('');
  });
});
