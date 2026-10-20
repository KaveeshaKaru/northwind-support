import { describe, expect, it } from 'vitest';
import { signedInAs } from '../test/fixtures';
import { endSession, getAuthState, readStoredSession, startSession } from './session';

describe('session', () => {
  it('keeps the session in sessionStorage, so a refresh stays signed in', () => {
    startSession(signedInAs());

    expect(readStoredSession()).toEqual(signedInAs());
    expect(getAuthState().session).toEqual(signedInAs());
  });

  it('forgets the session, and remembers why it ended, when signing out', () => {
    startSession(signedInAs());

    endSession('signedOut');

    expect(readStoredSession()).toBeNull();
    expect(getAuthState()).toEqual({ session: null, ended: 'signedOut' });
  });

  it('does not restore a stored session whose token has already expired', () => {
    startSession(signedInAs({ expiresAtUtc: '2020-01-01T00:00:00Z' }));

    expect(readStoredSession()).toBeNull();
  });
});
