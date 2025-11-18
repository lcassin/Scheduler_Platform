using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SchedulerPlatform.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class EnsureExternalClientIdExists : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                IF NOT EXISTS (SELECT 1 FROM sys.columns 
                               WHERE name = N'ExternalClientId' 
                                 AND object_id = OBJECT_ID(N'[dbo].[Clients]'))
                BEGIN
                    ALTER TABLE [dbo].[Clients] 
                      ADD [ExternalClientId] INT NOT NULL 
                      CONSTRAINT DF_Clients_ExternalClientId DEFAULT(0);
                    
                    -- Drop the default constraint after adding the column
                    ALTER TABLE [dbo].[Clients] 
                      DROP CONSTRAINT DF_Clients_ExternalClientId;
                END
            ");

            migrationBuilder.Sql(@"
                IF NOT EXISTS (SELECT 1 FROM sys.indexes 
                               WHERE name = N'IX_Clients_ExternalClientId'
                                 AND object_id = OBJECT_ID(N'[dbo].[Clients]'))
                BEGIN
                    CREATE INDEX [IX_Clients_ExternalClientId] 
                    ON [dbo].[Clients] ([ExternalClientId]);
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
                    CREATE INDEX [IX_Clients_LastSyncedAt] 
                    ON [dbo].[Clients] ([LastSyncedAt]);
                END
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            
        }
    }
}
