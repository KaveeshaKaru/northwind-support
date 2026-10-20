import { useState } from 'react';
import { ApiError, toErrorMessage } from '../../../api/client';
import { ticketsApi } from '../../../api/tickets';
import { Field } from '../../../components/Field';
import type { EscalateTicketPayload, TicketDetail } from '../../../types/api';
import {
  REASON_MAX_LENGTH,
  REASON_MIN_LENGTH,
  escalationBlocker,
  validateEscalation,
} from '../escalation';

interface EscalationPanelProps {
  ticket: TicketDetail;
  onEscalated: (ticket: TicketDetail) => void;
}

const emptyForm: EscalateTicketPayload = { reason: '' };

/**
 * Escalates a ticket with a reason. Checks the API's rules first so an invalid request never
 * leaves the browser, and still shows the server's answer when it refuses - the ticket on screen
 * may be out of date.
 */
export function EscalationPanel({ ticket, onEscalated }: EscalationPanelProps) {
  const [form, setForm] = useState<EscalateTicketPayload>(emptyForm);
  const [errors, setErrors] = useState<Partial<Record<keyof EscalateTicketPayload, string>>>({});
  const [submitError, setSubmitError] = useState<string>();
  const [isSaving, setIsSaving] = useState(false);

  const blocker = escalationBlocker(ticket);

  const submit = async (event: React.FormEvent<HTMLFormElement>) => {
    event.preventDefault();

    const validationErrors = validateEscalation(form);
    setErrors(validationErrors);
    setSubmitError(undefined);

    if (Object.keys(validationErrors).length > 0) {
      return;
    }

    setIsSaving(true);

    try {
      const escalated = await ticketsApi.escalate(ticket.id, { reason: form.reason.trim() });
      setForm(emptyForm);
      onEscalated(escalated);
    } catch (caught) {
      if (caught instanceof ApiError && caught.fieldErrors.Reason) {
        setErrors({ reason: caught.fieldErrors.Reason[0] });
      }

      setSubmitError(toErrorMessage(caught, 'Could not escalate the ticket.'));
    } finally {
      setIsSaving(false);
    }
  };

  return (
    <form className="card form" onSubmit={submit} noValidate aria-labelledby="escalate-heading">
      <h2 id="escalate-heading">Escalate</h2>

      {blocker ? (
        <div className="escalate__blocked">
          <p>{blocker.reason}</p>
          <p className="summary__note">{blocker.nextStep}</p>
        </div>
      ) : (
        <p className="summary__note">
          Raises the priority one level, restarts the SLA from now and re-checks the owner.
        </p>
      )}

      {!blocker && (
        <Field
          id="escalation-reason"
          label="Reason"
          error={errors.reason}
          hint={`Between ${REASON_MIN_LENGTH} and ${REASON_MAX_LENGTH} characters.`}
        >
          {(fieldProps) => (
            <textarea
              {...fieldProps}
              rows={3}
              value={form.reason}
              maxLength={REASON_MAX_LENGTH}
              disabled={isSaving}
              onChange={(event) =>
                setForm((current) => ({
                  ...current,
                  reason: event.target.value,
                }))
              }
            />
          )}
        </Field>
      )}

      {submitError && (
        <p className="field__error" role="alert">
          {submitError}
        </p>
      )}

      {!blocker && (
        <div className="button-row">
          <button type="submit" className="button button--primary" disabled={isSaving}>
            {isSaving ? 'Escalating...' : 'Escalate'}
          </button>
        </div>
      )}
    </form>
  );
}
