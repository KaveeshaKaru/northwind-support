import { useEffect, useMemo, useState } from 'react';
import { ApiError } from '../../../api/client';
import { ticketsApi } from '../../../api/tickets';
import { useDebouncedValue } from '../../../hooks/useDebouncedValue';
import type { PagedResult, TicketFilters, TicketListItem } from '../../../types/api';

const DEBOUNCE_MS = 300;

export const emptyFilters: TicketFilters = {
  search: '',
  status: undefined,
  priority: undefined,
  categoryId: undefined,
  customerId: undefined,
  assignedAgentId: undefined,
  unassignedOnly: false,
  page: 1,
  pageSize: 20,
  sortBy: 'createdAtUtc',
  sortDirection: 'desc',
};

/** Every filter except the search text, which is debounced on its own. */
type ImmediateFilters = Omit<TicketFilters, 'search'>;

const { search: emptySearch, ...emptyImmediateFilters } = emptyFilters;

/** One request to the API: the filters to send, and which retry of them this is. */
interface ListRequest {
  query: TicketFilters;
  attempt: number;
}

/** What the last finished request produced. */
interface ListResult {
  request?: ListRequest;
  data?: PagedResult<TicketListItem>;
  error?: string;
}

/**
 * Owns the ticket list's filters and the data they produce. Search text is sent once typing
 * settles; every other filter is sent as soon as it changes.
 */
export function useTicketList() {
  const [filters, setFilters] = useState<ImmediateFilters>(emptyImmediateFilters);
  const [search, setSearch] = useState(emptySearch);
  const [attempt, setAttempt] = useState(0);
  const [result, setResult] = useState<ListResult>({});

  const debouncedSearch = useDebouncedValue(search, DEBOUNCE_MS);

  // The request is built from every filter, so adding a filter needs no extra wiring here.
  const request = useMemo<ListRequest>(
    () => ({ query: { ...filters, search: debouncedSearch }, attempt }),
    [filters, debouncedSearch, attempt],
  );

  useEffect(() => {
    // Responses can arrive out of order; one for a request that has been replaced is dropped.
    let isCurrent = true;

    ticketsApi.getTickets(request.query).then(
      (data) => {
        if (isCurrent) {
          setResult({ request, data });
        }
      },
      (error: unknown) => {
        if (isCurrent) {
          const message = error instanceof ApiError ? error.message : 'Unable to load tickets.';
          setResult((current) => ({ request, data: current.data, error: message }));
        }
      },
    );

    return () => {
      isCurrent = false;
    };
  }, [request]);

  const updateFilters = (patch: Partial<TicketFilters>) => {
    const { search: nextSearch, ...rest } = patch;

    if (nextSearch !== undefined) {
      setSearch(nextSearch);
    }

    if (Object.keys(rest).length > 0) {
      setFilters((current) => ({ ...current, ...rest, page: rest.page ?? 1 }));
    } else if (nextSearch !== undefined) {
      // A new search starts from the first page. Leave the object alone when it already does,
      // so typing does not trigger a request before the debounce settles.
      setFilters((current) => (current.page === 1 ? current : { ...current, page: 1 }));
    }
  };

  const resetFilters = () => {
    setSearch(emptySearch);
    setFilters(emptyImmediateFilters);
  };

  const reload = () => setAttempt((current) => current + 1);

  return {
    filters: { ...filters, search },
    updateFilters,
    resetFilters,
    reload,
    // Loading until the latest request has answered; the previous page stays visible meanwhile.
    isLoading: result.request !== request,
    data: result.data,
    error: result.request === request ? result.error : undefined,
  };
}
