import '@testing-library/jest-dom/vitest';
import { afterEach } from 'vitest';
import { endSession } from '../auth/session';

// Every test starts signed out.
afterEach(() => {
  endSession('signedOut');
});
