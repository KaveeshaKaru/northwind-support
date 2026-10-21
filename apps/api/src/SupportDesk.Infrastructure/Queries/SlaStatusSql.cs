using System.Linq.Expressions;
using SupportDesk.Domain.Aggregates.Tickets;

namespace SupportDesk.Infrastructure.Queries;

/// <summary>
/// <see cref="SlaEvaluator.Rule"/> applied to a ticket's own columns, as an expression EF Core
/// translates to a CASE. The list filter and the SLA report both use it, so neither can work out
/// a ticket's status differently from what the ticket shows.
/// </summary>
internal static class SlaStatusSql
{
    /// <param name="ticket">An expression of type <see cref="Ticket"/> inside a query.</param>
    /// <param name="nowUtc">"Now" for the whole query; it reaches SQL as a parameter.</param>
    public static Expression StatusOf(Expression ticket, DateTime nowUtc)
    {
        var rule = SlaEvaluator.Rule;

        // A captured value rather than a constant, so it reaches SQL as a parameter.
        Expression<Func<DateTime>> now = () => nowUtc;

        return new ParameterReplacer(new Dictionary<ParameterExpression, Expression>
        {
            [rule.Parameters[0]] = Expression.Property(ticket, nameof(Ticket.DueAtUtc)),
            [rule.Parameters[1]] = Expression.Property(ticket, nameof(Ticket.AtRiskFromUtc)),
            [rule.Parameters[2]] = Expression.Property(ticket, nameof(Ticket.ResolvedAtUtc)),
            [rule.Parameters[3]] = now.Body
        }).Visit(rule.Body);
    }
}
