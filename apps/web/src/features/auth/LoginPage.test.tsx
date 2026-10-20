import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { RouterProvider, createMemoryRouter, useLocation } from 'react-router-dom';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { ApiError } from '../../api/client';
import { authApi } from '../../api/auth';
import { RequireAuth } from '../../auth/RequireAuth';
import { endSession, getAuthState } from '../../auth/session';
import { signedInAs } from '../../test/fixtures';
import { LoginPage } from './LoginPage';

vi.mock('../../api/auth', () => ({
  authApi: {
    login: vi.fn(),
  },
}));

const login = vi.mocked(authApi.login);

function CurrentPage() {
  const location = useLocation();

  return <p>Protected page {location.pathname + location.search}</p>;
}

/** The login page and one protected page, routed the way the app routes them. */
function renderAt(path: string) {
  const router = createMemoryRouter(
    [
      { path: '/login', element: <LoginPage /> },
      { element: <RequireAuth />, children: [{ path: '*', element: <CurrentPage /> }] },
    ],
    { initialEntries: [path] },
  );
  render(<RouterProvider router={router} />);

  return router;
}

async function signIn(email: string, password: string) {
  const user = userEvent.setup();
  if (email) {
    await user.type(screen.getByLabelText('Email'), email);
  }
  if (password) {
    await user.type(screen.getByLabelText('Password'), password);
  }
  await user.click(screen.getByRole('button', { name: 'Sign in' }));
}

describe('LoginPage', () => {
  afterEach(() => {
    vi.clearAllMocks();
  });

  it('sends a signed-out visitor to /login', () => {
    const router = renderAt('/tickets/43');

    expect(router.state.location.pathname).toBe('/login');
    expect(screen.getByRole('heading', { name: 'Sign in' })).toBeInTheDocument();
  });

  it('after signing in, lands on the page the visitor was trying to reach', async () => {
    login.mockResolvedValue(signedInAs());
    renderAt('/tickets?status=Open');

    await signIn('sara.lindqvist@northwind-support.example', 'LocalDev-Only-Password1!');

    expect(await screen.findByText('Protected page /tickets?status=Open')).toBeInTheDocument();
    expect(login).toHaveBeenCalledWith({
      email: 'sara.lindqvist@northwind-support.example',
      password: 'LocalDev-Only-Password1!',
    });
    expect(getAuthState().session).toEqual(signedInAs());
  });

  it('lands on the ticket list when the visitor came straight to /login', async () => {
    login.mockResolvedValue(signedInAs());
    renderAt('/login');

    await signIn('sara.lindqvist@northwind-support.example', 'LocalDev-Only-Password1!');

    expect(await screen.findByText('Protected page /tickets')).toBeInTheDocument();
  });

  it('checks both fields before calling the API', async () => {
    renderAt('/login');

    await signIn('not-an-email', '');

    expect(screen.getByText('Enter a valid email address.')).toBeInTheDocument();
    expect(screen.getByText('Enter your password.')).toBeInTheDocument();
    expect(screen.getByLabelText('Email')).toHaveAttribute('aria-invalid', 'true');
    expect(login).not.toHaveBeenCalled();
  });

  it('shows the rejection, keeps the email, and clears the password', async () => {
    login.mockRejectedValue(new ApiError(401, 'Invalid email or password.'));
    renderAt('/login');

    await signIn('sara.lindqvist@northwind-support.example', 'wrong-password');

    expect(await screen.findByRole('alert')).toHaveTextContent('Invalid email or password.');
    expect(screen.getByLabelText('Email')).toHaveValue('sara.lindqvist@northwind-support.example');
    expect(screen.getByLabelText('Password')).toHaveValue('');
    expect(getAuthState().session).toBeNull();
  });

  it('shows that it is signing in, and blocks a second submit', async () => {
    login.mockReturnValue(new Promise(() => {}));
    renderAt('/login');

    await signIn('sara.lindqvist@northwind-support.example', 'LocalDev-Only-Password1!');

    expect(screen.getByRole('button', { name: 'Signing in...' })).toBeDisabled();
  });

  it('can show the password being typed, and hide it again', async () => {
    const user = userEvent.setup();
    renderAt('/login');
    await user.type(screen.getByLabelText('Password'), 'secret');

    await user.click(screen.getByRole('button', { name: 'Show password' }));
    expect(screen.getByLabelText('Password')).toHaveAttribute('type', 'text');

    await user.click(screen.getByRole('button', { name: 'Hide password' }));
    expect(screen.getByLabelText('Password')).toHaveAttribute('type', 'password');
  });

  it('says so when the visitor was sent here because their session expired', () => {
    endSession('expired');
    renderAt('/tickets');

    expect(screen.getByText('Your session has expired. Sign in again.')).toBeInTheDocument();
  });
});
