import { render, screen } from '@testing-library/react';
import LastUpdate from '../LastUpdate';

describe('LastUpdate', () => {
  test('renders human-readable time for ISO string', () => {
    const ts = '2024-01-01T12:34:56Z';
    render(<LastUpdate timestamp={ts} />);
    expect(screen.getByText(new Date(ts).toLocaleString())).toBeInTheDocument();
  });

  test('renders human-readable time for Date object', () => {
    const date = new Date('2024-01-02T00:00:00Z');
    render(<LastUpdate timestamp={date} />);
    expect(screen.getByText(date.toLocaleString())).toBeInTheDocument();
  });

  test('renders fallback for undefined', () => {
    render(<LastUpdate timestamp={undefined} />);
    expect(screen.getByText('Not updated yet')).toBeInTheDocument();
  });

  test('does not show "Invalid time" for bad input', () => {
    render(<LastUpdate timestamp="bad-value" />);
    expect(screen.getByText('Not updated yet')).toBeInTheDocument();
    expect(screen.queryByText(/Invalid/)).not.toBeInTheDocument();
  });
});
