using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SchedulerPlatform.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCredentialIdToScheduleSyncSource : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CredentialId",
                table: "ScheduleSyncSources",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_ScheduleSyncSources_CredentialId",
                table: "ScheduleSyncSources",
                column: "CredentialId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ScheduleSyncSources_CredentialId",
                table: "ScheduleSyncSources");

            migrationBuilder.DropColumn(
                name: "CredentialId",
                table: "ScheduleSyncSources");
        }
    }
}
