using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SchedulerPlatform.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAdrEntities : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
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
                    VendorCode = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
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
                    ModifiedDateTime = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
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
                name: "AdrJob",
                columns: table => new
                {
                    AdrJobId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AdrAccountId = table.Column<int>(type: "int", nullable: false),
                    VMAccountId = table.Column<long>(type: "bigint", nullable: false),
                    VMAccountNumber = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
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
                    ModifiedDateTime = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
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
                    ModifiedDateTime = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
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
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AdrJobExecution");

            migrationBuilder.DropTable(
                name: "AdrJob");

            migrationBuilder.DropTable(
                name: "AdrAccount");
        }
    }
}
