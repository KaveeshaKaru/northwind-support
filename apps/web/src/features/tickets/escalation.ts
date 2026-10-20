import type { EscalateTicketPayload, TicketDetail } from '../../types/api';

/** The same limits the API enforces, so an invalid escalation never reaches it. */
export const REASON_MIN_LENGTH = 5;
export const REASON_MAX_LENGTH = 500;
export const ESCALATED_BY_MAX_LENGTH = 100;

/** Why a ticket cannot be escalated, and what the agent can do instead. */
export interface EscalationBlocker {
  reason: string;
  nextStep: string;
}

/**
 * Why this ticket cannot be escalated, or null when it can. Mirrors the server's rule so the
 * control can be hidden; the server still has the final say.
 */
export function escalationBlocker(
  ticket: Pick<TicketDetail, 'priority' | 'status'>,
): EscalationBlocker | null {
  if (ticket.status === 'Resolved' || ticket.status === 'Closed') {
    return {
      reason: `${ticket.status} tickets cannot be escalated.`,
      nextStep: 'If the problem has come back, move it back to Open first.',
    };
  }

  if (ticket.priority === 'Critical') {
    return {
      reason: 'This ticket is already Critical and cannot be escalated further.',
      nextStep: 'It already has the shortest SLA; reassign it if it needs more attention.',
    };
  }

  return null;
}

/** Field name to message, for each field that breaks the API's rules. */
export function validateEscalation(
  form: EscalateTicketPayload,
): Partial<Record<keyof EscalateTicketPayload, string>> {
  const errors: Partial<Record<keyof EscalateTicketPayload, string>> = {};
  const reason = form.reason.trim();

  if (reason.length < REASON_MIN_LENGTH || reason.length > REASON_MAX_LENGTH) {
    errors.reason = `The reason must be between ${REASON_MIN_LENGTH} and ${REASON_MAX_LENGTH} characters.`;
  }

  const escalatedBy = form.escalatedBy.trim();

  if (escalatedBy.length === 0) {
    errors.escalatedBy = 'Say who is escalating the ticket.';
  } else if (escalatedBy.length > ESCALATED_BY_MAX_LENGTH) {
    errors.escalatedBy = `At most ${ESCALATED_BY_MAX_LENGTH} characters.`;
  }

  return errors;
}
