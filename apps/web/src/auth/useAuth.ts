import { useSyncExternalStore } from 'react';
import { getAuthState, subscribe, type AuthState } from './session';

/** The current session, re-rendering the component whenever it starts or ends. */
export function useAuth(): AuthState {
  return useSyncExternalStore(subscribe, getAuthState);
}
