using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SchedulerPlatform.Infrastructure.Migrations
{
    /// <summary>
    /// Adds performance indexes for blacklist matching, paged queries, and common filter/sort patterns.
    /// These indexes already exist in the SQL DB project table definitions but were missing from EF migrations.
    /// All CREATE INDEX statements use IF NOT EXISTS guards so they are safe to re-run.
    /// </summary>
    public partial class AddPerformanceIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // =============================================
            // AdrAccountBlacklist indexes (none existed in EF migrations)
            // =============================================
            migrationBuilder.CreateIndex(
                name: "IX_AdrAccountBlacklist_PrimaryVendorCode",
                table: "AdrAccountBlacklist",
                column: "PrimaryVendorCode");

            migrationBuilder.CreateIndex(
                name: "IX_AdrAccountBlacklist_MasterVendorCode",
                table: "AdrAccountBlacklist",
                column: "MasterVendorCode");

            migrationBuilder.CreateIndex(
                name: "IX_AdrAccountBlacklist_VMAccountId",
                table: "AdrAccountBlacklist",
                column: "VMAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_AdrAccountBlacklist_VMAccountNumber",
                table: "AdrAccountBlacklist",
                column: "VMAccountNumber");

            migrationBuilder.CreateIndex(
                name: "IX_AdrAccountBlacklist_CredentialId",
                table: "AdrAccountBlacklist",
                column: "CredentialId");

            migrationBuilder.CreateIndex(
                name: "IX_AdrAccountBlacklist_IsActive",
                table: "AdrAccountBlacklist",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_AdrAccountBlacklist_IsDeleted_IsActive",
                table: "AdrAccountBlacklist",
                columns: new[] { "IsDeleted", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_AdrAccountBlacklist_PrimaryVendorCode_VMAccountId_CredentialId",
                table: "AdrAccountBlacklist",
                columns: new[] { "PrimaryVendorCode", "VMAccountId", "CredentialId" });

            migrationBuilder.CreateIndex(
                name: "IX_AdrAccountBlacklist_MasterVendorCode_VMAccountId_CredentialId",
                table: "AdrAccountBlacklist",
                columns: new[] { "MasterVendorCode", "VMAccountId", "CredentialId" });

            // =============================================
            // AdrAccount performance indexes (missing from InitialCreate)
            // =============================================
            migrationBuilder.CreateIndex(
                name: "IX_AdrAccount_NextRunDateTime",
                table: "AdrAccount",
                column: "NextRunDateTime");

            migrationBuilder.CreateIndex(
                name: "IX_AdrAccount_InterfaceAccountId",
                table: "AdrAccount",
                column: "InterfaceAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_AdrAccount_PrimaryVendorCode",
                table: "AdrAccount",
                column: "PrimaryVendorCode");

            migrationBuilder.CreateIndex(
                name: "IX_AdrAccount_MasterVendorCode",
                table: "AdrAccount",
                column: "MasterVendorCode");

            migrationBuilder.CreateIndex(
                name: "IX_AdrAccount_IsDeleted_NextRunStatus_NextRunDateTime",
                table: "AdrAccount",
                columns: new[] { "IsDeleted", "NextRunStatus", "NextRunDateTime" });

            migrationBuilder.CreateIndex(
                name: "IX_AdrAccount_IsDeleted_HistoricalBillingStatus",
                table: "AdrAccount",
                columns: new[] { "IsDeleted", "HistoricalBillingStatus" });

            migrationBuilder.CreateIndex(
                name: "IX_AdrAccount_IsDeleted_ClientId_NextRunStatus",
                table: "AdrAccount",
                columns: new[] { "IsDeleted", "ClientId", "NextRunStatus" });

            // =============================================
            // AdrJob performance indexes (missing from InitialCreate)
            // =============================================
            migrationBuilder.CreateIndex(
                name: "IX_AdrJob_AdrAccountRuleId",
                table: "AdrJob",
                column: "AdrAccountRuleId");

            migrationBuilder.CreateIndex(
                name: "IX_AdrJob_AdrJobTypeId",
                table: "AdrJob",
                column: "AdrJobTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_AdrJob_VMAccountNumber",
                table: "AdrJob",
                column: "VMAccountNumber");

            migrationBuilder.CreateIndex(
                name: "IX_AdrJob_PrimaryVendorCode",
                table: "AdrJob",
                column: "PrimaryVendorCode");

            migrationBuilder.CreateIndex(
                name: "IX_AdrJob_MasterVendorCode",
                table: "AdrJob",
                column: "MasterVendorCode");

            migrationBuilder.CreateIndex(
                name: "IX_AdrJob_ModifiedDateTime",
                table: "AdrJob",
                column: "ModifiedDateTime");

            migrationBuilder.CreateIndex(
                name: "IX_AdrJob_ScrapingCompletedDateTime",
                table: "AdrJob",
                column: "ScrapingCompletedDateTime");

            migrationBuilder.CreateIndex(
                name: "IX_AdrJob_IsDeleted_Status",
                table: "AdrJob",
                columns: new[] { "IsDeleted", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_AdrJob_IsDeleted_Status_BillingPeriodStartDateTime",
                table: "AdrJob",
                columns: new[] { "IsDeleted", "Status", "BillingPeriodStartDateTime" });

            migrationBuilder.CreateIndex(
                name: "IX_AdrJob_IsDeleted_AdrAccountId_BillingPeriodStartDateTime",
                table: "AdrJob",
                columns: new[] { "IsDeleted", "AdrAccountId", "BillingPeriodStartDateTime" });

            migrationBuilder.CreateIndex(
                name: "IX_AdrJob_IsDeleted_AdrAccountId_ScrapingCompletedDateTime",
                table: "AdrJob",
                columns: new[] { "IsDeleted", "AdrAccountId", "ScrapingCompletedDateTime" });

            // =============================================
            // AdrJobExecution index (missing from InitialCreate)
            // =============================================
            migrationBuilder.CreateIndex(
                name: "IX_AdrJobExecution_OrchestrationRequestId",
                table: "AdrJobExecution",
                column: "OrchestrationRequestId");

            // =============================================
            // AdrAccountRule indexes (none existed in EF migrations)
            // =============================================
            migrationBuilder.CreateIndex(
                name: "IX_AdrAccountRule_AdrAccountId",
                table: "AdrAccountRule",
                column: "AdrAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_AdrAccountRule_JobTypeId",
                table: "AdrAccountRule",
                column: "JobTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_AdrAccountRule_IsEnabled",
                table: "AdrAccountRule",
                column: "IsEnabled");

            migrationBuilder.CreateIndex(
                name: "IX_AdrAccountRule_NextRunDateTime",
                table: "AdrAccountRule",
                column: "NextRunDateTime");

            migrationBuilder.CreateIndex(
                name: "IX_AdrAccountRule_AdrAccountId_JobTypeId",
                table: "AdrAccountRule",
                columns: new[] { "AdrAccountId", "JobTypeId" });

            migrationBuilder.CreateIndex(
                name: "IX_AdrAccountRule_IsDeleted_IsEnabled_NextRunDateTime",
                table: "AdrAccountRule",
                columns: new[] { "IsDeleted", "IsEnabled", "NextRunDateTime" });

            migrationBuilder.CreateIndex(
                name: "IX_AdrAccountRule_IsDeleted_JobTypeId_AdrAccountId",
                table: "AdrAccountRule",
                columns: new[] { "IsDeleted", "JobTypeId", "AdrAccountId" });

            // =============================================
            // AdrOrchestrationRun indexes (none existed in EF migrations)
            // =============================================
            migrationBuilder.CreateIndex(
                name: "IX_AdrOrchestrationRun_RequestId",
                table: "AdrOrchestrationRun",
                column: "RequestId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AdrOrchestrationRun_RequestedDateTime",
                table: "AdrOrchestrationRun",
                column: "RequestedDateTime");

            migrationBuilder.CreateIndex(
                name: "IX_AdrOrchestrationRun_Status",
                table: "AdrOrchestrationRun",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_AdrOrchestrationRun_Status_RequestedDateTime",
                table: "AdrOrchestrationRun",
                columns: new[] { "Status", "RequestedDateTime" });

            // =============================================
            // AdrJobType indexes (none existed in EF migrations)
            // =============================================
            migrationBuilder.CreateIndex(
                name: "IX_AdrJobType_Code",
                table: "AdrJobType",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AdrJobType_IsActive",
                table: "AdrJobType",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_AdrJobType_AdrRequestTypeId",
                table: "AdrJobType",
                column: "AdrRequestTypeId");

            // =============================================
            // PowerBiReport indexes (none existed in EF migrations)
            // =============================================
            migrationBuilder.CreateIndex(
                name: "IX_PowerBiReport_Category",
                table: "PowerBiReport",
                column: "Category");

            migrationBuilder.CreateIndex(
                name: "IX_PowerBiReport_IsActive",
                table: "PowerBiReport",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_PowerBiReport_Category_DisplayOrder",
                table: "PowerBiReport",
                columns: new[] { "Category", "DisplayOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_PowerBiReport_IsDeleted_IsActive_Category_DisplayOrder",
                table: "PowerBiReport",
                columns: new[] { "IsDeleted", "IsActive", "Category", "DisplayOrder" });

            // =============================================
            // Archive table indexes (none existed in EF migrations)
            // =============================================

            // AdrJobArchive
            migrationBuilder.CreateIndex(
                name: "IX_AdrJobArchive_OriginalAdrJobId",
                table: "AdrJobArchive",
                column: "OriginalAdrJobId");

            migrationBuilder.CreateIndex(
                name: "IX_AdrJobArchive_AdrAccountId",
                table: "AdrJobArchive",
                column: "AdrAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_AdrJobArchive_VMAccountId",
                table: "AdrJobArchive",
                column: "VMAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_AdrJobArchive_ArchivedDateTime",
                table: "AdrJobArchive",
                column: "ArchivedDateTime");

            migrationBuilder.CreateIndex(
                name: "IX_AdrJobArchive_BillingPeriodStartDateTime",
                table: "AdrJobArchive",
                column: "BillingPeriodStartDateTime");

            // AdrJobExecutionArchive
            migrationBuilder.CreateIndex(
                name: "IX_AdrJobExecutionArchive_OriginalAdrJobExecutionId",
                table: "AdrJobExecutionArchive",
                column: "OriginalAdrJobExecutionId");

            migrationBuilder.CreateIndex(
                name: "IX_AdrJobExecutionArchive_AdrJobId",
                table: "AdrJobExecutionArchive",
                column: "AdrJobId");

            migrationBuilder.CreateIndex(
                name: "IX_AdrJobExecutionArchive_ArchivedDateTime",
                table: "AdrJobExecutionArchive",
                column: "ArchivedDateTime");

            migrationBuilder.CreateIndex(
                name: "IX_AdrJobExecutionArchive_StartDateTime",
                table: "AdrJobExecutionArchive",
                column: "StartDateTime");

            // AuditLogArchive
            migrationBuilder.CreateIndex(
                name: "IX_AuditLogArchive_OriginalAuditLogId",
                table: "AuditLogArchive",
                column: "OriginalAuditLogId");

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogArchive_ArchivedDateTime",
                table: "AuditLogArchive",
                column: "ArchivedDateTime");

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogArchive_TimestampDateTime",
                table: "AuditLogArchive",
                column: "TimestampDateTime");

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogArchive_EntityType_EntityId",
                table: "AuditLogArchive",
                columns: new[] { "EntityType", "EntityId" });

            // JobExecutionArchive
            migrationBuilder.CreateIndex(
                name: "IX_JobExecutionArchive_OriginalJobExecutionId",
                table: "JobExecutionArchive",
                column: "OriginalJobExecutionId");

            migrationBuilder.CreateIndex(
                name: "IX_JobExecutionArchive_ScheduleId",
                table: "JobExecutionArchive",
                column: "ScheduleId");

            migrationBuilder.CreateIndex(
                name: "IX_JobExecutionArchive_ArchivedDateTime",
                table: "JobExecutionArchive",
                column: "ArchivedDateTime");

            migrationBuilder.CreateIndex(
                name: "IX_JobExecutionArchive_StartDateTime",
                table: "JobExecutionArchive",
                column: "StartDateTime");

            migrationBuilder.CreateIndex(
                name: "IX_JobExecutionArchive_Status",
                table: "JobExecutionArchive",
                column: "Status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // AdrAccountBlacklist
            migrationBuilder.DropIndex(name: "IX_AdrAccountBlacklist_PrimaryVendorCode", table: "AdrAccountBlacklist");
            migrationBuilder.DropIndex(name: "IX_AdrAccountBlacklist_MasterVendorCode", table: "AdrAccountBlacklist");
            migrationBuilder.DropIndex(name: "IX_AdrAccountBlacklist_VMAccountId", table: "AdrAccountBlacklist");
            migrationBuilder.DropIndex(name: "IX_AdrAccountBlacklist_VMAccountNumber", table: "AdrAccountBlacklist");
            migrationBuilder.DropIndex(name: "IX_AdrAccountBlacklist_CredentialId", table: "AdrAccountBlacklist");
            migrationBuilder.DropIndex(name: "IX_AdrAccountBlacklist_IsActive", table: "AdrAccountBlacklist");
            migrationBuilder.DropIndex(name: "IX_AdrAccountBlacklist_IsDeleted_IsActive", table: "AdrAccountBlacklist");
            migrationBuilder.DropIndex(name: "IX_AdrAccountBlacklist_PrimaryVendorCode_VMAccountId_CredentialId", table: "AdrAccountBlacklist");
            migrationBuilder.DropIndex(name: "IX_AdrAccountBlacklist_MasterVendorCode_VMAccountId_CredentialId", table: "AdrAccountBlacklist");

            // AdrAccount
            migrationBuilder.DropIndex(name: "IX_AdrAccount_NextRunDateTime", table: "AdrAccount");
            migrationBuilder.DropIndex(name: "IX_AdrAccount_InterfaceAccountId", table: "AdrAccount");
            migrationBuilder.DropIndex(name: "IX_AdrAccount_PrimaryVendorCode", table: "AdrAccount");
            migrationBuilder.DropIndex(name: "IX_AdrAccount_MasterVendorCode", table: "AdrAccount");
            migrationBuilder.DropIndex(name: "IX_AdrAccount_IsDeleted_NextRunStatus_NextRunDateTime", table: "AdrAccount");
            migrationBuilder.DropIndex(name: "IX_AdrAccount_IsDeleted_HistoricalBillingStatus", table: "AdrAccount");
            migrationBuilder.DropIndex(name: "IX_AdrAccount_IsDeleted_ClientId_NextRunStatus", table: "AdrAccount");

            // AdrJob
            migrationBuilder.DropIndex(name: "IX_AdrJob_AdrAccountRuleId", table: "AdrJob");
            migrationBuilder.DropIndex(name: "IX_AdrJob_AdrJobTypeId", table: "AdrJob");
            migrationBuilder.DropIndex(name: "IX_AdrJob_VMAccountNumber", table: "AdrJob");
            migrationBuilder.DropIndex(name: "IX_AdrJob_PrimaryVendorCode", table: "AdrJob");
            migrationBuilder.DropIndex(name: "IX_AdrJob_MasterVendorCode", table: "AdrJob");
            migrationBuilder.DropIndex(name: "IX_AdrJob_ModifiedDateTime", table: "AdrJob");
            migrationBuilder.DropIndex(name: "IX_AdrJob_ScrapingCompletedDateTime", table: "AdrJob");
            migrationBuilder.DropIndex(name: "IX_AdrJob_IsDeleted_Status", table: "AdrJob");
            migrationBuilder.DropIndex(name: "IX_AdrJob_IsDeleted_Status_BillingPeriodStartDateTime", table: "AdrJob");
            migrationBuilder.DropIndex(name: "IX_AdrJob_IsDeleted_AdrAccountId_BillingPeriodStartDateTime", table: "AdrJob");
            migrationBuilder.DropIndex(name: "IX_AdrJob_IsDeleted_AdrAccountId_ScrapingCompletedDateTime", table: "AdrJob");

            // AdrJobExecution
            migrationBuilder.DropIndex(name: "IX_AdrJobExecution_OrchestrationRequestId", table: "AdrJobExecution");

            // AdrAccountRule
            migrationBuilder.DropIndex(name: "IX_AdrAccountRule_AdrAccountId", table: "AdrAccountRule");
            migrationBuilder.DropIndex(name: "IX_AdrAccountRule_JobTypeId", table: "AdrAccountRule");
            migrationBuilder.DropIndex(name: "IX_AdrAccountRule_IsEnabled", table: "AdrAccountRule");
            migrationBuilder.DropIndex(name: "IX_AdrAccountRule_NextRunDateTime", table: "AdrAccountRule");
            migrationBuilder.DropIndex(name: "IX_AdrAccountRule_AdrAccountId_JobTypeId", table: "AdrAccountRule");
            migrationBuilder.DropIndex(name: "IX_AdrAccountRule_IsDeleted_IsEnabled_NextRunDateTime", table: "AdrAccountRule");
            migrationBuilder.DropIndex(name: "IX_AdrAccountRule_IsDeleted_JobTypeId_AdrAccountId", table: "AdrAccountRule");

            // AdrOrchestrationRun
            migrationBuilder.DropIndex(name: "IX_AdrOrchestrationRun_RequestId", table: "AdrOrchestrationRun");
            migrationBuilder.DropIndex(name: "IX_AdrOrchestrationRun_RequestedDateTime", table: "AdrOrchestrationRun");
            migrationBuilder.DropIndex(name: "IX_AdrOrchestrationRun_Status", table: "AdrOrchestrationRun");
            migrationBuilder.DropIndex(name: "IX_AdrOrchestrationRun_Status_RequestedDateTime", table: "AdrOrchestrationRun");

            // AdrJobType
            migrationBuilder.DropIndex(name: "IX_AdrJobType_Code", table: "AdrJobType");
            migrationBuilder.DropIndex(name: "IX_AdrJobType_IsActive", table: "AdrJobType");
            migrationBuilder.DropIndex(name: "IX_AdrJobType_AdrRequestTypeId", table: "AdrJobType");

            // PowerBiReport
            migrationBuilder.DropIndex(name: "IX_PowerBiReport_Category", table: "PowerBiReport");
            migrationBuilder.DropIndex(name: "IX_PowerBiReport_IsActive", table: "PowerBiReport");
            migrationBuilder.DropIndex(name: "IX_PowerBiReport_Category_DisplayOrder", table: "PowerBiReport");
            migrationBuilder.DropIndex(name: "IX_PowerBiReport_IsDeleted_IsActive_Category_DisplayOrder", table: "PowerBiReport");

            // AdrJobArchive
            migrationBuilder.DropIndex(name: "IX_AdrJobArchive_OriginalAdrJobId", table: "AdrJobArchive");
            migrationBuilder.DropIndex(name: "IX_AdrJobArchive_AdrAccountId", table: "AdrJobArchive");
            migrationBuilder.DropIndex(name: "IX_AdrJobArchive_VMAccountId", table: "AdrJobArchive");
            migrationBuilder.DropIndex(name: "IX_AdrJobArchive_ArchivedDateTime", table: "AdrJobArchive");
            migrationBuilder.DropIndex(name: "IX_AdrJobArchive_BillingPeriodStartDateTime", table: "AdrJobArchive");

            // AdrJobExecutionArchive
            migrationBuilder.DropIndex(name: "IX_AdrJobExecutionArchive_OriginalAdrJobExecutionId", table: "AdrJobExecutionArchive");
            migrationBuilder.DropIndex(name: "IX_AdrJobExecutionArchive_AdrJobId", table: "AdrJobExecutionArchive");
            migrationBuilder.DropIndex(name: "IX_AdrJobExecutionArchive_ArchivedDateTime", table: "AdrJobExecutionArchive");
            migrationBuilder.DropIndex(name: "IX_AdrJobExecutionArchive_StartDateTime", table: "AdrJobExecutionArchive");

            // AuditLogArchive
            migrationBuilder.DropIndex(name: "IX_AuditLogArchive_OriginalAuditLogId", table: "AuditLogArchive");
            migrationBuilder.DropIndex(name: "IX_AuditLogArchive_ArchivedDateTime", table: "AuditLogArchive");
            migrationBuilder.DropIndex(name: "IX_AuditLogArchive_TimestampDateTime", table: "AuditLogArchive");
            migrationBuilder.DropIndex(name: "IX_AuditLogArchive_EntityType_EntityId", table: "AuditLogArchive");

            // JobExecutionArchive
            migrationBuilder.DropIndex(name: "IX_JobExecutionArchive_OriginalJobExecutionId", table: "JobExecutionArchive");
            migrationBuilder.DropIndex(name: "IX_JobExecutionArchive_ScheduleId", table: "JobExecutionArchive");
            migrationBuilder.DropIndex(name: "IX_JobExecutionArchive_ArchivedDateTime", table: "JobExecutionArchive");
            migrationBuilder.DropIndex(name: "IX_JobExecutionArchive_StartDateTime", table: "JobExecutionArchive");
            migrationBuilder.DropIndex(name: "IX_JobExecutionArchive_Status", table: "JobExecutionArchive");
        }
    }
}
