using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using SupportDesk.Application.Abstractions;
using SupportDesk.Application.Contracts.Common;
using SupportDesk.Application.Contracts.Tickets;
using SupportDesk.Domain.Aggregates.Agents;
using SupportDesk.Domain.Aggregates.Categories;
using SupportDesk.Domain.Aggregates.Customers;
using SupportDesk.Domain.Aggregates.Tickets;
using SupportDesk.Infrastructure.Data;

namespace SupportDesk.Infrastructure.Queries;

/// <summary>
/// Ticket reads. Projected in the database and never tracked. Aggregates hold only each
/// other's ids, so the customer, category and agent labels are joined in here, on the read side.
/// </summary>
public sealed class TicketQueries(SupportDbContext db, IClock clock) : ITicketQueries
{
    /// <remarks>
    /// Filters, counts, sorts and pages in the database: the filters are applied before the
    /// count, so the total and the page always describe the same set.
    /// </remarks>
    public async Task<PagedResult<TicketListItemDto>> GetPagedAsync(TicketQuery query, CancellationToken ct)
    {
        // One "now" for the whole request, so the filter and the status shown agree.
        var now = clock.UtcNow;

        var tickets = ApplyFilters(TicketsWithLabels(), query, now);

        var totalCount = await tickets.CountAsync(ct);

        var items = await ApplySort(tickets, query)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(x => new TicketListItemDto
            {
                Id = x.Ticket.Id,
                Reference = x.Ticket.Reference,
                Title = x.Ticket.Title,
                Status = x.Ticket.Status,
                Priority = x.Ticket.Priority,
                Customer = new CustomerSummaryDto(x.Customer.Id, x.Customer.Name, x.Customer.Tier),
                Category = new CategorySummaryDto(x.Category.Id, x.Category.Name),
                AssignedAgent = x.Agent == null ? null : new AgentSummaryDto(x.Agent.Id, x.Agent.FullName),
                CreatedAtUtc = x.Ticket.CreatedAtUtc,
                UpdatedAtUtc = x.Ticket.UpdatedAtUtc,
                DueAtUtc = x.Ticket.DueAtUtc,
                ResolvedAtUtc = x.Ticket.ResolvedAtUtc,
                SlaStatus = SlaEvaluator.Evaluate(x.Ticket.DueAtUtc, x.Ticket.AtRiskFromUtc, x.Ticket.ResolvedAtUtc, now)
            })
            .ToListAsync(ct);

        return new PagedResult<TicketListItemDto>(items, query.Page, query.PageSize, totalCount);
    }

    public Task<TicketDetailDto?> GetDetailAsync(int id, CancellationToken ct)
    {
        var now = clock.UtcNow;

        return TicketsWithLabels()
            .Where(x => x.Ticket.Id == id)
            .Select(x => new TicketDetailDto
            {
                Id = x.Ticket.Id,
                Reference = x.Ticket.Reference,
                Title = x.Ticket.Title,
                Description = x.Ticket.Description,
                Status = x.Ticket.Status,
                Priority = x.Ticket.Priority,
                Customer = new CustomerContactDto(
                    x.Customer.Id, x.Customer.Name, x.Customer.Email, x.Customer.Phone, x.Customer.Tier),
                Category = new CategoryDto(x.Category.Id, x.Category.Name, x.Category.RequiresSpecialist),
                AssignedAgent = x.Agent == null ? null : new AgentSummaryDto(x.Agent.Id, x.Agent.FullName),
                CreatedAtUtc = x.Ticket.CreatedAtUtc,
                UpdatedAtUtc = x.Ticket.UpdatedAtUtc,
                DueAtUtc = x.Ticket.DueAtUtc,
                ResolvedAtUtc = x.Ticket.ResolvedAtUtc,
                SlaStatus = SlaEvaluator.Evaluate(x.Ticket.DueAtUtc, x.Ticket.AtRiskFromUtc, x.Ticket.ResolvedAtUtc, now)
            })
            .FirstOrDefaultAsync(ct);
    }

