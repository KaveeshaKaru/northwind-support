import { Link, NavLink, Outlet } from 'react-router-dom';
import { endSession } from './auth/session';
import { useAuth } from './auth/useAuth';

/** "Sara Lindqvist" -> "SL": the avatar for an agent without a photo. */
function initials(fullName: string): string {
  const parts = fullName.trim().split(/\s+/);

  return `${parts[0]?.[0] ?? ''}${parts.length > 1 ? parts[parts.length - 1][0] : ''}`.toUpperCase();
}

/** Application shell: the header, the navigation, and wherever the router puts the page. */
export function App() {
  const { session } = useAuth();

  return (
    <div className="app">
      <header className="app__header">
        <Link className="app__brand" to="/tickets">
          <span className="brand-mark" aria-hidden="true">
            N
          </span>
          Northwind Support
        </Link>

        <nav className="app__nav" aria-label="Main">
          <NavLink to="/tickets">Tickets</NavLink>
          <NavLink to="/customers">Customers</NavLink>
        </nav>

        {session && (
          <div className="app__user">
            <span className="app__avatar" aria-hidden="true">
              {initials(session.agent.fullName)}
            </span>
            <span className="app__identity">
              <span className="app__name">{session.agent.fullName}</span>
              <span className="app__email">{session.agent.email}</span>
            </span>
            <button type="button" className="app__sign-out" onClick={() => endSession('signedOut')}>
              <svg viewBox="0 0 20 20" width="16" height="16" aria-hidden="true">
                <path
                  d="M8 4H5a1 1 0 0 0-1 1v10a1 1 0 0 0 1 1h3M13 13l3-3-3-3M16 10H8"
                  fill="none"
                  stroke="currentColor"
                  strokeWidth="1.6"
                  strokeLinecap="round"
                  strokeLinejoin="round"
                />
              </svg>
              Sign out
            </button>
          </div>
        )}
      </header>

      <main className="app__main">
        <Outlet />
      </main>
    </div>
  );
}
