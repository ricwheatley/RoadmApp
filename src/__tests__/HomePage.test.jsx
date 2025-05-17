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

  test('shows message when no tenants provided', () => {
    render(<HomePage tenants={[]} />);
    expect(screen.getByRole('heading', { name: /authorised tenants/i })).toBeInTheDocument();
    expect(screen.queryByRole('listitem')).not.toBeInTheDocument();
  });
});
