import { formatDateTime } from '../../../lib/format';
import type { TicketDetail } from '../../../types/api';
import { PriorityBadge } from './TicketBadges';

interface TriageResultProps {
  ticket: TicketDetail;
}

/**
 * What the system decided when a ticket was raised: its priority, due date and owner, each with
 * the reason the API gave. Displays the decision; makes none of it.
 */
export function TriageResult({ ticket }: TriageResultProps) {
  const { triage } = ticket;

  return (
    <div role="status" className="triage">
      <p>
        Raised <strong>{ticket.reference}</strong> for {ticket.customer.name}.
      </p>

      {triage && (
        <dl className="summary">
          <div>
            <dt>Priority</dt>
            <dd>
              <PriorityBadge priority={triage.appliedPriority} />
              <span className="summary__note">{triage.priorityReason}</span>
            </dd>
          </div>

          <div>
            <dt>Due</dt>
            <dd>
              {formatDateTime(ticket.dueAtUtc)}
              <span className="summary__note">
                {triage.slaWindowHours}h window: {triage.slaReason}
              </span>
            </dd>
          </div>

          <div>
            <dt>Assigned to</dt>
            <dd>
              {ticket.assignedAgent?.fullName ?? 'Unassigned'}
              <span className="summary__note">{triage.assignmentReason}</span>
            </dd>
          </div>
        </dl>
      )}
    </div>
  );
}
