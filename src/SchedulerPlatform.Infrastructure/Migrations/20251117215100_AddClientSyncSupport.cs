using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SchedulerPlatform.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddClientSyncSupport : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ScheduleSyncSources_Clients_ClientId",
                table: "ScheduleSyncSources");

            migrationBuilder.DropIndex(
                name: "IX_ScheduleSyncSources_ClientId_VendorId_AccountNumber",
                table: "ScheduleSyncSources");

            migrationBuilder.RenameColumn(
                name: "ClientCode",
                table: "Clients",
                newName: "ExternalClientId");

            migrationBuilder.RenameIndex(
                name: "IX_Clients_ClientCode",
                table: "Clients",
                newName: "IX_Clients_ExternalClientId");

            migrationBuilder.AddColumn<DateTime>(
                name: "LastSyncedAt",
                table: "Clients",
                type: "datetime2",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Clients_LastSyncedAt",
                table: "Clients",
                column: "LastSyncedAt");

            migrationBuilder.RenameColumn(
                name: "AccountId",
                table: "ScheduleSyncSources",
                newName: "ExternalAccountId");

            migrationBuilder.RenameColumn(
                name: "VendorId",
                table: "ScheduleSyncSources",
                newName: "ExternalVendorId");

            migrationBuilder.RenameIndex(
                name: "IX_ScheduleSyncSources_AccountId",
                table: "ScheduleSyncSources",
                newName: "IX_ScheduleSyncSources_ExternalAccountId");

            migrationBuilder.RenameIndex(
                name: "IX_ScheduleSyncSources_VendorId",
                table: "ScheduleSyncSources",
                newName: "IX_ScheduleSyncSources_ExternalVendorId");

            migrationBuilder.AddColumn<int>(
                name: "ExternalClientId",
                table: "ScheduleSyncSources",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AlterColumn<int>(
                name: "ClientId",
                table: "ScheduleSyncSources",
                type: "int",
                nullable: true,
                oldClrType: typeof(long),
                oldType: "bigint");

            migrationBuilder.CreateIndex(
                name: "IX_ScheduleSyncSources_ExternalClientId",
                table: "ScheduleSyncSources",
                column: "ExternalClientId");

            migrationBuilder.CreateIndex(
                name: "IX_ScheduleSyncSources_ClientId_ExternalVendorId_AccountNumber",
                table: "ScheduleSyncSources",
                columns: new[] { "ExternalClientId", "ExternalVendorId", "AccountNumber" });

            migrationBuilder.AddForeignKey(
                name: "FK_ScheduleSyncSources_Clients_ClientId",
                table: "ScheduleSyncSources",
                column: "ClientId",
                principalTable: "Clients",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ScheduleSyncSources_Clients_ClientId",
                table: "ScheduleSyncSources");

            migrationBuilder.DropIndex(
                name: "IX_ScheduleSyncSources_ExternalClientId",
                table: "ScheduleSyncSources");

            migrationBuilder.DropIndex(
                name: "IX_ScheduleSyncSources_ClientId_ExternalVendorId_AccountNumber",
                table: "ScheduleSyncSources");

            migrationBuilder.DropIndex(
                name: "IX_Clients_LastSyncedAt",
                table: "Clients");

            migrationBuilder.DropColumn(
                name: "LastSyncedAt",
                table: "Clients");

            migrationBuilder.RenameColumn(
                name: "ExternalClientId",
                table: "Clients",
                newName: "ClientCode");

            migrationBuilder.RenameIndex(
                name: "IX_Clients_ExternalClientId",
                table: "Clients",
                newName: "IX_Clients_ClientCode");

            migrationBuilder.DropColumn(
                name: "ExternalClientId",
                table: "ScheduleSyncSources");

            migrationBuilder.RenameColumn(
                name: "ExternalAccountId",
                table: "ScheduleSyncSources",
                newName: "AccountId");

            migrationBuilder.RenameColumn(
                name: "ExternalVendorId",
                table: "ScheduleSyncSources",
                newName: "VendorId");

            migrationBuilder.RenameIndex(
                name: "IX_ScheduleSyncSources_ExternalAccountId",
                table: "ScheduleSyncSources",
                newName: "IX_ScheduleSyncSources_AccountId");

            migrationBuilder.RenameIndex(
                name: "IX_ScheduleSyncSources_ExternalVendorId",
                table: "ScheduleSyncSources",
                newName: "IX_ScheduleSyncSources_VendorId");

            migrationBuilder.AlterColumn<long>(
                name: "ClientId",
                table: "ScheduleSyncSources",
                type: "bigint",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ScheduleSyncSources_ClientId_VendorId_AccountNumber",
                table: "ScheduleSyncSources",
                columns: new[] { "ClientId", "VendorId", "AccountNumber" });

            migrationBuilder.AddForeignKey(
                name: "FK_ScheduleSyncSources_Clients_ClientId",
                table: "ScheduleSyncSources",
                column: "ClientId",
                principalTable: "Clients",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
