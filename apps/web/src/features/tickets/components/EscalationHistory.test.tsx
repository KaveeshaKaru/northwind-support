import { render, screen } from '@testing-library/react';
import { describe, expect, it } from 'vitest';
import type { Escalation } from '../../../types/api';
import { EscalationHistory } from './EscalationHistory';

const escalation: Escalation = {
  id: 1,
  fromPriority: 'High',
  toPriority: 'Critical',
  fromAgent: { id: 5, fullName: 'Marcus Doyle' },
  toAgent: { id: 4, fullName: 'Sara Lindqvist' },
  fromDueAtUtc: '2026-10-20T05:36:46Z',
  toDueAtUtc: '2026-10-20T02:23:06Z',
  reason: 'Customer is being double charged.',
  escalatedBy: 'kaveesha',
  escalatedAtUtc: '2026-10-19T22:23:06Z',
};

describe('EscalationHistory', () => {
  it('shows what changed, the reason, who and when', () => {
    render(<EscalationHistory escalations={[escalation]} />);

    expect(screen.getByText('Customer is being double charged.')).toBeInTheDocument();
    expect(screen.getByText(/kaveesha/)).toBeInTheDocument();
    expect(screen.getByText('High → Critical')).toBeInTheDocument();
    expect(screen.getByText('Marcus Doyle → Sara Lindqvist')).toBeInTheDocument();
    expect(screen.getAllByText(/2026/).length).toBeGreaterThan(0);
  });

  it('says so when the ticket has never been escalated', () => {
    render(<EscalationHistory escalations={[]} />);

    expect(screen.getByText('Not escalated yet.')).toBeInTheDocument();
  });
});
