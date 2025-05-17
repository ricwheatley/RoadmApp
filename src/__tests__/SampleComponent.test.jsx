import { render, screen } from '@testing-library/react';
import React from 'react';
import SampleComponent from '../SampleComponent';

test('renders greeting', () => {
  render(<SampleComponent />);
  expect(screen.getByText(/hello jest/i)).toBeInTheDocument();
});
