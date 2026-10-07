import { describe, expect, it } from 'vitest';
import { render, screen } from '@testing-library/react';
import { Bar, StatTile } from '../components/Controls.jsx';
import Login from '../pages/Login.jsx';

describe('components', () => {
  it('renders a stat tile with its n/a reason as a tooltip', () => {
    render(<StatTile label="Load" value="n/a" reason="Windows has no load average." />);
    expect(screen.getByText('Load').parentElement.getAttribute('title')).toBe('Windows has no load average.');
  });

  it('colors bars by load', () => {
    const { container } = render(<><Bar percent={30} /><Bar percent={70} /><Bar percent={95} /><Bar percent={null} /></>);
    const fills = [...container.querySelectorAll('.bar-fill')].map((e) => e.className);
    expect(fills).toEqual(['bar-fill low', 'bar-fill mid', 'bar-fill high', 'bar-fill none']);
  });

  it('renders the login form', () => {
    render(<Login />);
    expect(screen.getByRole('button', { name: 'Sign in' })).toBeTruthy();
    expect(screen.getByLabelText('Password')).toBeTruthy();
  });
});
