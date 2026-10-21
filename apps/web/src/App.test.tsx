import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { RouterProvider, createMemoryRouter } from 'react-router-dom';
import { describe, expect, it } from 'vitest';
import { App } from './App';
import { RequireAuth } from './auth/RequireAuth';
import { getAuthState, startSession } from './auth/session';
import { signedInAs } from './test/fixtures';

function renderSignedIn() {
  startSession(signedInAs());
  const router = createMemoryRouter(
    [
      { path: '/login', element: <p>Login page</p> },
      {
        element: <RequireAuth />,
        children: [{ element: <App />, children: [{ path: '/tickets', element: <p>Tickets</p> }] }],
      },
    ],
    { initialEntries: ['/tickets'] },
  );
  render(<RouterProvider router={router} />);

  return router;
}

describe('App', () => {
  it('shows who is signed in', () => {
    renderSignedIn();

    expect(screen.getByText('Sara Lindqvist')).toBeInTheDocument();
    expect(screen.getByText('sara.lindqvist@northwind-support.example')).toBeInTheDocument();
    expect(screen.getByText('SL')).toBeInTheDocument();
  });

  it('signs out: forgets the session and returns to /login', async () => {
    const router = renderSignedIn();

    await userEvent.setup().click(screen.getByRole('button', { name: 'Sign out' }));

    expect(router.state.location.pathname).toBe('/login');
    expect(await screen.findByText('Login page')).toBeInTheDocument();
    expect(getAuthState()).toEqual({ session: null, ended: 'signedOut' });
  });
});
