using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SchedulerPlatform.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RenameColumnsToNamingConventions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_JobExecutions_Schedules_ScheduleId",
                table: "JobExecutions");

            migrationBuilder.DropForeignKey(
                name: "FK_JobParameters_Schedules_ScheduleId",
                table: "JobParameters");

            migrationBuilder.DropForeignKey(
                name: "FK_NotificationSettings_Schedules_ScheduleId",
                table: "NotificationSettings");

            migrationBuilder.DropForeignKey(
                name: "FK_PasswordHistories_Users_UserId",
                table: "PasswordHistories");

            migrationBuilder.DropForeignKey(
                name: "FK_Schedules_Clients_ClientId",
                table: "Schedules");

            migrationBuilder.DropForeignKey(
                name: "FK_ScheduleSyncSources_Clients_ClientId",
                table: "ScheduleSyncSources");

            migrationBuilder.DropForeignKey(
                name: "FK_UserPermissions_Users_UserId",
                table: "UserPermissions");

            migrationBuilder.DropForeignKey(
                name: "FK_Users_Clients_ClientId",
                table: "Users");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Users",
                table: "Users");

            migrationBuilder.DropPrimaryKey(
                name: "PK_UserPermissions",
                table: "UserPermissions");

            migrationBuilder.DropPrimaryKey(
                name: "PK_ScheduleSyncSources",
                table: "ScheduleSyncSources");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Schedules",
                table: "Schedules");

            migrationBuilder.DropPrimaryKey(
                name: "PK_PasswordHistories",
                table: "PasswordHistories");

            migrationBuilder.DropPrimaryKey(
                name: "PK_NotificationSettings",
                table: "NotificationSettings");

            migrationBuilder.DropPrimaryKey(
                name: "PK_JobParameters",
                table: "JobParameters");

            migrationBuilder.DropPrimaryKey(
                name: "PK_JobExecutions",
                table: "JobExecutions");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Clients",
                table: "Clients");

            migrationBuilder.DropPrimaryKey(
                name: "PK_AuditLogs",
                table: "AuditLogs");

            migrationBuilder.RenameTable(
                name: "Users",
                newName: "User");

            migrationBuilder.RenameTable(
                name: "UserPermissions",
                newName: "UserPermission");

            migrationBuilder.RenameTable(
                name: "ScheduleSyncSources",
                newName: "ScheduleSyncSource");

            migrationBuilder.RenameTable(
                name: "Schedules",
                newName: "Schedule");

            migrationBuilder.RenameTable(
                name: "PasswordHistories",
                newName: "PasswordHistory");

            migrationBuilder.RenameTable(
                name: "NotificationSettings",
                newName: "NotificationSetting");

            migrationBuilder.RenameTable(
                name: "JobParameters",
                newName: "JobParameter");

            migrationBuilder.RenameTable(
                name: "JobExecutions",
                newName: "JobExecution");

            migrationBuilder.RenameTable(
                name: "Clients",
                newName: "Client");

            migrationBuilder.RenameTable(
                name: "AuditLogs",
                newName: "AuditLog");

            migrationBuilder.RenameColumn(
                name: "Id",
                table: "User",
                newName: "UserId");

            migrationBuilder.RenameColumn(
                name: "UpdatedBy",
                table: "User",
                newName: "ModifiedBy");

            migrationBuilder.RenameColumn(
                name: "UpdatedAt",
                table: "User",
                newName: "PasswordChangedDateTime");

            migrationBuilder.RenameColumn(
                name: "LastLoginAt",
                table: "User",
                newName: "ModifiedDateTime");

            migrationBuilder.RenameColumn(
                name: "CreatedAt",
                table: "User",
                newName: "CreatedDateTime");

            migrationBuilder.RenameIndex(
                name: "IX_Users_ExternalIssuer_ExternalUserId",
                table: "User",
                newName: "IX_User_ExternalIssuer_ExternalUserId");

            migrationBuilder.RenameIndex(
                name: "IX_Users_Email",
                table: "User",
                newName: "IX_User_Email");

            migrationBuilder.RenameIndex(
                name: "IX_Users_ClientId",
                table: "User",
                newName: "IX_User_ClientId");

            migrationBuilder.RenameColumn(
                name: "Id",
                table: "UserPermission",
                newName: "UserPermissionId");

            migrationBuilder.RenameColumn(
                name: "UpdatedBy",
                table: "UserPermission",
                newName: "ModifiedBy");

            migrationBuilder.RenameColumn(
                name: "UpdatedAt",
                table: "UserPermission",
                newName: "ModifiedDateTime");

            migrationBuilder.RenameColumn(
                name: "CreatedAt",
                table: "UserPermission",
                newName: "CreatedDateTime");

            migrationBuilder.RenameIndex(
                name: "IX_UserPermissions_UserId",
                table: "UserPermission",
                newName: "IX_UserPermission_UserId");

            migrationBuilder.RenameColumn(
                name: "SyncId",
                table: "ScheduleSyncSource",
                newName: "ScheduleSyncSourceId");

            migrationBuilder.RenameColumn(
                name: "UpdatedBy",
                table: "ScheduleSyncSource",
                newName: "ModifiedBy");

            migrationBuilder.RenameColumn(
                name: "UpdatedAt",
                table: "ScheduleSyncSource",
                newName: "ModifiedDateTime");

            migrationBuilder.RenameColumn(
                name: "LastSyncedAt",
                table: "ScheduleSyncSource",
                newName: "LastSyncedDateTime");

            migrationBuilder.RenameColumn(
                name: "LastInvoiceDate",
                table: "ScheduleSyncSource",
                newName: "LastInvoiceDateTime");

            migrationBuilder.RenameColumn(
                name: "CreatedAt",
                table: "ScheduleSyncSource",
                newName: "CreatedDateTime");

            migrationBuilder.RenameIndex(
                name: "IX_ScheduleSyncSources_LastSyncedAt",
                table: "ScheduleSyncSource",
                newName: "IX_ScheduleSyncSource_LastSyncedDateTime");

            migrationBuilder.RenameIndex(
                name: "IX_ScheduleSyncSources_ExternalVendorId",
                table: "ScheduleSyncSource",
                newName: "IX_ScheduleSyncSource_ExternalVendorId");

            migrationBuilder.RenameIndex(
                name: "IX_ScheduleSyncSources_ExternalClientId_ExternalVendorId_AccountNumber",
                table: "ScheduleSyncSource",
                newName: "IX_ScheduleSyncSource_ExternalClientId_ExternalVendorId_AccountNumber");

            migrationBuilder.RenameIndex(
                name: "IX_ScheduleSyncSources_ExternalClientId",
                table: "ScheduleSyncSource",
                newName: "IX_ScheduleSyncSource_ExternalClientId");

            migrationBuilder.RenameIndex(
                name: "IX_ScheduleSyncSources_ExternalAccountId",
                table: "ScheduleSyncSource",
                newName: "IX_ScheduleSyncSource_ExternalAccountId");

            migrationBuilder.RenameIndex(
                name: "IX_ScheduleSyncSources_ClientId",
                table: "ScheduleSyncSource",
                newName: "IX_ScheduleSyncSource_ClientId");

            migrationBuilder.RenameColumn(
                name: "Id",
                table: "Schedule",
                newName: "ScheduleId");

            migrationBuilder.RenameColumn(
                name: "UpdatedBy",
                table: "Schedule",
                newName: "ModifiedBy");

            migrationBuilder.RenameColumn(
                name: "UpdatedAt",
                table: "Schedule",
                newName: "NextRunDateTime");

            migrationBuilder.RenameColumn(
                name: "NextRunTime",
                table: "Schedule",
                newName: "ModifiedDateTime");

            migrationBuilder.RenameColumn(
                name: "LastRunTime",
                table: "Schedule",
                newName: "LastRunDateTime");

            migrationBuilder.RenameColumn(
                name: "CreatedAt",
                table: "Schedule",
                newName: "CreatedDateTime");

            migrationBuilder.RenameIndex(
                name: "IX_Schedules_ClientId",
                table: "Schedule",
                newName: "IX_Schedule_ClientId");

            migrationBuilder.RenameColumn(
                name: "Id",
                table: "PasswordHistory",
                newName: "PasswordHistoryId");

            migrationBuilder.RenameColumn(
                name: "UpdatedBy",
                table: "PasswordHistory",
                newName: "ModifiedBy");

            migrationBuilder.RenameColumn(
                name: "UpdatedAt",
                table: "PasswordHistory",
                newName: "ModifiedDateTime");

            migrationBuilder.RenameColumn(
                name: "CreatedAt",
                table: "PasswordHistory",
                newName: "CreatedDateTime");

            migrationBuilder.RenameColumn(
                name: "ChangedAt",
                table: "PasswordHistory",
                newName: "ChangedDateTime");

            migrationBuilder.RenameIndex(
                name: "IX_PasswordHistories_UserId_ChangedAt",
                table: "PasswordHistory",
                newName: "IX_PasswordHistory_UserId_ChangedDateTime");

            migrationBuilder.RenameColumn(
                name: "Id",
                table: "NotificationSetting",
                newName: "NotificationSettingId");

            migrationBuilder.RenameColumn(
                name: "UpdatedBy",
                table: "NotificationSetting",
                newName: "ModifiedBy");

            migrationBuilder.RenameColumn(
                name: "UpdatedAt",
                table: "NotificationSetting",
                newName: "ModifiedDateTime");

            migrationBuilder.RenameColumn(
                name: "CreatedAt",
                table: "NotificationSetting",
                newName: "CreatedDateTime");

            migrationBuilder.RenameIndex(
                name: "IX_NotificationSettings_ScheduleId",
                table: "NotificationSetting",
                newName: "IX_NotificationSetting_ScheduleId");

            migrationBuilder.RenameColumn(
                name: "Id",
                table: "JobParameter",
                newName: "JobParameterId");

            migrationBuilder.RenameColumn(
                name: "UpdatedBy",
                table: "JobParameter",
                newName: "ModifiedBy");

            migrationBuilder.RenameColumn(
                name: "UpdatedAt",
                table: "JobParameter",
                newName: "ModifiedDateTime");

            migrationBuilder.RenameColumn(
                name: "CreatedAt",
                table: "JobParameter",
                newName: "CreatedDateTime");

            migrationBuilder.RenameIndex(
                name: "IX_JobParameters_ScheduleId",
                table: "JobParameter",
                newName: "IX_JobParameter_ScheduleId");

            migrationBuilder.RenameColumn(
                name: "Id",
                table: "JobExecution",
                newName: "JobExecutionId");

            migrationBuilder.RenameColumn(
                name: "UpdatedBy",
                table: "JobExecution",
                newName: "ModifiedBy");

            migrationBuilder.RenameColumn(
                name: "UpdatedAt",
                table: "JobExecution",
                newName: "ModifiedDateTime");

            migrationBuilder.RenameColumn(
                name: "StartTime",
                table: "JobExecution",
                newName: "StartDateTime");

            migrationBuilder.RenameColumn(
                name: "EndTime",
                table: "JobExecution",
                newName: "EndDateTime");

            migrationBuilder.RenameColumn(
                name: "CreatedAt",
                table: "JobExecution",
                newName: "CreatedDateTime");

            migrationBuilder.RenameIndex(
                name: "IX_JobExecutions_Status",
                table: "JobExecution",
                newName: "IX_JobExecution_Status");

            migrationBuilder.RenameIndex(
                name: "IX_JobExecutions_StartTime",
                table: "JobExecution",
                newName: "IX_JobExecution_StartDateTime");

            migrationBuilder.RenameIndex(
                name: "IX_JobExecutions_ScheduleId",
                table: "JobExecution",
                newName: "IX_JobExecution_ScheduleId");

            migrationBuilder.RenameColumn(
                name: "Id",
                table: "Client",
                newName: "ClientId");

            migrationBuilder.RenameColumn(
                name: "UpdatedBy",
                table: "Client",
                newName: "ModifiedBy");

            migrationBuilder.RenameColumn(
                name: "UpdatedAt",
                table: "Client",
                newName: "ModifiedDateTime");

            migrationBuilder.RenameColumn(
                name: "LastSyncedAt",
                table: "Client",
                newName: "LastSyncedDateTime");

            migrationBuilder.RenameColumn(
                name: "CreatedAt",
                table: "Client",
                newName: "CreatedDateTime");

            migrationBuilder.RenameIndex(
                name: "IX_Clients_LastSyncedAt",
                table: "Client",
                newName: "IX_Client_LastSyncedDateTime");

            migrationBuilder.RenameIndex(
                name: "IX_Clients_ExternalClientId",
                table: "Client",
                newName: "IX_Client_ExternalClientId");

            migrationBuilder.RenameColumn(
                name: "Id",
                table: "AuditLog",
                newName: "AuditLogId");

            migrationBuilder.RenameColumn(
                name: "UpdatedBy",
                table: "AuditLog",
                newName: "ModifiedBy");

            migrationBuilder.RenameColumn(
                name: "UpdatedAt",
                table: "AuditLog",
                newName: "ModifiedDateTime");

            migrationBuilder.RenameColumn(
                name: "Timestamp",
                table: "AuditLog",
                newName: "TimestampDateTime");

            migrationBuilder.RenameColumn(
                name: "CreatedAt",
                table: "AuditLog",
                newName: "CreatedDateTime");

            migrationBuilder.RenameIndex(
                name: "IX_AuditLogs_Timestamp",
                table: "AuditLog",
                newName: "IX_AuditLog_TimestampDateTime");

            migrationBuilder.RenameIndex(
                name: "IX_AuditLogs_EntityType_EntityId",
                table: "AuditLog",
                newName: "IX_AuditLog_EntityType_EntityId");

            migrationBuilder.AlterColumn<long>(
                name: "ClientId",
                table: "User",
                type: "bigint",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AddColumn<DateTime>(
                name: "LastLoginDateTime",
                table: "User",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "MustChangePassword",
                table: "User",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AlterColumn<long>(
                name: "ClientId",
                table: "ScheduleSyncSource",
                type: "bigint",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.AlterColumn<long>(
                name: "ClientId",
                table: "Schedule",
                type: "bigint",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AlterColumn<int>(
                name: "JobExecutionId",
                table: "JobExecution",
                type: "int",
                nullable: false,
                oldClrType: typeof(long),
                oldType: "bigint")
                .Annotation("SqlServer:Identity", "1, 1")
                .OldAnnotation("SqlServer:Identity", "1, 1");

            migrationBuilder.AlterColumn<long>(
                name: "ClientId",
                table: "Client",
                type: "bigint",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int")
                .Annotation("SqlServer:Identity", "1, 1")
                .OldAnnotation("SqlServer:Identity", "1, 1");

            migrationBuilder.AddColumn<string>(
                name: "ClientCode",
                table: "Client",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddPrimaryKey(
                name: "PK_User",
                table: "User",
                column: "UserId");

            migrationBuilder.AddPrimaryKey(
                name: "PK_UserPermission",
                table: "UserPermission",
                column: "UserPermissionId");

            migrationBuilder.AddPrimaryKey(
                name: "PK_ScheduleSyncSource",
                table: "ScheduleSyncSource",
                column: "ScheduleSyncSourceId");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Schedule",
                table: "Schedule",
                column: "ScheduleId");

            migrationBuilder.AddPrimaryKey(
                name: "PK_PasswordHistory",
                table: "PasswordHistory",
                column: "PasswordHistoryId");

            migrationBuilder.AddPrimaryKey(
                name: "PK_NotificationSetting",
                table: "NotificationSetting",
                column: "NotificationSettingId");

            migrationBuilder.AddPrimaryKey(
                name: "PK_JobParameter",
                table: "JobParameter",
                column: "JobParameterId");

            migrationBuilder.AddPrimaryKey(
                name: "PK_JobExecution",
                table: "JobExecution",
                column: "JobExecutionId");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Client",
                table: "Client",
                column: "ClientId");

            migrationBuilder.AddPrimaryKey(
                name: "PK_AuditLog",
                table: "AuditLog",
                column: "AuditLogId");

            migrationBuilder.AddForeignKey(
                name: "FK_JobExecution_Schedule_ScheduleId",
                table: "JobExecution",
                column: "ScheduleId",
                principalTable: "Schedule",
                principalColumn: "ScheduleId",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_JobParameter_Schedule_ScheduleId",
                table: "JobParameter",
                column: "ScheduleId",
                principalTable: "Schedule",
                principalColumn: "ScheduleId",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_NotificationSetting_Schedule_ScheduleId",
                table: "NotificationSetting",
                column: "ScheduleId",
                principalTable: "Schedule",
                principalColumn: "ScheduleId",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_PasswordHistory_User_UserId",
                table: "PasswordHistory",
                column: "UserId",
                principalTable: "User",
                principalColumn: "UserId",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Schedule_Client_ClientId",
                table: "Schedule",
                column: "ClientId",
                principalTable: "Client",
                principalColumn: "ClientId",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ScheduleSyncSource_Client_ClientId",
                table: "ScheduleSyncSource",
                column: "ClientId",
                principalTable: "Client",
                principalColumn: "ClientId",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_User_Client_ClientId",
                table: "User",
                column: "ClientId",
                principalTable: "Client",
                principalColumn: "ClientId",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_UserPermission_User_UserId",
                table: "UserPermission",
                column: "UserId",
                principalTable: "User",
                principalColumn: "UserId",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_JobExecution_Schedule_ScheduleId",
                table: "JobExecution");

            migrationBuilder.DropForeignKey(
                name: "FK_JobParameter_Schedule_ScheduleId",
                table: "JobParameter");

            migrationBuilder.DropForeignKey(
                name: "FK_NotificationSetting_Schedule_ScheduleId",
                table: "NotificationSetting");

            migrationBuilder.DropForeignKey(
                name: "FK_PasswordHistory_User_UserId",
                table: "PasswordHistory");

            migrationBuilder.DropForeignKey(
                name: "FK_Schedule_Client_ClientId",
                table: "Schedule");

            migrationBuilder.DropForeignKey(
                name: "FK_ScheduleSyncSource_Client_ClientId",
                table: "ScheduleSyncSource");

            migrationBuilder.DropForeignKey(
                name: "FK_User_Client_ClientId",
                table: "User");

            migrationBuilder.DropForeignKey(
                name: "FK_UserPermission_User_UserId",
                table: "UserPermission");

            migrationBuilder.DropPrimaryKey(
                name: "PK_UserPermission",
                table: "UserPermission");

            migrationBuilder.DropPrimaryKey(
                name: "PK_User",
                table: "User");

            migrationBuilder.DropPrimaryKey(
                name: "PK_ScheduleSyncSource",
                table: "ScheduleSyncSource");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Schedule",
                table: "Schedule");

            migrationBuilder.DropPrimaryKey(
                name: "PK_PasswordHistory",
                table: "PasswordHistory");

            migrationBuilder.DropPrimaryKey(
                name: "PK_NotificationSetting",
                table: "NotificationSetting");

            migrationBuilder.DropPrimaryKey(
                name: "PK_JobParameter",
                table: "JobParameter");

            migrationBuilder.DropPrimaryKey(
                name: "PK_JobExecution",
                table: "JobExecution");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Client",
                table: "Client");

            migrationBuilder.DropPrimaryKey(
                name: "PK_AuditLog",
                table: "AuditLog");

            migrationBuilder.DropColumn(
                name: "LastLoginDateTime",
                table: "User");

            migrationBuilder.DropColumn(
                name: "MustChangePassword",
                table: "User");

            migrationBuilder.DropColumn(
                name: "ClientCode",
                table: "Client");

            migrationBuilder.RenameTable(
                name: "UserPermission",
                newName: "UserPermissions");

            migrationBuilder.RenameTable(
                name: "User",
                newName: "Users");

            migrationBuilder.RenameTable(
                name: "ScheduleSyncSource",
                newName: "ScheduleSyncSources");

            migrationBuilder.RenameTable(
                name: "Schedule",
                newName: "Schedules");

            migrationBuilder.RenameTable(
                name: "PasswordHistory",
                newName: "PasswordHistories");

            migrationBuilder.RenameTable(
                name: "NotificationSetting",
                newName: "NotificationSettings");

            migrationBuilder.RenameTable(
                name: "JobParameter",
                newName: "JobParameters");

            migrationBuilder.RenameTable(
                name: "JobExecution",
                newName: "JobExecutions");

            migrationBuilder.RenameTable(
                name: "Client",
                newName: "Clients");

            migrationBuilder.RenameTable(
                name: "AuditLog",
                newName: "AuditLogs");

            migrationBuilder.RenameColumn(
                name: "UserPermissionId",
                table: "UserPermissions",
                newName: "Id");

            migrationBuilder.RenameColumn(
                name: "ModifiedDateTime",
                table: "UserPermissions",
                newName: "UpdatedAt");

            migrationBuilder.RenameColumn(
                name: "ModifiedBy",
                table: "UserPermissions",
                newName: "UpdatedBy");

            migrationBuilder.RenameColumn(
                name: "CreatedDateTime",
                table: "UserPermissions",
                newName: "CreatedAt");

            migrationBuilder.RenameIndex(
                name: "IX_UserPermission_UserId",
                table: "UserPermissions",
                newName: "IX_UserPermissions_UserId");

            migrationBuilder.RenameColumn(
                name: "UserId",
                table: "Users",
                newName: "Id");

            migrationBuilder.RenameColumn(
                name: "PasswordChangedDateTime",
                table: "Users",
                newName: "UpdatedAt");

            migrationBuilder.RenameColumn(
                name: "ModifiedDateTime",
                table: "Users",
                newName: "LastLoginAt");

            migrationBuilder.RenameColumn(
                name: "ModifiedBy",
                table: "Users",
                newName: "UpdatedBy");

            migrationBuilder.RenameColumn(
                name: "CreatedDateTime",
                table: "Users",
                newName: "CreatedAt");

            migrationBuilder.RenameIndex(
                name: "IX_User_ExternalIssuer_ExternalUserId",
                table: "Users",
                newName: "IX_Users_ExternalIssuer_ExternalUserId");

            migrationBuilder.RenameIndex(
                name: "IX_User_Email",
                table: "Users",
                newName: "IX_Users_Email");

            migrationBuilder.RenameIndex(
                name: "IX_User_ClientId",
                table: "Users",
                newName: "IX_Users_ClientId");

            migrationBuilder.RenameColumn(
                name: "ScheduleSyncSourceId",
                table: "ScheduleSyncSources",
                newName: "SyncId");

            migrationBuilder.RenameColumn(
                name: "ModifiedDateTime",
                table: "ScheduleSyncSources",
                newName: "UpdatedAt");

            migrationBuilder.RenameColumn(
                name: "ModifiedBy",
                table: "ScheduleSyncSources",
                newName: "UpdatedBy");

            migrationBuilder.RenameColumn(
                name: "LastSyncedDateTime",
                table: "ScheduleSyncSources",
                newName: "LastSyncedAt");

            migrationBuilder.RenameColumn(
                name: "LastInvoiceDateTime",
                table: "ScheduleSyncSources",
                newName: "LastInvoiceDate");

            migrationBuilder.RenameColumn(
                name: "CreatedDateTime",
                table: "ScheduleSyncSources",
                newName: "CreatedAt");

            migrationBuilder.RenameIndex(
                name: "IX_ScheduleSyncSource_LastSyncedDateTime",
                table: "ScheduleSyncSources",
                newName: "IX_ScheduleSyncSources_LastSyncedAt");

            migrationBuilder.RenameIndex(
                name: "IX_ScheduleSyncSource_ExternalVendorId",
                table: "ScheduleSyncSources",
                newName: "IX_ScheduleSyncSources_ExternalVendorId");

            migrationBuilder.RenameIndex(
                name: "IX_ScheduleSyncSource_ExternalClientId_ExternalVendorId_AccountNumber",
                table: "ScheduleSyncSources",
                newName: "IX_ScheduleSyncSources_ExternalClientId_ExternalVendorId_AccountNumber");

            migrationBuilder.RenameIndex(
                name: "IX_ScheduleSyncSource_ExternalClientId",
                table: "ScheduleSyncSources",
                newName: "IX_ScheduleSyncSources_ExternalClientId");

            migrationBuilder.RenameIndex(
                name: "IX_ScheduleSyncSource_ExternalAccountId",
                table: "ScheduleSyncSources",
                newName: "IX_ScheduleSyncSources_ExternalAccountId");

            migrationBuilder.RenameIndex(
                name: "IX_ScheduleSyncSource_ClientId",
                table: "ScheduleSyncSources",
                newName: "IX_ScheduleSyncSources_ClientId");

            migrationBuilder.RenameColumn(
                name: "ScheduleId",
                table: "Schedules",
                newName: "Id");

            migrationBuilder.RenameColumn(
                name: "NextRunDateTime",
                table: "Schedules",
                newName: "UpdatedAt");

            migrationBuilder.RenameColumn(
                name: "ModifiedDateTime",
                table: "Schedules",
                newName: "NextRunTime");

            migrationBuilder.RenameColumn(
                name: "ModifiedBy",
                table: "Schedules",
                newName: "UpdatedBy");

            migrationBuilder.RenameColumn(
                name: "LastRunDateTime",
                table: "Schedules",
                newName: "LastRunTime");

            migrationBuilder.RenameColumn(
                name: "CreatedDateTime",
                table: "Schedules",
                newName: "CreatedAt");

            migrationBuilder.RenameIndex(
                name: "IX_Schedule_ClientId",
                table: "Schedules",
                newName: "IX_Schedules_ClientId");

            migrationBuilder.RenameColumn(
                name: "PasswordHistoryId",
                table: "PasswordHistories",
                newName: "Id");

            migrationBuilder.RenameColumn(
                name: "ModifiedDateTime",
                table: "PasswordHistories",
                newName: "UpdatedAt");

            migrationBuilder.RenameColumn(
                name: "ModifiedBy",
                table: "PasswordHistories",
                newName: "UpdatedBy");

            migrationBuilder.RenameColumn(
                name: "CreatedDateTime",
                table: "PasswordHistories",
                newName: "CreatedAt");

            migrationBuilder.RenameColumn(
                name: "ChangedDateTime",
                table: "PasswordHistories",
                newName: "ChangedAt");

            migrationBuilder.RenameIndex(
                name: "IX_PasswordHistory_UserId_ChangedDateTime",
                table: "PasswordHistories",
                newName: "IX_PasswordHistories_UserId_ChangedAt");

            migrationBuilder.RenameColumn(
                name: "NotificationSettingId",
                table: "NotificationSettings",
                newName: "Id");

            migrationBuilder.RenameColumn(
                name: "ModifiedDateTime",
                table: "NotificationSettings",
                newName: "UpdatedAt");

            migrationBuilder.RenameColumn(
                name: "ModifiedBy",
                table: "NotificationSettings",
                newName: "UpdatedBy");

            migrationBuilder.RenameColumn(
                name: "CreatedDateTime",
                table: "NotificationSettings",
                newName: "CreatedAt");

            migrationBuilder.RenameIndex(
                name: "IX_NotificationSetting_ScheduleId",
                table: "NotificationSettings",
                newName: "IX_NotificationSettings_ScheduleId");

            migrationBuilder.RenameColumn(
                name: "JobParameterId",
                table: "JobParameters",
                newName: "Id");

            migrationBuilder.RenameColumn(
                name: "ModifiedDateTime",
                table: "JobParameters",
                newName: "UpdatedAt");

            migrationBuilder.RenameColumn(
                name: "ModifiedBy",
                table: "JobParameters",
                newName: "UpdatedBy");

            migrationBuilder.RenameColumn(
                name: "CreatedDateTime",
                table: "JobParameters",
                newName: "CreatedAt");

            migrationBuilder.RenameIndex(
                name: "IX_JobParameter_ScheduleId",
                table: "JobParameters",
                newName: "IX_JobParameters_ScheduleId");

            migrationBuilder.RenameColumn(
                name: "JobExecutionId",
                table: "JobExecutions",
                newName: "Id");

            migrationBuilder.RenameColumn(
                name: "StartDateTime",
                table: "JobExecutions",
                newName: "StartTime");

            migrationBuilder.RenameColumn(
                name: "ModifiedDateTime",
                table: "JobExecutions",
                newName: "UpdatedAt");

            migrationBuilder.RenameColumn(
                name: "ModifiedBy",
                table: "JobExecutions",
                newName: "UpdatedBy");

            migrationBuilder.RenameColumn(
                name: "EndDateTime",
                table: "JobExecutions",
                newName: "EndTime");

            migrationBuilder.RenameColumn(
                name: "CreatedDateTime",
                table: "JobExecutions",
                newName: "CreatedAt");

            migrationBuilder.RenameIndex(
                name: "IX_JobExecution_Status",
                table: "JobExecutions",
                newName: "IX_JobExecutions_Status");

            migrationBuilder.RenameIndex(
                name: "IX_JobExecution_StartDateTime",
                table: "JobExecutions",
                newName: "IX_JobExecutions_StartTime");

            migrationBuilder.RenameIndex(
                name: "IX_JobExecution_ScheduleId",
                table: "JobExecutions",
                newName: "IX_JobExecutions_ScheduleId");

            migrationBuilder.RenameColumn(
                name: "ClientId",
                table: "Clients",
                newName: "Id");

            migrationBuilder.RenameColumn(
                name: "ModifiedDateTime",
                table: "Clients",
                newName: "UpdatedAt");

            migrationBuilder.RenameColumn(
                name: "ModifiedBy",
                table: "Clients",
                newName: "UpdatedBy");

            migrationBuilder.RenameColumn(
                name: "LastSyncedDateTime",
                table: "Clients",
                newName: "LastSyncedAt");

            migrationBuilder.RenameColumn(
                name: "CreatedDateTime",
                table: "Clients",
                newName: "CreatedAt");

            migrationBuilder.RenameIndex(
                name: "IX_Client_LastSyncedDateTime",
                table: "Clients",
                newName: "IX_Clients_LastSyncedAt");

            migrationBuilder.RenameIndex(
                name: "IX_Client_ExternalClientId",
                table: "Clients",
                newName: "IX_Clients_ExternalClientId");

            migrationBuilder.RenameColumn(
                name: "AuditLogId",
                table: "AuditLogs",
                newName: "Id");

            migrationBuilder.RenameColumn(
                name: "TimestampDateTime",
                table: "AuditLogs",
                newName: "Timestamp");

            migrationBuilder.RenameColumn(
                name: "ModifiedDateTime",
                table: "AuditLogs",
                newName: "UpdatedAt");

            migrationBuilder.RenameColumn(
                name: "ModifiedBy",
                table: "AuditLogs",
                newName: "UpdatedBy");

            migrationBuilder.RenameColumn(
                name: "CreatedDateTime",
                table: "AuditLogs",
                newName: "CreatedAt");

            migrationBuilder.RenameIndex(
                name: "IX_AuditLog_TimestampDateTime",
                table: "AuditLogs",
                newName: "IX_AuditLogs_Timestamp");

            migrationBuilder.RenameIndex(
                name: "IX_AuditLog_EntityType_EntityId",
                table: "AuditLogs",
                newName: "IX_AuditLogs_EntityType_EntityId");

            migrationBuilder.AlterColumn<int>(
                name: "ClientId",
                table: "Users",
                type: "int",
                nullable: false,
                oldClrType: typeof(long),
                oldType: "bigint");

            migrationBuilder.AlterColumn<int>(
                name: "ClientId",
                table: "ScheduleSyncSources",
                type: "int",
                nullable: true,
                oldClrType: typeof(long),
                oldType: "bigint",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "ClientId",
                table: "Schedules",
                type: "int",
                nullable: false,
                oldClrType: typeof(long),
                oldType: "bigint");

            migrationBuilder.AlterColumn<long>(
                name: "Id",
                table: "JobExecutions",
                type: "bigint",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int")
                .Annotation("SqlServer:Identity", "1, 1")
                .OldAnnotation("SqlServer:Identity", "1, 1");

            migrationBuilder.AlterColumn<int>(
                name: "Id",
                table: "Clients",
                type: "int",
                nullable: false,
                oldClrType: typeof(long),
                oldType: "bigint")
                .Annotation("SqlServer:Identity", "1, 1")
                .OldAnnotation("SqlServer:Identity", "1, 1");

            migrationBuilder.AddPrimaryKey(
                name: "PK_UserPermissions",
                table: "UserPermissions",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Users",
                table: "Users",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_ScheduleSyncSources",
                table: "ScheduleSyncSources",
                column: "SyncId");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Schedules",
                table: "Schedules",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_PasswordHistories",
                table: "PasswordHistories",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_NotificationSettings",
                table: "NotificationSettings",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_JobParameters",
                table: "JobParameters",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_JobExecutions",
                table: "JobExecutions",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Clients",
                table: "Clients",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_AuditLogs",
                table: "AuditLogs",
                column: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_JobExecutions_Schedules_ScheduleId",
                table: "JobExecutions",
                column: "ScheduleId",
                principalTable: "Schedules",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_JobParameters_Schedules_ScheduleId",
                table: "JobParameters",
                column: "ScheduleId",
                principalTable: "Schedules",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_NotificationSettings_Schedules_ScheduleId",
                table: "NotificationSettings",
                column: "ScheduleId",
                principalTable: "Schedules",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_PasswordHistories_Users_UserId",
                table: "PasswordHistories",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Schedules_Clients_ClientId",
                table: "Schedules",
                column: "ClientId",
                principalTable: "Clients",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ScheduleSyncSources_Clients_ClientId",
                table: "ScheduleSyncSources",
                column: "ClientId",
                principalTable: "Clients",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_UserPermissions_Users_UserId",
                table: "UserPermissions",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Users_Clients_ClientId",
                table: "Users",
                column: "ClientId",
                principalTable: "Clients",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
