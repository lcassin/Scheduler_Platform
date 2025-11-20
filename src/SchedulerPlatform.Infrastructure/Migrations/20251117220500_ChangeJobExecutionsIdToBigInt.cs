using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SchedulerPlatform.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ChangeJobExecutionsIdToBigInt : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                CREATE TABLE [JobExecutions_New] (
                    [Id] bigint NOT NULL IDENTITY(1,1),
                    [ScheduleId] int NOT NULL,
                    [StartTime] datetime2 NOT NULL,
                    [EndTime] datetime2 NULL,
                    [Status] int NOT NULL,
                    [Output] nvarchar(max) NULL,
                    [ErrorMessage] nvarchar(max) NULL,
                    [StackTrace] nvarchar(max) NULL,
                    [RetryCount] int NOT NULL,
                    [DurationSeconds] int NULL,
                    [TriggeredBy] nvarchar(100) NULL,
                    [CancelledBy] nvarchar(100) NULL,
                    [CreatedAt] datetime2 NOT NULL,
                    [UpdatedAt] datetime2 NULL,
                    [CreatedBy] nvarchar(max) NULL,
                    [UpdatedBy] nvarchar(max) NULL,
                    [IsDeleted] bit NOT NULL,
                    CONSTRAINT [PK_JobExecutions_New] PRIMARY KEY ([Id]),
                    CONSTRAINT [FK_JobExecutions_New_Schedules_ScheduleId] FOREIGN KEY ([ScheduleId]) REFERENCES [Schedules] ([Id]) ON DELETE CASCADE
                );
            ");

            migrationBuilder.Sql(@"
                SET IDENTITY_INSERT [JobExecutions_New] ON;
                
                INSERT INTO [JobExecutions_New] (
                    [Id], [ScheduleId], [StartTime], [EndTime], [Status], [Output], 
                    [ErrorMessage], [StackTrace], [RetryCount], [DurationSeconds], 
                    [TriggeredBy], [CancelledBy], [CreatedAt], [UpdatedAt], 
                    [CreatedBy], [UpdatedBy], [IsDeleted]
                )
                SELECT 
                    [Id], [ScheduleId], [StartTime], [EndTime], [Status], [Output], 
                    [ErrorMessage], [StackTrace], [RetryCount], [DurationSeconds], 
                    [TriggeredBy], [CancelledBy], [CreatedAt], [UpdatedAt], 
                    [CreatedBy], [UpdatedBy], [IsDeleted]
                FROM [JobExecutions];
                
                SET IDENTITY_INSERT [JobExecutions_New] OFF;
            ");

            migrationBuilder.Sql("DROP TABLE [JobExecutions];");

            migrationBuilder.Sql("EXEC sp_rename 'JobExecutions_New', 'JobExecutions';");

            migrationBuilder.Sql(@"
                CREATE INDEX [IX_JobExecutions_ScheduleId] ON [JobExecutions] ([ScheduleId]);
                CREATE INDEX [IX_JobExecutions_StartTime] ON [JobExecutions] ([StartTime]);
                CREATE INDEX [IX_JobExecutions_Status] ON [JobExecutions] ([Status]);
            ");

            migrationBuilder.Sql(@"
                DECLARE @MaxId BIGINT = (SELECT ISNULL(MAX(Id), 0) FROM [JobExecutions]);
                DBCC CHECKIDENT ('JobExecutions', RESEED, @MaxId);
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                CREATE TABLE [JobExecutions_Old] (
                    [Id] int NOT NULL IDENTITY(1,1),
                    [ScheduleId] int NOT NULL,
                    [StartTime] datetime2 NOT NULL,
                    [EndTime] datetime2 NULL,
                    [Status] int NOT NULL,
                    [Output] nvarchar(max) NULL,
                    [ErrorMessage] nvarchar(max) NULL,
                    [StackTrace] nvarchar(max) NULL,
                    [RetryCount] int NOT NULL,
                    [DurationSeconds] int NULL,
                    [TriggeredBy] nvarchar(100) NULL,
                    [CancelledBy] nvarchar(100) NULL,
                    [CreatedAt] datetime2 NOT NULL,
                    [UpdatedAt] datetime2 NULL,
                    [CreatedBy] nvarchar(max) NULL,
                    [UpdatedBy] nvarchar(max) NULL,
                    [IsDeleted] bit NOT NULL,
                    CONSTRAINT [PK_JobExecutions_Old] PRIMARY KEY ([Id]),
                    CONSTRAINT [FK_JobExecutions_Old_Schedules_ScheduleId] FOREIGN KEY ([ScheduleId]) REFERENCES [Schedules] ([Id]) ON DELETE CASCADE
                );
            ");

            migrationBuilder.Sql(@"
                SET IDENTITY_INSERT [JobExecutions_Old] ON;
                
                INSERT INTO [JobExecutions_Old] (
                    [Id], [ScheduleId], [StartTime], [EndTime], [Status], [Output], 
                    [ErrorMessage], [StackTrace], [RetryCount], [DurationSeconds], 
                    [TriggeredBy], [CancelledBy], [CreatedAt], [UpdatedAt], 
                    [CreatedBy], [UpdatedBy], [IsDeleted]
                )
                SELECT 
                    [Id], [ScheduleId], [StartTime], [EndTime], [Status], [Output], 
                    [ErrorMessage], [StackTrace], [RetryCount], [DurationSeconds], 
                    [TriggeredBy], [CancelledBy], [CreatedAt], [UpdatedAt], 
                    [CreatedBy], [UpdatedBy], [IsDeleted]
                FROM [JobExecutions]
                WHERE [Id] <= 2147483647;
                
                SET IDENTITY_INSERT [JobExecutions_Old] OFF;
            ");

            migrationBuilder.Sql("DROP TABLE [JobExecutions];");

            migrationBuilder.Sql("EXEC sp_rename 'JobExecutions_Old', 'JobExecutions';");

            migrationBuilder.Sql(@"
                CREATE INDEX [IX_JobExecutions_ScheduleId] ON [JobExecutions] ([ScheduleId]);
                CREATE INDEX [IX_JobExecutions_StartTime] ON [JobExecutions] ([StartTime]);
                CREATE INDEX [IX_JobExecutions_Status] ON [JobExecutions] ([Status]);
            ");

            migrationBuilder.Sql(@"
                DECLARE @MaxId INT = (SELECT ISNULL(MAX(Id), 0) FROM [JobExecutions]);
                DBCC CHECKIDENT ('JobExecutions', RESEED, @MaxId);
            ");
        }
    }
}
