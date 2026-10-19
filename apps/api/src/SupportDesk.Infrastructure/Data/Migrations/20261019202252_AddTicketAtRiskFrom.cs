using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SupportDesk.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddTicketAtRiskFrom : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "AtRiskFromUtc",
                table: "Tickets",
                type: "datetime2(3)",
                nullable: true);

            // Existing tickets got their due date when they were raised, so their window runs
            // from CreatedAtUtc to DueAtUtc. They become at risk for the last 25% of it - the
            // threshold configured when this migration was written.
            migrationBuilder.Sql("""
                UPDATE Tickets
                SET AtRiskFromUtc = DATEADD(second, -DATEDIFF(second, CreatedAtUtc, DueAtUtc) / 4, DueAtUtc)
                WHERE DueAtUtc IS NOT NULL;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AtRiskFromUtc",
                table: "Tickets");
        }
    }
}
