using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Query.SqlExpressions;
using Microsoft.EntityFrameworkCore.Storage;

namespace SupportDesk.Infrastructure.Queries;

/// <summary>
/// Date arithmetic for queries that must run in the database. EF Core has no translation for
/// "hours between two dates" that works on both SQL Server (the app) and SQLite (the tests), so
/// this maps one method to each database's own SQL.
/// </summary>
public static class SqlDate
{
    private const string SqliteProvider = "Microsoft.EntityFrameworkCore.Sqlite";

    /// <summary>
    /// Hours from <paramref name="startUtc"/> to <paramref name="endUtc"/>; null when the end is.
    /// Only usable inside a LINQ query that EF Core translates.
    /// </summary>
    public static double? HoursBetween(DateTime startUtc, DateTime? endUtc) =>
        throw new NotSupportedException($"{nameof(HoursBetween)} only runs inside a database query.");

    /// <summary>Teaches the model how the current database spells <see cref="HoursBetween"/>.</summary>
    internal static void Register(ModelBuilder modelBuilder, string? providerName)
    {
        var function = modelBuilder.HasDbFunction(typeof(SqlDate).GetMethod(nameof(HoursBetween))!);

        if (providerName == SqliteProvider)
        {
            // (julianday(end) - julianday(start)) * 24
            var real = new DoubleTypeMapping("REAL");
            function.HasTranslation(args => new SqlBinaryExpression(
                ExpressionType.Multiply,
                new SqlBinaryExpression(
                    ExpressionType.Subtract,
                    JulianDay(args[1], real),
                    JulianDay(args[0], real),
                    typeof(double),
                    real),
                new SqlConstantExpression(24.0, typeof(double), real),
                typeof(double),
                real));
        }
        else
        {
            // CAST(DATEDIFF_BIG(second, start, end) AS float) / 3600.0 - seconds, so part-hours are
            // kept. The cast matters: bigint / 3600.0 would be a decimal, which EF can't read as a double.
            var @float = new DoubleTypeMapping("float");
            function.HasTranslation(args => new SqlBinaryExpression(
                ExpressionType.Divide,
                new SqlUnaryExpression(
                    ExpressionType.Convert,
                    new SqlFunctionExpression(
                        "DATEDIFF_BIG",
                        [new SqlFragmentExpression("second"), args[0], args[1]],
                        nullable: true,
                        argumentsPropagateNullability: [false, true, true],
                        typeof(long),
                        new LongTypeMapping("bigint")),
                    typeof(double),
                    @float),
                new SqlConstantExpression(3600.0, typeof(double), @float),
                typeof(double),
                @float));
        }
    }

    private static SqlFunctionExpression JulianDay(SqlExpression date, RelationalTypeMapping real) =>
        new("julianday", [date], nullable: true, argumentsPropagateNullability: [true], typeof(double), real);
}
