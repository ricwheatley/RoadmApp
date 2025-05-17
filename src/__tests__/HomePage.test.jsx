import { render, screen } from '@testing-library/react';
import React from 'react';
import HomePage from '../HomePage';

describe('HomePage', () => {
  test('displays a list of authorised tenants', () => {
    const tenants = [
      { id: '1', name: 'Tenant A' },
      { id: '2', name: 'Tenant B' }
    ];
    render(<HomePage tenants={tenants} />);
    expect(screen.getByText('Tenant A')).toBeInTheDocument();
    expect(screen.getByText('Tenant B')).toBeInTheDocument();
  });
});
