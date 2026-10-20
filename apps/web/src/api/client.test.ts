import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { getAuthState, startSession } from '../auth/session';
import { signedInAs } from '../test/fixtures';
import { ApiError, request } from './client';

function respondWith(status: number, body: unknown = {}) {
  const fetchMock = vi.fn().mockResolvedValue(new Response(JSON.stringify(body), { status }));
  vi.stubGlobal('fetch', fetchMock);

  return fetchMock;
}

function sentHeaders(fetchMock: ReturnType<typeof vi.fn>): Record<string, string> {
  return fetchMock.mock.calls[0][1].headers;
}

describe('request', () => {
  afterEach(() => {
    vi.unstubAllGlobals();
  });

  describe('when signed in', () => {
    beforeEach(() => {
      startSession(signedInAs());
    });

    it('attaches the token to the call', async () => {
      const fetchMock = respondWith(200);

      await request('/tickets');

      expect(sentHeaders(fetchMock).Authorization).toBe('Bearer test-token');
    });

    it('ends the session when the API answers 401, and still reports the failure', async () => {
      respondWith(401, { title: 'Unauthorized' });

      await expect(request('/tickets')).rejects.toBeInstanceOf(ApiError);

      expect(getAuthState()).toEqual({ session: null, ended: 'expired' });
    });

    it('keeps the session for any other failure', async () => {
      respondWith(409, { title: 'Conflict' });

      await expect(request('/tickets/1/escalate')).rejects.toBeInstanceOf(ApiError);

      expect(getAuthState().session).not.toBeNull();
    });
  });

  describe('when signed out', () => {
    it('sends no Authorization header', async () => {
      const fetchMock = respondWith(200);

      await request('/auth/login');

      expect(sentHeaders(fetchMock).Authorization).toBeUndefined();
    });

    it('leaves a rejected sign-in to the login page rather than treating it as an expiry', async () => {
      respondWith(401, { detail: 'Invalid email or password.' });

      await expect(request('/auth/login')).rejects.toThrow('Invalid email or password.');

      expect(getAuthState().ended).not.toBe('expired');
    });
  });
});
