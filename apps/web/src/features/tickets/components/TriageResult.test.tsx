import { render, screen } from '@testing-library/react';
import { describe, expect, it } from 'vitest';
import { ticketDetail } from '../../../test/fixtures';
import { TriageResult } from './TriageResult';

describe('TriageResult', () => {
  it('shows the applied priority, due date and agent, with the reason for each', () => {
    render(
      <TriageResult
        ticket={ticketDetail({
          priority: 'Critical',
          triage: {
            appliedPriority: 'Critical',
            priorityReason: "Category 'Security' is always treated as Critical",
            slaWindowHours: 2,
            slaReason: 'Critical base window 4h, halved for a Premium customer',
            assignmentReason: 'Fewest open tickets among Security specialists',
          },
          assignedAgent: { id: 3, fullName: 'Priya Nair' },
        })}
      />,
    );

    expect(screen.getByText('Critical')).toBeInTheDocument();
    expect(screen.getByText("Category 'Security' is always treated as Critical")).toBeInTheDocument();
    expect(screen.getByText(/2026/)).toBeInTheDocument();
    expect(screen.getByText(/2h window/)).toBeInTheDocument();
    expect(screen.getByText('Priya Nair')).toBeInTheDocument();
    expect(screen.getByText('Fewest open tickets among Security specialists')).toBeInTheDocument();
  });

  it('explains why a ticket was left unassigned', () => {
    render(
      <TriageResult
        ticket={ticketDetail({
          assignedAgent: null,
          triage: {
            appliedPriority: 'Critical',
            priorityReason: "Category 'Security' is always treated as Critical",
            slaWindowHours: 4,
            slaReason: 'Critical base window 4h',
            assignmentReason: 'No available agent specialised in Security',
          },
        })}
      />,
    );

    expect(screen.getByText('Unassigned')).toBeInTheDocument();
    expect(screen.getByText('No available agent specialised in Security')).toBeInTheDocument();
  });

  it('is announced to screen readers when it appears', () => {
    render(<TriageResult ticket={ticketDetail()} />);

    expect(screen.getByRole('status')).toHaveTextContent('TCK-0043');
  });
});