    /// <remarks>
    /// Reads through IX_TicketEscalations_TicketId_EscalatedAtUtc, which already holds a
    /// ticket's rows newest first. Agent names are joined in, both sides optional.
    /// </remarks>
    public async Task<IReadOnlyList<EscalationDto>?> GetEscalationsAsync(int ticketId, CancellationToken ct)
    {
        if (!await db.Set<Ticket>().AnyAsync(t => t.Id == ticketId, ct))
        {
            return null;
        }

        return await (
            from escalation in db.Set<TicketEscalation>().AsNoTracking()
            where escalation.TicketId == ticketId
            from fromAgent in db.Set<Agent>().Where(a => a.Id == escalation.FromAgentId).DefaultIfEmpty()
            from toAgent in db.Set<Agent>().Where(a => a.Id == escalation.ToAgentId).DefaultIfEmpty()
            orderby escalation.EscalatedAtUtc descending, escalation.Id descending
            select new EscalationDto(
                escalation.Id,
                escalation.FromPriority,
                escalation.ToPriority,
                fromAgent == null ? null : new AgentSummaryDto(fromAgent.Id, fromAgent.FullName),
                toAgent == null ? null : new AgentSummaryDto(toAgent.Id, toAgent.FullName),
                escalation.FromDueAtUtc,
                escalation.ToDueAtUtc,
                escalation.Reason,
                escalation.EscalatedBy,
                escalation.EscalatedAtUtc))
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<TicketListItemDto>> GetForCustomerAsync(int customerId, CancellationToken ct)
    {
        var now = clock.UtcNow;

        return await TicketsWithLabels()
            .Where(x => x.Ticket.CustomerId == customerId)
            .OrderByDescending(x => x.Ticket.CreatedAtUtc)
            .ThenByDescending(x => x.Ticket.Id)
            .Select(x => new TicketListItemDto
            {
                Id = x.Ticket.Id,
                Reference = x.Ticket.Reference,
                Title = x.Ticket.Title,
                Status = x.Ticket.Status,
                Priority = x.Ticket.Priority,
                Customer = new CustomerSummaryDto(x.Customer.Id, x.Customer.Name, x.Customer.Tier),
                Category = new CategorySummaryDto(x.Category.Id, x.Category.Name),
                AssignedAgent = x.Agent == null ? null : new AgentSummaryDto(x.Agent.Id, x.Agent.FullName),
                CreatedAtUtc = x.Ticket.CreatedAtUtc,
                UpdatedAtUtc = x.Ticket.UpdatedAtUtc,
                DueAtUtc = x.Ticket.DueAtUtc,
                ResolvedAtUtc = x.Ticket.ResolvedAtUtc,
                SlaStatus = SlaEvaluator.Evaluate(x.Ticket.DueAtUtc, x.Ticket.AtRiskFromUtc, x.Ticket.ResolvedAtUtc, now)
            })
            .ToListAsync(ct);
    }

    /// <summary>
    /// Every ticket with the rows that label it. Filter and sort this, then project.
    /// </summary>
    private IQueryable<TicketWithLabels> TicketsWithLabels() =>
        from ticket in db.Set<Ticket>().AsNoTracking()
        join customer in db.Set<Customer>() on ticket.CustomerId equals customer.Id
        join category in db.Set<Category>() on ticket.CategoryId equals category.Id
        from agent in db.Set<Agent>().Where(a => a.Id == ticket.AssignedAgentId).DefaultIfEmpty()
        select new TicketWithLabels { Ticket = ticket, Customer = customer, Category = category, Agent = agent };

    /// <summary>
    /// Narrows the list to the filters that were supplied, combined with AND. Each filter is one
    /// condition here, translated to SQL; a new filter is one more <c>if</c>.
    /// </summary>
    private static IQueryable<TicketWithLabels> ApplyFilters(
        IQueryable<TicketWithLabels> source,
        TicketQuery query,
        DateTime nowUtc)
    {
        if (query.Status is { } status)
        {
            source = source.Where(x => x.Ticket.Status == status);
        }

        if (query.Priority is { } priority)
        {
            source = source.Where(x => x.Ticket.Priority == priority);
        }

        if (query.CategoryId is { } categoryId)
        {
            source = source.Where(x => x.Ticket.CategoryId == categoryId);
        }

        if (query.CustomerId is { } customerId)
        {
            source = source.Where(x => x.Ticket.CustomerId == customerId);
        }

        if (query.AssignedAgentId is { } agentId)
        {
            source = source.Where(x => x.Ticket.AssignedAgentId == agentId);
        }

        if (query.UnassignedOnly)
        {
            source = source.Where(x => x.Ticket.AssignedAgentId == null);
        }

        var search = query.Search?.Trim();

        if (!string.IsNullOrEmpty(search))
        {
            source = source.Where(x =>
                x.Ticket.Title.Contains(search) ||
                x.Ticket.Reference.Contains(search) ||
                x.Customer.Name.Contains(search));
        }

        if (query.SlaStatus is { } slaStatus)
        {
            source = source.Where(HasSlaStatus(slaStatus, nowUtc));
        }

        return source;
    }

    /// <summary>
    /// <see cref="SlaEvaluator.Rule"/> applied to the ticket's own columns and compared with
    /// <paramref name="status"/>: the same rule that decides the status shown, so the filter and
    /// the list cannot disagree. EF Core translates it to a CASE expression in the WHERE clause.
    /// </summary>
    private static Expression<Func<TicketWithLabels, bool>> HasSlaStatus(SlaStatus status, DateTime nowUtc)
    {
        var rule = SlaEvaluator.Rule;
        var row = Expression.Parameter(typeof(TicketWithLabels), "x");
        var ticket = Expression.Property(row, nameof(TicketWithLabels.Ticket));

        // Captured values rather than constants, so they reach SQL as parameters.
        Expression<Func<DateTime>> now = () => nowUtc;
        Expression<Func<SlaStatus>> wanted = () => status;

        var statusOfTicket = new ParameterReplacer(new Dictionary<ParameterExpression, Expression>
        {
            [rule.Parameters[0]] = Expression.Property(ticket, nameof(Ticket.DueAtUtc)),
            [rule.Parameters[1]] = Expression.Property(ticket, nameof(Ticket.AtRiskFromUtc)),
            [rule.Parameters[2]] = Expression.Property(ticket, nameof(Ticket.ResolvedAtUtc)),
            [rule.Parameters[3]] = now.Body
        }).Visit(rule.Body);

        return Expression.Lambda<Func<TicketWithLabels, bool>>(Expression.Equal(statusOfTicket, wanted.Body), row);
    }

    private static IQueryable<TicketWithLabels> ApplySort(IQueryable<TicketWithLabels> source, TicketQuery query)
    {
        var descending = !string.Equals(query.SortDirection, "asc", StringComparison.OrdinalIgnoreCase);

        var sorted = query.SortBy?.ToLowerInvariant() switch
        {
            "updatedatutc" => descending
                ? source.OrderByDescending(x => x.Ticket.UpdatedAtUtc)
                : source.OrderBy(x => x.Ticket.UpdatedAtUtc),
            "dueatutc" => descending
                ? source.OrderByDescending(x => x.Ticket.DueAtUtc)
                : source.OrderBy(x => x.Ticket.DueAtUtc),
            "priority" => descending
                ? source.OrderByDescending(x => x.Ticket.Priority)
                : source.OrderBy(x => x.Ticket.Priority),
            "status" => descending
                ? source.OrderByDescending(x => x.Ticket.Status)
                : source.OrderBy(x => x.Ticket.Status),
            _ => descending
                ? source.OrderByDescending(x => x.Ticket.CreatedAtUtc)
                : source.OrderBy(x => x.Ticket.CreatedAtUtc)
        };

        // Keeps paging stable when the sort column has duplicates.
        return sorted.ThenByDescending(x => x.Ticket.Id);
    }

    private sealed class TicketWithLabels
    {
        public required Ticket Ticket { get; init; }

        public required Customer Customer { get; init; }

        public required Category Category { get; init; }

        public Agent? Agent { get; init; }
    }
}
