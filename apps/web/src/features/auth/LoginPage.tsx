import { useState } from 'react';
import { Navigate, useLocation, type Location } from 'react-router-dom';
import { authApi } from '../../api/auth';
import { toErrorMessage } from '../../api/client';
import { startSession } from '../../auth/session';
import { useAuth } from '../../auth/useAuth';
import { Field } from '../../components/Field';
import type { LoginPayload } from '../../types/api';

type LoginErrors = Partial<Record<keyof LoginPayload, string>>;

const emailPattern = /^[^\s@]+@[^\s@]+$/;

function validate({ email, password }: LoginPayload): LoginErrors {
  const errors: LoginErrors = {};

  if (email.trim().length === 0) {
    errors.email = 'Enter your email address.';
  } else if (!emailPattern.test(email.trim())) {
    errors.email = 'Enter a valid email address.';
  }

  if (password.length === 0) {
    errors.password = 'Enter your password.';
  }

  return errors;
}

/** Where to go after signing in: the page the agent was sent away from, or the ticket list. */
function destination(state: unknown): string {
  const from = (state as { from?: Location } | null)?.from;

  return from ? `${from.pathname}${from.search}${from.hash}` : '/tickets';
}

export function LoginPage() {
  const { session, ended } = useAuth();
  const location = useLocation();
  const [form, setForm] = useState<LoginPayload>({ email: '', password: '' });
  const [errors, setErrors] = useState<LoginErrors>({});
  const [submitError, setSubmitError] = useState<string>();
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [showPassword, setShowPassword] = useState(false);

  // Signing in starts the session, which re-renders this page into the redirect.
  if (session) {
    return <Navigate to={destination(location.state)} replace />;
  }

  const submit = async (event: React.FormEvent<HTMLFormElement>) => {
    event.preventDefault();

    const validationErrors = validate(form);
    setErrors(validationErrors);
    setSubmitError(undefined);

    if (Object.keys(validationErrors).length > 0) {
      return;
    }

    setIsSubmitting(true);

    try {
      startSession(await authApi.login({ email: form.email.trim(), password: form.password }));
    } catch (caught) {
      setForm((current) => ({ ...current, password: '' }));
      setSubmitError(toErrorMessage(caught, 'Could not sign in.'));
      setIsSubmitting(false);
    }
  };

  return (
    <main className="login">
      <section className="login__intro" aria-label="About Northwind Support">
        <p className="login__logo">
          <span className="brand-mark brand-mark--inverse" aria-hidden="true">
            N
          </span>
          Northwind Support
        </p>

        <div>
          <h2 className="login__headline">Every ticket answered on time.</h2>
          <p className="login__lead">
            The support desk for the Northwind team: triage, SLAs and escalations in one place.
          </p>
        </div>

        <ul className="login__points">
          <li>New tickets triaged and assigned automatically</li>
          <li>At-risk SLAs flagged before they breach</li>
          <li>Every escalation recorded with who and why</li>
        </ul>
      </section>

      <section className="login__panel">
        <form className="login__form" onSubmit={submit} noValidate aria-labelledby="login-heading">
          <h1 id="login-heading">Sign in</h1>
          <p className="login__subtitle">Use your Northwind agent account.</p>

          {ended === 'expired' && (
            <p className="login__banner login__banner--info" role="status">
              Your session has expired. Sign in again.
            </p>
          )}

          {submitError && (
            <p className="login__banner login__banner--error" role="alert">
              {submitError}
            </p>
          )}

          <Field id="login-email" label="Email" error={errors.email}>
            {(fieldProps) => (
              <input
                {...fieldProps}
                type="email"
                autoComplete="username"
                placeholder="name@northwind-support.example"
                value={form.email}
                disabled={isSubmitting}
                onChange={(event) =>
                  setForm((current) => ({ ...current, email: event.target.value }))
                }
              />
            )}
          </Field>

          <Field id="login-password" label="Password" error={errors.password}>
            {(fieldProps) => (
              <div className="login__password">
                <input
                  {...fieldProps}
                  type={showPassword ? 'text' : 'password'}
                  autoComplete="current-password"
                  value={form.password}
                  disabled={isSubmitting}
                  onChange={(event) =>
                    setForm((current) => ({ ...current, password: event.target.value }))
                  }
                />
                <button
                  type="button"
                  className="login__reveal"
                  aria-label={showPassword ? 'Hide password' : 'Show password'}
                  disabled={isSubmitting}
                  onClick={() => setShowPassword((shown) => !shown)}
                >
                  {showPassword ? 'Hide' : 'Show'}
                </button>
              </div>
            )}
          </Field>

          <button
            type="submit"
            className="button button--primary login__submit"
            disabled={isSubmitting}
          >
            {isSubmitting && <span className="login__spinner" aria-hidden="true" />}
            {isSubmitting ? 'Signing in...' : 'Sign in'}
          </button>
        </form>
      </section>
    </main>
  );
}
