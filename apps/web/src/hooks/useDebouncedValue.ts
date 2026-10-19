import { useEffect, useState } from 'react';

/** Returns `value` once it has stopped changing for `delayMs`, e.g. to wait for typing to settle. */
export function useDebouncedValue<T>(value: T, delayMs: number): T {
  const [debounced, setDebounced] = useState(value);

  useEffect(() => {
    const handle = window.setTimeout(() => setDebounced(value), delayMs);

    return () => window.clearTimeout(handle);
  }, [value, delayMs]);

  return debounced;
}
