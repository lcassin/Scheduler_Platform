using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SchedulerPlatform.Infrastructure.Migrations
{
    public partial class AddCalendarIndexes : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                CREATE NONCLUSTERED INDEX IX_Schedules_Calendar 
                ON dbo.Schedules (IsDeleted, ClientId, NextRunTime) 
                INCLUDE (Id, Name, TimeZone, IsEnabled);
            ");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP INDEX IF EXISTS IX_Schedules_Calendar ON dbo.Schedules;");
        }
    }
}
