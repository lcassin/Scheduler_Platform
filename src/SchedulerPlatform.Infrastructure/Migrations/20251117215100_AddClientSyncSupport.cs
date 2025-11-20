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
            migrationBuilder.Sql(@"
                IF EXISTS (SELECT 1 FROM sys.foreign_keys 
                           WHERE name = N'FK_ScheduleSyncSources_Clients_ClientId'
                             AND parent_object_id = OBJECT_ID(N'[dbo].[ScheduleSyncSources]'))
                BEGIN
                    ALTER TABLE [dbo].[ScheduleSyncSources] DROP CONSTRAINT [FK_ScheduleSyncSources_Clients_ClientId];
                END
            ");

            migrationBuilder.Sql(@"
                IF EXISTS (SELECT 1 FROM sys.indexes 
                           WHERE name = N'IX_ScheduleSyncSources_ClientId_Vendor_AccountNumber'
                             AND object_id = OBJECT_ID(N'[dbo].[ScheduleSyncSources]'))
                BEGIN
                    DROP INDEX [IX_ScheduleSyncSources_ClientId_Vendor_AccountNumber] ON [dbo].[ScheduleSyncSources];
                END
                IF EXISTS (SELECT 1 FROM sys.indexes 
                           WHERE name = N'IX_ScheduleSyncSources_ClientId_VendorId_AccountNumber'
                             AND object_id = OBJECT_ID(N'[dbo].[ScheduleSyncSources]'))
                BEGIN
                    DROP INDEX [IX_ScheduleSyncSources_ClientId_VendorId_AccountNumber] ON [dbo].[ScheduleSyncSources];
                END
            ");

            migrationBuilder.Sql(@"
                IF EXISTS (SELECT 1 FROM sys.columns 
                           WHERE name = N'ClientCode' 
                             AND object_id = OBJECT_ID(N'[dbo].[Clients]'))
                BEGIN
                    EXEC sp_rename N'[dbo].[Clients].[ClientCode]', N'ExternalClientId', 'COLUMN';
                END
            ");

            migrationBuilder.Sql(@"
                IF EXISTS (SELECT 1 FROM sys.indexes 
                           WHERE name = N'IX_Clients_ClientCode'
                             AND object_id = OBJECT_ID(N'[dbo].[Clients]'))
                BEGIN
                    EXEC sp_rename N'[dbo].[Clients].[IX_Clients_ClientCode]', N'IX_Clients_ExternalClientId', 'INDEX';
                END
            ");

            migrationBuilder.Sql(@"
                IF NOT EXISTS (SELECT 1 FROM sys.columns 
                               WHERE name = N'LastSyncedAt' 
                                 AND object_id = OBJECT_ID(N'[dbo].[Clients]'))
                BEGIN
                    ALTER TABLE [dbo].[Clients] ADD [LastSyncedAt] DATETIME2 NULL;
                END
            ");

            migrationBuilder.Sql(@"
                IF NOT EXISTS (SELECT 1 FROM sys.indexes 
                               WHERE name = N'IX_Clients_LastSyncedAt'
                                 AND object_id = OBJECT_ID(N'[dbo].[Clients]'))
                BEGIN
                    CREATE INDEX [IX_Clients_LastSyncedAt] ON [dbo].[Clients] ([LastSyncedAt]);
                END
            ");

            migrationBuilder.Sql(@"
                IF EXISTS (SELECT 1 FROM sys.columns 
                           WHERE name = N'AccountId' 
                             AND object_id = OBJECT_ID(N'[dbo].[ScheduleSyncSources]'))
                BEGIN
                    EXEC sp_rename N'[dbo].[ScheduleSyncSources].[AccountId]', N'ExternalAccountId', 'COLUMN';
                END
            ");

            migrationBuilder.Sql(@"
                IF EXISTS (SELECT 1 FROM sys.columns 
                           WHERE name = N'VendorId' 
                             AND object_id = OBJECT_ID(N'[dbo].[ScheduleSyncSources]'))
                BEGIN
                    EXEC sp_rename N'[dbo].[ScheduleSyncSources].[VendorId]', N'ExternalVendorId', 'COLUMN';
                END
            ");

            migrationBuilder.Sql(@"
                IF EXISTS (SELECT 1 FROM sys.indexes 
                           WHERE name = N'IX_ScheduleSyncSources_AccountId'
                             AND object_id = OBJECT_ID(N'[dbo].[ScheduleSyncSources]'))
                BEGIN
                    EXEC sp_rename N'[dbo].[ScheduleSyncSources].[IX_ScheduleSyncSources_AccountId]', N'IX_ScheduleSyncSources_ExternalAccountId', 'INDEX';
                END
                IF EXISTS (SELECT 1 FROM sys.indexes 
                           WHERE name = N'IX_ScheduleSyncSources_VendorId'
                             AND object_id = OBJECT_ID(N'[dbo].[ScheduleSyncSources]'))
                BEGIN
                    EXEC sp_rename N'[dbo].[ScheduleSyncSources].[IX_ScheduleSyncSources_VendorId]', N'IX_ScheduleSyncSources_ExternalVendorId', 'INDEX';
                END
            ");

            migrationBuilder.Sql(@"
                IF NOT EXISTS (SELECT 1 FROM sys.columns 
                               WHERE name = N'ExternalClientId' 
                                 AND object_id = OBJECT_ID(N'[dbo].[ScheduleSyncSources]'))
                BEGIN
                    ALTER TABLE [dbo].[ScheduleSyncSources] 
                      ADD [ExternalClientId] INT NOT NULL CONSTRAINT DF_SSS_ExternalClientId DEFAULT(0);
                    ALTER TABLE [dbo].[ScheduleSyncSources] 
                      DROP CONSTRAINT DF_SSS_ExternalClientId;
                END
            ");

            migrationBuilder.Sql(@"
                DECLARE @currentType NVARCHAR(50);
                DECLARE @isNullable BIT;
                
                SELECT @currentType = t.name, @isNullable = c.is_nullable
                FROM sys.columns c
                JOIN sys.types t ON c.user_type_id = t.user_type_id
                WHERE c.[object_id] = OBJECT_ID(N'[dbo].[ScheduleSyncSources]')
                  AND c.[name] = N'ClientId';
                
                IF @currentType <> 'int' OR @isNullable = 0
                BEGIN
                    ALTER TABLE [dbo].[ScheduleSyncSources] ALTER COLUMN [ClientId] INT NULL;
                END
            ");

            migrationBuilder.Sql(@"
                IF NOT EXISTS (SELECT 1 FROM sys.indexes 
                               WHERE name = N'IX_ScheduleSyncSources_ExternalClientId'
                                 AND object_id = OBJECT_ID(N'[dbo].[ScheduleSyncSources]'))
                BEGIN
                    CREATE INDEX [IX_ScheduleSyncSources_ExternalClientId] ON [dbo].[ScheduleSyncSources] ([ExternalClientId]);
                END
            ");

            migrationBuilder.Sql(@"
                IF NOT EXISTS (SELECT 1 FROM sys.indexes 
                               WHERE name = N'IX_ScheduleSyncSources_ClientId_ExternalVendorId_AccountNumber'
                                 AND object_id = OBJECT_ID(N'[dbo].[ScheduleSyncSources]'))
                BEGIN
                    CREATE INDEX [IX_ScheduleSyncSources_ClientId_ExternalVendorId_AccountNumber]
                    ON [dbo].[ScheduleSyncSources] ([ExternalClientId], [ExternalVendorId], [AccountNumber]);
                END
            ");

            migrationBuilder.Sql(@"
                IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys 
                               WHERE name = N'FK_ScheduleSyncSources_Clients_ClientId'
                                 AND parent_object_id = OBJECT_ID(N'[dbo].[ScheduleSyncSources]'))
                BEGIN
                    ALTER TABLE [dbo].[ScheduleSyncSources] 
                    ADD CONSTRAINT [FK_ScheduleSyncSources_Clients_ClientId] 
                    FOREIGN KEY ([ClientId]) REFERENCES [dbo].[Clients] ([Id]) ON DELETE NO ACTION;
                END
            ");
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
