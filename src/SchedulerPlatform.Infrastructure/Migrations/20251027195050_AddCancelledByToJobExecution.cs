using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SchedulerPlatform.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCancelledByToJobExecution : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CancelledBy",
                table: "JobExecutions",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CancelledBy",
                table: "JobExecutions");
        }
    }
}
