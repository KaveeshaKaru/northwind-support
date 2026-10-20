import type { LoginResponse } from '../types/api';

/**
 * The signed-in agent's session: kept in sessionStorage, so it survives a refresh but ends when
 * the tab closes and is not shared with other tabs. sessionStorage is readable by any script on
 * the page, so an XSS hole would expose the token - the README weighs this against a cookie.
 */
export type Session = LoginResponse;

/** How the last session ended: the agent signed out, or the API stopped accepting the token. */
export type SessionEnd = 'signedOut' | 'expired';

export interface AuthState {
  session: Session | null;
  ended: SessionEnd | null;
}

const storageKey = 'support-desk.session';

const listeners = new Set<() => void>();

let state: AuthState = { session: readStoredSession(), ended: null };

/** The stored session, or null when there is none or its token has already expired. */
export function readStoredSession(): Session | null {
  try {
    const stored = sessionStorage.getItem(storageKey);
    const session = stored ? (JSON.parse(stored) as Session) : null;

    return session && Date.parse(session.expiresAtUtc) > Date.now() ? session : null;
  } catch {
    // Storage blocked or the value unreadable: treat it as signed out.
    return null;
  }
}

export function getAuthState(): AuthState {
  return state;
}

export function startSession(session: Session): void {
  write(session);
  update({ session, ended: null });
}

export function endSession(reason: SessionEnd): void {
  write(null);
  update({ session: null, ended: reason });
}

/** For useSyncExternalStore: calls back whenever the session starts or ends. */
export function subscribe(listener: () => void): () => void {
  listeners.add(listener);

  return () => listeners.delete(listener);
}

function update(next: AuthState) {
  state = next;
  listeners.forEach((listener) => listener());
}

function write(session: Session | null) {
  try {
    if (session) {
      sessionStorage.setItem(storageKey, JSON.stringify(session));
    } else {
      sessionStorage.removeItem(storageKey);
    }
  } catch {
    // Storage blocked: the session still lasts until the page is reloaded.
  }
}
