import { afterEach, describe, expect, it, vi } from 'vitest';
import { emptyFilters } from '../features/tickets/hooks/useTicketList';
import { page } from '../test/fixtures';
import { ticketsApi } from './tickets';

describe('ticketsApi.getTickets', () => {
  afterEach(() => {
    vi.unstubAllGlobals();
  });

  it('sends the SLA status filter to the API', async () => {
    const fetchMock = vi.fn().mockResolvedValue(new Response(JSON.stringify(page([])), { status: 200 }));
    vi.stubGlobal('fetch', fetchMock);

    await ticketsApi.getTickets({ ...emptyFilters, slaStatus: 'AtRisk' });

    expect(String(fetchMock.mock.calls[0][0])).toContain('slaStatus=AtRisk');
  });
});
