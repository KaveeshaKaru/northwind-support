using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace SupportDesk.UnitTests.Infrastructure;

/// <summary>Records the text of every query EF sends, so a test can see what reached the database.</summary>
internal sealed class SqlCommandRecorder : DbCommandInterceptor
{
    public List<string> Commands { get; } = [];

    public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<DbDataReader> result,
        CancellationToken cancellationToken = default)
    {
        Commands.Add(command.CommandText);

        return base.ReaderExecutingAsync(command, eventData, result, cancellationToken);
    }
}
