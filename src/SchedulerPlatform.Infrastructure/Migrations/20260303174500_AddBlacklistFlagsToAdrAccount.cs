using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SchedulerPlatform.Infrastructure.Migrations
{
    /// <summary>
    /// Adds denormalized blacklist flags (IsCurrentlyBlacklisted, IsFutureBlacklisted) to AdrAccount.
    /// These flags are populated during Account Sync and allow all queries to filter by simple
    /// indexed boolean columns instead of running expensive blacklist table joins/subqueries.
    /// This dramatically improves Dashboard, Accounts, Jobs, and Rules page load times.
    /// </summary>
    public partial class AddBlacklistFlagsToAdrAccount : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Add IsCurrentlyBlacklisted column with default false
            migrationBuilder.AddColumn<bool>(
                name: "IsCurrentlyBlacklisted",
                table: "AdrAccount",
                type: "bit",
                nullable: false,
                defaultValue: false);

            // Add IsFutureBlacklisted column with default false
            migrationBuilder.AddColumn<bool>(
                name: "IsFutureBlacklisted",
                table: "AdrAccount",
                type: "bit",
                nullable: false,
                defaultValue: false);

            // Index on IsCurrentlyBlacklisted for fast filtering
            migrationBuilder.CreateIndex(
                name: "IX_AdrAccount_IsCurrentlyBlacklisted",
                table: "AdrAccount",
                column: "IsCurrentlyBlacklisted");

            // Index on IsFutureBlacklisted for fast filtering
            migrationBuilder.CreateIndex(
                name: "IX_AdrAccount_IsFutureBlacklisted",
                table: "AdrAccount",
                column: "IsFutureBlacklisted");

            // Composite index: IsDeleted + IsCurrentlyBlacklisted + NextRunStatus
            // Optimizes the most common query pattern: active, non-blacklisted accounts by status
            migrationBuilder.CreateIndex(
                name: "IX_AdrAccount_IsDeleted_IsCurrentlyBlacklisted_NextRunStatus",
                table: "AdrAccount",
                columns: new[] { "IsDeleted", "IsCurrentlyBlacklisted", "NextRunStatus" });

            // Composite index: IsDeleted + IsCurrentlyBlacklisted + HistoricalBillingStatus
            // Optimizes missing accounts queries that exclude blacklisted
            migrationBuilder.CreateIndex(
                name: "IX_AdrAccount_IsDeleted_IsCurrentlyBlacklisted_HistoricalBillingStatus",
                table: "AdrAccount",
                columns: new[] { "IsDeleted", "IsCurrentlyBlacklisted", "HistoricalBillingStatus" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(name: "IX_AdrAccount_IsDeleted_IsCurrentlyBlacklisted_HistoricalBillingStatus", table: "AdrAccount");
            migrationBuilder.DropIndex(name: "IX_AdrAccount_IsDeleted_IsCurrentlyBlacklisted_NextRunStatus", table: "AdrAccount");
            migrationBuilder.DropIndex(name: "IX_AdrAccount_IsFutureBlacklisted", table: "AdrAccount");
            migrationBuilder.DropIndex(name: "IX_AdrAccount_IsCurrentlyBlacklisted", table: "AdrAccount");

            migrationBuilder.DropColumn(
                name: "IsFutureBlacklisted",
                table: "AdrAccount");

            migrationBuilder.DropColumn(
                name: "IsCurrentlyBlacklisted",
                table: "AdrAccount");
        }
    }
}
