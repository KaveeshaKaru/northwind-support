import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { ApiError } from '../../../api/client';
import { ticketsApi } from '../../../api/tickets';
import { ticketDetail } from '../../../test/fixtures';
import { EscalationPanel } from './EscalationPanel';

vi.mock('../../../api/tickets', () => ({
  ticketsApi: {
    escalate: vi.fn(),
  },
}));

const escalate = vi.mocked(ticketsApi.escalate);

async function fillIn(reason: string) {
  const user = userEvent.setup();
  await user.type(screen.getByLabelText('Reason'), reason);
  await user.click(screen.getByRole('button', { name: 'Escalate' }));
}

describe('EscalationPanel', () => {
  afterEach(() => {
    vi.clearAllMocks();
  });

  it('blocks a reason that is too short, shows why, and does not call the API', async () => {
    render(<EscalationPanel ticket={ticketDetail()} onEscalated={vi.fn()} />);

    await fillIn('   abc   ');

    expect(
      screen.getByText('The reason must be between 5 and 500 characters.'),
    ).toBeInTheDocument();
    expect(screen.getByLabelText('Reason')).toHaveAttribute('aria-invalid', 'true');
    expect(escalate).not.toHaveBeenCalled();
  });

  it('shows a 409 from the server and leaves the ticket as it was', async () => {
    escalate.mockRejectedValue(
      new ApiError(409, 'Ticket TCK-0043 is already Critical and cannot be escalated further.'),
    );
    const onEscalated = vi.fn();
    render(<EscalationPanel ticket={ticketDetail()} onEscalated={onEscalated} />);

    await fillIn('Customer is very unhappy.');

    expect(await screen.findByRole('alert')).toHaveTextContent('already Critical');
    expect(onEscalated).not.toHaveBeenCalled();
    expect(screen.getByLabelText('Reason')).toHaveValue('Customer is very unhappy.');
  });

  it('sends the trimmed reason and hands back the escalated ticket', async () => {
    const escalated = ticketDetail({ priority: 'Critical' });
    escalate.mockResolvedValue(escalated);
    const onEscalated = vi.fn();
    render(<EscalationPanel ticket={ticketDetail()} onEscalated={onEscalated} />);

    await fillIn('  Customer is very unhappy.  ');

    expect(escalate).toHaveBeenCalledWith(43, { reason: 'Customer is very unhappy.' });
    expect(onEscalated).toHaveBeenCalledWith(escalated);
  });

  it('does not ask who is escalating - the API records the signed-in agent', () => {
    render(<EscalationPanel ticket={ticketDetail()} onEscalated={vi.fn()} />);

    expect(screen.queryByLabelText('Escalated by')).not.toBeInTheDocument();
  });

  it.each([
    [{ priority: 'Critical' as const }, 'already Critical', 'shortest SLA'],
    [{ status: 'Resolved' as const }, 'Resolved tickets cannot be escalated', 'back to Open'],
    [{ status: 'Closed' as const }, 'Closed tickets cannot be escalated', 'back to Open'],
  ])('explains why %o cannot be escalated, and what to do instead', (overrides, why, next) => {
    render(<EscalationPanel ticket={ticketDetail(overrides)} onEscalated={vi.fn()} />);

    expect(screen.getByText(new RegExp(why))).toBeInTheDocument();
    expect(screen.getByText(new RegExp(next))).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Escalate' })).not.toBeInTheDocument();
    expect(screen.queryByLabelText('Reason')).not.toBeInTheDocument();
  });
});
