using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SchedulerPlatform.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class UpdateScheduleSyncSourceForApiIntegration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ScheduleSyncSources_ClientId_Vendor_AccountNumber",
                table: "ScheduleSyncSources");

            migrationBuilder.DropIndex(
                name: "IX_ScheduleSyncSources_ScheduleDate",
                table: "ScheduleSyncSources");

            migrationBuilder.DropIndex(
                name: "IX_ScheduleSyncSources_ScheduleFrequency",
                table: "ScheduleSyncSources");

            migrationBuilder.RenameColumn(
                name: "Id",
                table: "ScheduleSyncSources",
                newName: "SyncId");

            migrationBuilder.DropColumn(
                name: "Vendor",
                table: "ScheduleSyncSources");

            migrationBuilder.DropColumn(
                name: "ScheduleDate",
                table: "ScheduleSyncSources");

            migrationBuilder.AlterColumn<long>(
                name: "ClientId",
                table: "ScheduleSyncSources",
                type: "bigint",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AlterColumn<string>(
                name: "AccountNumber",
                table: "ScheduleSyncSources",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(100)",
                oldMaxLength: 100);

            migrationBuilder.AddColumn<long>(
                name: "AccountId",
                table: "ScheduleSyncSources",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<long>(
                name: "VendorId",
                table: "ScheduleSyncSources",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastInvoiceDate",
                table: "ScheduleSyncSources",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<string>(
                name: "AccountName",
                table: "ScheduleSyncSources",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "VendorName",
                table: "ScheduleSyncSources",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ClientName",
                table: "ScheduleSyncSources",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TandemAccountId",
                table: "ScheduleSyncSources",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastSyncedAt",
                table: "ScheduleSyncSources",
                type: "datetime2",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ScheduleSyncSources_AccountId",
                table: "ScheduleSyncSources",
                column: "AccountId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ScheduleSyncSources_VendorId",
                table: "ScheduleSyncSources",
                column: "VendorId");

            migrationBuilder.CreateIndex(
                name: "IX_ScheduleSyncSources_LastSyncedAt",
                table: "ScheduleSyncSources",
                column: "LastSyncedAt");

            migrationBuilder.CreateIndex(
                name: "IX_ScheduleSyncSources_ClientId_VendorId_AccountNumber",
                table: "ScheduleSyncSources",
                columns: new[] { "ClientId", "VendorId", "AccountNumber" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ScheduleSyncSources_AccountId",
                table: "ScheduleSyncSources");

            migrationBuilder.DropIndex(
                name: "IX_ScheduleSyncSources_VendorId",
                table: "ScheduleSyncSources");

            migrationBuilder.DropIndex(
                name: "IX_ScheduleSyncSources_LastSyncedAt",
                table: "ScheduleSyncSources");

            migrationBuilder.DropIndex(
                name: "IX_ScheduleSyncSources_ClientId_VendorId_AccountNumber",
                table: "ScheduleSyncSources");

            migrationBuilder.DropColumn(
                name: "AccountId",
                table: "ScheduleSyncSources");

            migrationBuilder.DropColumn(
                name: "VendorId",
                table: "ScheduleSyncSources");

            migrationBuilder.DropColumn(
                name: "LastInvoiceDate",
                table: "ScheduleSyncSources");

            migrationBuilder.DropColumn(
                name: "AccountName",
                table: "ScheduleSyncSources");

            migrationBuilder.DropColumn(
                name: "VendorName",
                table: "ScheduleSyncSources");

            migrationBuilder.DropColumn(
                name: "ClientName",
                table: "ScheduleSyncSources");

            migrationBuilder.DropColumn(
                name: "TandemAccountId",
                table: "ScheduleSyncSources");

            migrationBuilder.DropColumn(
                name: "LastSyncedAt",
                table: "ScheduleSyncSources");

            migrationBuilder.RenameColumn(
                name: "SyncId",
                table: "ScheduleSyncSources",
                newName: "Id");

            migrationBuilder.AlterColumn<int>(
                name: "ClientId",
                table: "ScheduleSyncSources",
                type: "int",
                nullable: false,
                oldClrType: typeof(long),
                oldType: "bigint");

            migrationBuilder.AlterColumn<string>(
                name: "AccountNumber",
                table: "ScheduleSyncSources",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(128)",
                oldMaxLength: 128);

            migrationBuilder.AddColumn<string>(
                name: "Vendor",
                table: "ScheduleSyncSources",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTime>(
                name: "ScheduleDate",
                table: "ScheduleSyncSources",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.CreateIndex(
                name: "IX_ScheduleSyncSources_ClientId_Vendor_AccountNumber",
                table: "ScheduleSyncSources",
                columns: new[] { "ClientId", "Vendor", "AccountNumber" });

            migrationBuilder.CreateIndex(
                name: "IX_ScheduleSyncSources_ScheduleDate",
                table: "ScheduleSyncSources",
                column: "ScheduleDate");

            migrationBuilder.CreateIndex(
                name: "IX_ScheduleSyncSources_ScheduleFrequency",
                table: "ScheduleSyncSources",
                column: "ScheduleFrequency");
        }
    }
}
