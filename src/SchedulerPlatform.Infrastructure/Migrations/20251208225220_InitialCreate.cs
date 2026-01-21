using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SchedulerPlatform.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AuditLog",
                columns: table => new
                {
                    AuditLogId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EventType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    EntityType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    EntityId = table.Column<int>(type: "int", nullable: true),
                    Action = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    OldValues = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    NewValues = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UserName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ClientId = table.Column<int>(type: "int", nullable: true),
                    IpAddress = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    UserAgent = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    TimestampDateTime = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AdditionalData = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedDateTime = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedDateTime = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditLog", x => x.AuditLogId);
                });

            migrationBuilder.CreateTable(
                name: "Client",
                columns: table => new
                {
                    ClientId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ExternalClientId = table.Column<int>(type: "int", nullable: false),
                    ClientCode = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ClientName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    ContactEmail = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    ContactPhone = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    LastSyncedDateTime = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedDateTime = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedDateTime = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Client", x => x.ClientId);
                });

            migrationBuilder.CreateTable(
                name: "AdrAccount",
                columns: table => new
                {
                    AdrAccountId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    VMAccountId = table.Column<long>(type: "bigint", nullable: false),
                    VMAccountNumber = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    InterfaceAccountId = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    ClientId = table.Column<long>(type: "bigint", nullable: true),
                    ClientName = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    CredentialId = table.Column<int>(type: "int", nullable: false),
                    PrimaryVendorCode = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    MasterVendorCode = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    PeriodType = table.Column<string>(type: "nvarchar(13)", maxLength: 13, nullable: true),
                    PeriodDays = table.Column<int>(type: "int", nullable: true),
                    MedianDays = table.Column<double>(type: "float", nullable: true),
                    InvoiceCount = table.Column<int>(type: "int", nullable: false),
                    LastInvoiceDateTime = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ExpectedNextDateTime = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ExpectedRangeStartDateTime = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ExpectedRangeEndDateTime = table.Column<DateTime>(type: "datetime2", nullable: true),
                    NextRunDateTime = table.Column<DateTime>(type: "datetime2", nullable: true),
                    NextRangeStartDateTime = table.Column<DateTime>(type: "datetime2", nullable: true),
                    NextRangeEndDateTime = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DaysUntilNextRun = table.Column<int>(type: "int", nullable: true),
                    NextRunStatus = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    HistoricalBillingStatus = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    LastSyncedDateTime = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedDateTime = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedDateTime = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AdrAccount", x => x.AdrAccountId);
                    table.ForeignKey(
                        name: "FK_AdrAccount_Client_ClientId",
                        column: x => x.ClientId,
                        principalTable: "Client",
                        principalColumn: "ClientId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Schedule",
                columns: table => new
                {
                    ScheduleId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    ClientId = table.Column<long>(type: "bigint", nullable: false),
                    JobType = table.Column<int>(type: "int", nullable: false),
                    Frequency = table.Column<int>(type: "int", nullable: false),
                    CronExpression = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    NextRunDateTime = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastRunDateTime = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsEnabled = table.Column<bool>(type: "bit", nullable: false),
                    MaxRetries = table.Column<int>(type: "int", nullable: false),
                    RetryDelayMinutes = table.Column<int>(type: "int", nullable: false),
                    TimeoutMinutes = table.Column<int>(type: "int", nullable: true),
                    TimeZone = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    JobConfiguration = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedDateTime = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedDateTime = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Schedule", x => x.ScheduleId);
                    table.ForeignKey(
                        name: "FK_Schedule_Client_ClientId",
                        column: x => x.ClientId,
                        principalTable: "Client",
                        principalColumn: "ClientId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ScheduleSyncSource",
                columns: table => new
                {
                    ScheduleSyncSourceId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ExternalAccountId = table.Column<long>(type: "bigint", nullable: false),
                    AccountNumber = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    ExternalVendorId = table.Column<long>(type: "bigint", nullable: false),
                    ExternalClientId = table.Column<int>(type: "int", nullable: false),
                    ClientId = table.Column<long>(type: "bigint", nullable: true),
                    CredentialId = table.Column<int>(type: "int", nullable: false),
                    ScheduleFrequency = table.Column<int>(type: "int", nullable: false),
                    LastInvoiceDateTime = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AccountName = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    VendorName = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    ClientName = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    TandemAccountId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    LastSyncedDateTime = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedDateTime = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedDateTime = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ScheduleSyncSource", x => x.ScheduleSyncSourceId);
                    table.ForeignKey(
                        name: "FK_ScheduleSyncSource_Client_ClientId",
                        column: x => x.ClientId,
                        principalTable: "Client",
                        principalColumn: "ClientId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "User",
                columns: table => new
                {
                    UserId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Username = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Email = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    FirstName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    LastName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ClientId = table.Column<long>(type: "bigint", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    ExternalUserId = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    ExternalIssuer = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    PasswordHash = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    IsSystemAdmin = table.Column<bool>(type: "bit", nullable: false),
                    LastLoginDateTime = table.Column<DateTime>(type: "datetime2", nullable: true),
                    MustChangePassword = table.Column<bool>(type: "bit", nullable: false),
                    PasswordChangedDateTime = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedDateTime = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedDateTime = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_User", x => x.UserId);
                    table.ForeignKey(
                        name: "FK_User_Client_ClientId",
                        column: x => x.ClientId,
                        principalTable: "Client",
                        principalColumn: "ClientId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AdrJob",
                columns: table => new
                {
                    AdrJobId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AdrAccountId = table.Column<int>(type: "int", nullable: false),
                    VMAccountId = table.Column<long>(type: "bigint", nullable: false),
                    VMAccountNumber = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    PrimaryVendorCode = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    MasterVendorCode = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    CredentialId = table.Column<int>(type: "int", nullable: false),
                    PeriodType = table.Column<string>(type: "nvarchar(13)", maxLength: 13, nullable: true),
                    BillingPeriodStartDateTime = table.Column<DateTime>(type: "datetime2", nullable: false),
                    BillingPeriodEndDateTime = table.Column<DateTime>(type: "datetime2", nullable: false),
                    NextRunDateTime = table.Column<DateTime>(type: "datetime2", nullable: true),
                    NextRangeStartDateTime = table.Column<DateTime>(type: "datetime2", nullable: true),
                    NextRangeEndDateTime = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    IsMissing = table.Column<bool>(type: "bit", nullable: false),
                    AdrStatusId = table.Column<int>(type: "int", nullable: true),
                    AdrStatusDescription = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    AdrIndexId = table.Column<long>(type: "bigint", nullable: true),
                    CredentialVerifiedDateTime = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ScrapingCompletedDateTime = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ErrorMessage = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RetryCount = table.Column<int>(type: "int", nullable: false),
                    CreatedDateTime = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedDateTime = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AdrJob", x => x.AdrJobId);
                    table.ForeignKey(
                        name: "FK_AdrJob_AdrAccount_AdrAccountId",
                        column: x => x.AdrAccountId,
                        principalTable: "AdrAccount",
                        principalColumn: "AdrAccountId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "JobExecution",
                columns: table => new
                {
                    JobExecutionId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ScheduleId = table.Column<int>(type: "int", nullable: false),
                    StartDateTime = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EndDateTime = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    Output = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ErrorMessage = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    StackTrace = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RetryCount = table.Column<int>(type: "int", nullable: false),
                    DurationSeconds = table.Column<int>(type: "int", nullable: true),
                    TriggeredBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    CancelledBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    CreatedDateTime = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedDateTime = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_JobExecution", x => x.JobExecutionId);
                    table.ForeignKey(
                        name: "FK_JobExecution_Schedule_ScheduleId",
                        column: x => x.ScheduleId,
                        principalTable: "Schedule",
                        principalColumn: "ScheduleId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "JobParameter",
                columns: table => new
                {
                    JobParameterId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ScheduleId = table.Column<int>(type: "int", nullable: false),
                    ParameterName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ParameterType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    ParameterValue = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SourceQuery = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SourceConnectionString = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    IsDynamic = table.Column<bool>(type: "bit", nullable: false),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false),
                    CreatedDateTime = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedDateTime = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_JobParameter", x => x.JobParameterId);
                    table.ForeignKey(
                        name: "FK_JobParameter_Schedule_ScheduleId",
                        column: x => x.ScheduleId,
                        principalTable: "Schedule",
                        principalColumn: "ScheduleId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "NotificationSetting",
                columns: table => new
                {
                    NotificationSettingId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ScheduleId = table.Column<int>(type: "int", nullable: false),
                    EnableSuccessNotifications = table.Column<bool>(type: "bit", nullable: false),
                    EnableFailureNotifications = table.Column<bool>(type: "bit", nullable: false),
                    SuccessEmailRecipients = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    FailureEmailRecipients = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    SuccessEmailSubject = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    FailureEmailSubject = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    IncludeExecutionDetails = table.Column<bool>(type: "bit", nullable: false),
                    IncludeOutput = table.Column<bool>(type: "bit", nullable: false),
                    CreatedDateTime = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedDateTime = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NotificationSetting", x => x.NotificationSettingId);
                    table.ForeignKey(
                        name: "FK_NotificationSetting_Schedule_ScheduleId",
                        column: x => x.ScheduleId,
                        principalTable: "Schedule",
                        principalColumn: "ScheduleId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PasswordHistory",
                columns: table => new
                {
                    PasswordHistoryId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    PasswordHash = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    ChangedDateTime = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedDateTime = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedDateTime = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PasswordHistory", x => x.PasswordHistoryId);
                    table.ForeignKey(
                        name: "FK_PasswordHistory_User_UserId",
                        column: x => x.UserId,
                        principalTable: "User",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserPermission",
                columns: table => new
                {
                    UserPermissionId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    PermissionName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ResourceType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    ResourceId = table.Column<int>(type: "int", nullable: true),
                    CanCreate = table.Column<bool>(type: "bit", nullable: false),
                    CanRead = table.Column<bool>(type: "bit", nullable: false),
                    CanUpdate = table.Column<bool>(type: "bit", nullable: false),
                    CanDelete = table.Column<bool>(type: "bit", nullable: false),
                    CanExecute = table.Column<bool>(type: "bit", nullable: false),
                    CreatedDateTime = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedDateTime = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserPermission", x => x.UserPermissionId);
                    table.ForeignKey(
                        name: "FK_UserPermission_User_UserId",
                        column: x => x.UserId,
                        principalTable: "User",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AdrJobExecution",
                columns: table => new
                {
                    AdrJobExecutionId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AdrJobId = table.Column<int>(type: "int", nullable: false),
                    AdrRequestTypeId = table.Column<int>(type: "int", nullable: false),
                    StartDateTime = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EndDateTime = table.Column<DateTime>(type: "datetime2", nullable: true),
                    AdrStatusId = table.Column<int>(type: "int", nullable: true),
                    AdrStatusDescription = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    IsError = table.Column<bool>(type: "bit", nullable: false),
                    IsFinal = table.Column<bool>(type: "bit", nullable: false),
                    AdrIndexId = table.Column<long>(type: "bigint", nullable: true),
                    HttpStatusCode = table.Column<int>(type: "int", nullable: true),
                    IsSuccess = table.Column<bool>(type: "bit", nullable: false),
                    ErrorMessage = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ApiResponse = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RequestPayload = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedDateTime = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedDateTime = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AdrJobExecution", x => x.AdrJobExecutionId);
                    table.ForeignKey(
                        name: "FK_AdrJobExecution_AdrJob_AdrJobId",
                        column: x => x.AdrJobId,
                        principalTable: "AdrJob",
                        principalColumn: "AdrJobId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AdrAccount_ClientId",
                table: "AdrAccount",
                column: "ClientId");

            migrationBuilder.CreateIndex(
                name: "IX_AdrAccount_CredentialId",
                table: "AdrAccount",
                column: "CredentialId");

            migrationBuilder.CreateIndex(
                name: "IX_AdrAccount_HistoricalBillingStatus",
                table: "AdrAccount",
                column: "HistoricalBillingStatus");

            migrationBuilder.CreateIndex(
                name: "IX_AdrAccount_NextRunStatus",
                table: "AdrAccount",
                column: "NextRunStatus");

            migrationBuilder.CreateIndex(
                name: "IX_AdrAccount_VMAccountId",
                table: "AdrAccount",
                column: "VMAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_AdrAccount_VMAccountId_VMAccountNumber",
                table: "AdrAccount",
                columns: new[] { "VMAccountId", "VMAccountNumber" });

            migrationBuilder.CreateIndex(
                name: "IX_AdrAccount_VMAccountNumber",
                table: "AdrAccount",
                column: "VMAccountNumber");

            migrationBuilder.CreateIndex(
                name: "IX_AdrJob_AdrAccountId",
                table: "AdrJob",
                column: "AdrAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_AdrJob_AdrAccountId_BillingPeriodStartDateTime_BillingPeriodEndDateTime",
                table: "AdrJob",
                columns: new[] { "AdrAccountId", "BillingPeriodStartDateTime", "BillingPeriodEndDateTime" });

            migrationBuilder.CreateIndex(
                name: "IX_AdrJob_BillingPeriodStartDateTime",
                table: "AdrJob",
                column: "BillingPeriodStartDateTime");

            migrationBuilder.CreateIndex(
                name: "IX_AdrJob_CredentialId",
                table: "AdrJob",
                column: "CredentialId");

            migrationBuilder.CreateIndex(
                name: "IX_AdrJob_Status",
                table: "AdrJob",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_AdrJob_VMAccountId",
                table: "AdrJob",
                column: "VMAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_AdrJobExecution_AdrJobId",
                table: "AdrJobExecution",
                column: "AdrJobId");

            migrationBuilder.CreateIndex(
                name: "IX_AdrJobExecution_AdrRequestTypeId",
                table: "AdrJobExecution",
                column: "AdrRequestTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_AdrJobExecution_IsSuccess",
                table: "AdrJobExecution",
                column: "IsSuccess");

            migrationBuilder.CreateIndex(
                name: "IX_AdrJobExecution_StartDateTime",
                table: "AdrJobExecution",
                column: "StartDateTime");

            migrationBuilder.CreateIndex(
                name: "IX_AuditLog_EntityType_EntityId",
                table: "AuditLog",
                columns: new[] { "EntityType", "EntityId" });

            migrationBuilder.CreateIndex(
                name: "IX_AuditLog_TimestampDateTime",
                table: "AuditLog",
                column: "TimestampDateTime");

            migrationBuilder.CreateIndex(
                name: "IX_Client_ExternalClientId",
                table: "Client",
                column: "ExternalClientId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Client_LastSyncedDateTime",
                table: "Client",
                column: "LastSyncedDateTime");

            migrationBuilder.CreateIndex(
                name: "IX_JobExecution_ScheduleId",
                table: "JobExecution",
                column: "ScheduleId");

            migrationBuilder.CreateIndex(
                name: "IX_JobExecution_StartDateTime",
                table: "JobExecution",
                column: "StartDateTime");

            migrationBuilder.CreateIndex(
                name: "IX_JobExecution_Status",
                table: "JobExecution",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_JobParameter_ScheduleId",
                table: "JobParameter",
                column: "ScheduleId");

            migrationBuilder.CreateIndex(
                name: "IX_NotificationSetting_ScheduleId",
                table: "NotificationSetting",
                column: "ScheduleId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PasswordHistory_UserId_ChangedDateTime",
                table: "PasswordHistory",
                columns: new[] { "UserId", "ChangedDateTime" });

            migrationBuilder.CreateIndex(
                name: "IX_Schedule_ClientId",
                table: "Schedule",
                column: "ClientId");

            migrationBuilder.CreateIndex(
                name: "IX_ScheduleSyncSource_ClientId",
                table: "ScheduleSyncSource",
                column: "ClientId");

            migrationBuilder.CreateIndex(
                name: "IX_ScheduleSyncSource_ExternalAccountId",
                table: "ScheduleSyncSource",
                column: "ExternalAccountId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ScheduleSyncSource_ExternalClientId",
                table: "ScheduleSyncSource",
                column: "ExternalClientId");

            migrationBuilder.CreateIndex(
                name: "IX_ScheduleSyncSource_ExternalClientId_ExternalVendorId_AccountNumber",
                table: "ScheduleSyncSource",
                columns: new[] { "ExternalClientId", "ExternalVendorId", "AccountNumber" });

            migrationBuilder.CreateIndex(
                name: "IX_ScheduleSyncSource_ExternalVendorId",
                table: "ScheduleSyncSource",
                column: "ExternalVendorId");

            migrationBuilder.CreateIndex(
                name: "IX_ScheduleSyncSource_LastSyncedDateTime",
                table: "ScheduleSyncSource",
                column: "LastSyncedDateTime");

            migrationBuilder.CreateIndex(
                name: "IX_User_ClientId",
                table: "User",
                column: "ClientId");

            migrationBuilder.CreateIndex(
                name: "IX_User_Email",
                table: "User",
                column: "Email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_User_ExternalIssuer_ExternalUserId",
                table: "User",
                columns: new[] { "ExternalIssuer", "ExternalUserId" });

            migrationBuilder.CreateIndex(
                name: "IX_UserPermission_UserId",
                table: "UserPermission",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AdrJobExecution");

            migrationBuilder.DropTable(
                name: "AuditLog");

            migrationBuilder.DropTable(
                name: "JobExecution");

            migrationBuilder.DropTable(
                name: "JobParameter");

            migrationBuilder.DropTable(
                name: "NotificationSetting");

            migrationBuilder.DropTable(
                name: "PasswordHistory");

            migrationBuilder.DropTable(
                name: "ScheduleSyncSource");

            migrationBuilder.DropTable(
                name: "UserPermission");

            migrationBuilder.DropTable(
                name: "AdrJob");

            migrationBuilder.DropTable(
                name: "Schedule");

            migrationBuilder.DropTable(
                name: "User");

            migrationBuilder.DropTable(
                name: "AdrAccount");

            migrationBuilder.DropTable(
                name: "Client");
        }
    }
}
