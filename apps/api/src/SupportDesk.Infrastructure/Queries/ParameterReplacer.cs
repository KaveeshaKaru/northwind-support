using System.Linq.Expressions;

namespace SupportDesk.Infrastructure.Queries;

/// <summary>
/// Rewrites an expression with some of its parameters replaced, e.g. a rule's "due date" input
/// by the ticket's DueAtUtc column, so a rule written once in the domain can run inside a query.
/// </summary>
internal sealed class ParameterReplacer(IReadOnlyDictionary<ParameterExpression, Expression> replacements)
    : ExpressionVisitor
{
    protected override Expression VisitParameter(ParameterExpression node) =>
        replacements.TryGetValue(node, out var replacement) ? replacement : base.VisitParameter(node);
}
