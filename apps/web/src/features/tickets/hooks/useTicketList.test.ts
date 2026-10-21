import { act, renderHook } from '@testing-library/react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { ticketsApi } from '../../../api/tickets';
import { page, ticketListItem } from '../../../test/fixtures';
import type { PagedResult, TicketFilters, TicketListItem } from '../../../types/api';
import { useTicketList } from './useTicketList';

vi.mock('../../../api/tickets', () => ({
  ticketsApi: {
    getTickets: vi.fn(),
  },
}));

const getTickets = vi.mocked(ticketsApi.getTickets);

/** Renders the hook and lets the first load finish, so each test starts from a settled list. */
async function renderSettled() {
  const hook = renderHook(() => useTicketList());

  await act(async () => {
    await vi.advanceTimersByTimeAsync(300);
  });

  getTickets.mockClear();

  return hook;
}

/** Lets pending effects and promises run without moving past any debounce. */
async function flush(ms = 0) {
  await act(async () => {
    await vi.advanceTimersByTimeAsync(ms);
  });
}

describe('useTicketList', () => {
  beforeEach(() => {
    vi.useFakeTimers();
    getTickets.mockResolvedValue(page([ticketListItem()]));
  });

  afterEach(() => {
    vi.useRealTimers();
    vi.clearAllMocks();
  });

  it('searches with the text the user typed, once the typing settles', async () => {
    const { result } = renderHook(() => useTicketList());

    await act(async () => {
      await vi.advanceTimersByTimeAsync(300);
    });

    expect(getTickets).toHaveBeenCalledTimes(1);

    act(() => {
      result.current.updateFilters({ search: 'invoice' });
    });

    // Still inside the debounce window: no second request yet.
    expect(getTickets).toHaveBeenCalledTimes(1);

    await act(async () => {
      await vi.advanceTimersByTimeAsync(300);
    });

    expect(getTickets).toHaveBeenCalledTimes(2);
    expect(getTickets).toHaveBeenLastCalledWith(
      expect.objectContaining({ search: 'invoice', page: 1 }),
    );

    expect(result.current.isLoading).toBe(false);
    expect(result.current.data?.items).toHaveLength(1);
  });

  // DEFECT-117: changing the status sent no request at all.
  it('requests immediately when the status filter changes', async () => {
    const { result } = await renderSettled();

    act(() => {
      result.current.updateFilters({ status: 'InProgress' });
    });
    await flush();

    expect(getTickets).toHaveBeenCalledTimes(1);
    expect(getTickets).toHaveBeenLastCalledWith(
      expect.objectContaining({ status: 'InProgress', page: 1 }),
    );
  });

  it.each<Partial<TicketFilters>>([
    { priority: 'High' },
    { categoryId: 2 },
    { customerId: 3 },
    { assignedAgentId: 4 },
    { unassignedOnly: true },
    { sortBy: 'priority', sortDirection: 'desc' },
    // Added with the Task 2 SLA filter: the hook needed no change to support it.
    { slaStatus: 'Breached' },
  ])('requests immediately when %o changes', async (patch) => {
    const { result } = await renderSettled();

    act(() => {
      result.current.updateFilters(patch);
    });
    await flush();

    expect(getTickets).toHaveBeenCalledTimes(1);
    expect(getTickets).toHaveBeenLastCalledWith(expect.objectContaining(patch));
  });

  it('sends one request for a burst of keystrokes', async () => {
    const { result } = await renderSettled();

    act(() => result.current.updateFilters({ search: 'i' }));
    await flush(100);
    act(() => result.current.updateFilters({ search: 'in' }));
    await flush(100);
    act(() => result.current.updateFilters({ search: 'inv' }));
    await flush(300);

    expect(getTickets).toHaveBeenCalledTimes(1);
    expect(getTickets).toHaveBeenLastCalledWith(expect.objectContaining({ search: 'inv' }));
  });

  it('keeps an earlier status when the search changes afterwards', async () => {
    const { result } = await renderSettled();

    act(() => result.current.updateFilters({ status: 'InProgress' }));
    await flush();
    act(() => result.current.updateFilters({ search: 'invoice' }));
    await flush(300);

    expect(getTickets).toHaveBeenCalledTimes(2);
    expect(getTickets).toHaveBeenLastCalledWith(
      expect.objectContaining({ status: 'InProgress', search: 'invoice' }),
    );
  });

  it('requests again when the search is cleared', async () => {
    const { result } = await renderSettled();

    act(() => result.current.updateFilters({ search: 'inv' }));
    await flush(300);
    getTickets.mockClear();

    act(() => result.current.updateFilters({ search: '' }));
    await flush(300);

    expect(getTickets).toHaveBeenCalledTimes(1);
    expect(getTickets).toHaveBeenLastCalledWith(expect.objectContaining({ search: '' }));
  });

  it('shows the newest filters when an older request resolves last', async () => {
    let resolveSlow: (value: PagedResult<TicketListItem>) => void = () => {};
    const slow = new Promise<PagedResult<TicketListItem>>((resolve) => {
      resolveSlow = resolve;
    });
    const newest = page([ticketListItem({ id: 99, reference: 'TCK-0099' })]);

    const { result } = await renderSettled();

    getTickets.mockReturnValueOnce(slow).mockResolvedValueOnce(newest);

    act(() => result.current.updateFilters({ status: 'Open' }));
    await flush();
    act(() => result.current.updateFilters({ status: 'Closed' }));
    await flush();

    await act(async () => {
      resolveSlow(page([ticketListItem({ id: 1, reference: 'TCK-0001' })]));
      await vi.advanceTimersByTimeAsync(0);
    });

    expect(result.current.data).toEqual(newest);
  });
});
