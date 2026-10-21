import { render, screen } from '@testing-library/react';
import { describe, expect, it } from 'vitest';
import type { SlaStatus } from '../../../types/api';
import { SlaIndicator } from './SlaIndicator';

const states: [SlaStatus, string][] = [
  ['WithinSla', 'Within SLA'],
  ['AtRisk', 'At risk'],
  ['Breached', 'Breached'],
  ['Met', 'Met'],
  ['NotApplicable', 'No SLA'],
];

describe('SlaIndicator', () => {
  it.each(states)('labels %s as "%s"', (status, label) => {
    render(<SlaIndicator status={status} />);

    expect(screen.getByText(label)).toBeInTheDocument();
  });

  it('gives every state its own label and its own icon, so colour is never the only signal', () => {
    const rendered = states.map(([status]) => {
      const { container, unmount } = render(<SlaIndicator status={status} />);
      const icon = container.querySelector('[aria-hidden="true"]');
      const result = { text: container.textContent, icon: icon?.textContent };
      unmount();
      return result;
    });

    expect(new Set(rendered.map((r) => r.text)).size).toBe(states.length);
    expect(new Set(rendered.map((r) => r.icon)).size).toBe(states.length);
    rendered.forEach((r) => expect(r.icon).toBeTruthy());
  });

  it('shows the due date when there is one', () => {
    render(<SlaIndicator status="AtRisk" dueAtUtc="2026-09-24T16:12:00Z" />);

    expect(screen.getByText(/2026/)).toBeInTheDocument();
  });

  it('shows no date when there is no SLA', () => {
    const { container } = render(<SlaIndicator status="NotApplicable" dueAtUtc={null} />);

    expect(container.textContent).not.toMatch(/\d{4}/);
  });
});
