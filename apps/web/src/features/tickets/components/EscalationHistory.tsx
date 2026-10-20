import { formatDateTime } from '../../../lib/format';
import type { AgentSummary, Escalation } from '../../../types/api';

interface EscalationHistoryProps {
  escalations: Escalation[];
}

const agentName = (agent: AgentSummary | null) => agent?.fullName ?? 'Unassigned';

/** A ticket's escalations, newest first as the API returns them. */
export function EscalationHistory({ escalations }: EscalationHistoryProps) {
  if (escalations.length === 0) {
    return <p className="summary__note">Not escalated yet.</p>;
  }

  return (
    <ol className="escalations">
      {escalations.map((escalation) => (
        <li key={escalation.id} className="escalations__item">
          <p className="escalations__meta">
            {formatDateTime(escalation.escalatedAtUtc)} by <strong>{escalation.escalatedBy}</strong>
          </p>
          <p className="escalations__reason">{escalation.reason}</p>
          <dl className="escalations__changes">
            <dt>Priority</dt>
            <dd>{`${escalation.fromPriority} → ${escalation.toPriority}`}</dd>
            <dt>Agent</dt>
            <dd>{`${agentName(escalation.fromAgent)} → ${agentName(escalation.toAgent)}`}</dd>
            <dt>Due</dt>
            <dd>{`${formatDateTime(escalation.fromDueAtUtc)} → ${formatDateTime(escalation.toDueAtUtc)}`}</dd>
          </dl>
        </li>
      ))}
    </ol>
  );
}
