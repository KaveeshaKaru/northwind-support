import { Badge, type BadgeTone } from '../../../components/Badge';
import { formatDateTime } from '../../../lib/format';
import type { SlaStatus } from '../../../types/api';

/** Every state has a label and an icon of its own, so colour is never the only signal. */
const appearance: Record<SlaStatus, { label: string; icon: string; tone: BadgeTone }> = {
  WithinSla: { label: 'Within SLA', icon: '✓', tone: 'info' },
  AtRisk: { label: 'At risk', icon: '!', tone: 'warning' },
  Breached: { label: 'Breached', icon: '✕', tone: 'danger' },
  Met: { label: 'Met', icon: '★', tone: 'success' },
  NotApplicable: { label: 'No SLA', icon: '–', tone: 'neutral' },
};

interface SlaIndicatorProps {
  status: SlaStatus;
  dueAtUtc?: string | null;
}

/** Where a ticket stands against its SLA, with its due date. Used by the list and the detail page. */
export function SlaIndicator({ status, dueAtUtc }: SlaIndicatorProps) {
  const { label, icon, tone } = appearance[status];

  return (
    <div className="sla-cell">
      <Badge tone={tone}>
        <span className="sla-cell__icon" aria-hidden="true">
          {icon}
        </span>
        {label}
      </Badge>
      {dueAtUtc && <span className="sla-cell__due">Due {formatDateTime(dueAtUtc)}</span>}
    </div>
  );
}
