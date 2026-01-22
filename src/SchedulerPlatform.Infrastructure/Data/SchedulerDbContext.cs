using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using SchedulerPlatform.Core.Domain.Entities;

namespace SchedulerPlatform.Infrastructure.Data;

public class SchedulerDbContext : DbContext
{
    public SchedulerDbContext(DbContextOptions<SchedulerDbContext> options) : base(options)
    {
    }

    public DbSet<Client> Clients { get; set; }
    public DbSet<User> Users { get; set; }
    public DbSet<UserPermission> UserPermissions { get; set; }
    public DbSet<PasswordHistory> PasswordHistories { get; set; }
    public DbSet<Schedule> Schedules { get; set; }
    public DbSet<JobExecution> JobExecutions { get; set; }
    public DbSet<JobParameter> JobParameters { get; set; }
    public DbSet<AuditLog> AuditLogs { get; set; }
    public DbSet<NotificationSetting> NotificationSettings { get; set; }
    public DbSet<ScheduleSyncSource> ScheduleSyncSources { get; set; }
    
        // ADR Process entities
        public DbSet<AdrAccount> AdrAccounts { get; set; }
        public DbSet<AdrAccountRule> AdrAccountRules { get; set; }
        public DbSet<AdrJobType> AdrJobTypes { get; set; }
        public DbSet<AdrConfiguration> AdrConfigurations { get; set; }
        public DbSet<AdrAccountBlacklist> AdrAccountBlacklists { get; set; }
        public DbSet<AdrJob> AdrJobs { get; set; }
        public DbSet<AdrJobExecution> AdrJobExecutions { get; set; }
        public DbSet<AdrOrchestrationRun> AdrOrchestrationRuns { get; set; }
        
        // Power BI Reports configuration
        public DbSet<PowerBiReport> PowerBiReports { get; set; }
        
        // Archive tables for data retention
        public DbSet<AdrJobArchive> AdrJobArchives { get; set; }
        public DbSet<AdrJobExecutionArchive> AdrJobExecutionArchives { get; set; }
        public DbSet<AuditLogArchive> AuditLogArchives { get; set; }
        public DbSet<JobExecutionArchive> JobExecutionArchives { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        var intToLongConverter = new ValueConverter<int, long>(
            v => v,
            v => checked((int)v));
        
        var nullableIntToLongConverter = new ValueConverter<int?, long?>(
            v => v.HasValue ? (long?)v.Value : null,
            v => v.HasValue ? (int?)checked((int)v.Value) : null);

        modelBuilder.Entity<Client>(entity =>
        {
            entity.ToTable("Client");
            entity.HasKey(e => e.Id);
            
            entity.Property(e => e.Id)
                .HasColumnName("ClientId")
                .HasColumnType("bigint")
                .HasConversion(intToLongConverter);
            
            entity.Property(e => e.ClientName).IsRequired().HasMaxLength(200);
            entity.Property(e => e.ExternalClientId).IsRequired();
            entity.HasIndex(e => e.ExternalClientId).IsUnique();
            entity.HasIndex(e => e.LastSyncedDateTime);
            entity.Property(e => e.ContactEmail).HasMaxLength(255);
            entity.Property(e => e.ContactPhone).HasMaxLength(50);
        });

        modelBuilder.Entity<User>(entity =>
        {
            entity.ToTable("User");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("UserId");
            entity.Property(e => e.Username).IsRequired().HasMaxLength(100);
            entity.Property(e => e.Email).IsRequired().HasMaxLength(255);
            entity.HasIndex(e => e.Email).IsUnique();
            entity.Property(e => e.FirstName).IsRequired().HasMaxLength(100);
            entity.Property(e => e.LastName).IsRequired().HasMaxLength(100);
            entity.Property(e => e.ExternalUserId).HasMaxLength(255);
            entity.Property(e => e.ExternalIssuer).HasMaxLength(500);
            entity.Property(e => e.PasswordHash).HasMaxLength(500);
            entity.HasIndex(e => new { e.ExternalIssuer, e.ExternalUserId });
            
            entity.Property(e => e.ClientId)
                .HasColumnType("bigint")
                .HasConversion(intToLongConverter);
            
            entity.HasOne(e => e.Client)
                .WithMany(c => c.Users)
                .HasForeignKey(e => e.ClientId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<UserPermission>(entity =>
        {
            entity.ToTable("UserPermission");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("UserPermissionId");
            entity.Property(e => e.PermissionName).IsRequired().HasMaxLength(100);
            entity.Property(e => e.ResourceType).HasMaxLength(50);
            
            entity.HasOne(e => e.User)
                .WithMany(u => u.Permissions)
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<PasswordHistory>(entity =>
        {
            entity.ToTable("PasswordHistory");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("PasswordHistoryId");
            entity.Property(e => e.PasswordHash).IsRequired().HasMaxLength(500);
            entity.Property(e => e.ChangedDateTime).IsRequired();
            
            entity.HasOne(e => e.User)
                .WithMany(u => u.PasswordHistories)
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);
                
            entity.HasIndex(e => new { e.UserId, e.ChangedDateTime });
        });

        modelBuilder.Entity<Schedule>(entity =>
        {
            entity.ToTable("Schedule");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("ScheduleId");
            entity.Property(e => e.Name).IsRequired().HasMaxLength(200);
            entity.Property(e => e.Description).HasMaxLength(1000);
            entity.Property(e => e.CronExpression).IsRequired().HasMaxLength(100);
            entity.Property(e => e.TimeZone).HasMaxLength(100);
            entity.Property(e => e.JobConfiguration).HasColumnType("nvarchar(max)");
            
            entity.Property(e => e.ClientId)
                .HasColumnType("bigint")
                .HasConversion(intToLongConverter);
            
            entity.HasOne(e => e.Client)
                .WithMany(c => c.Schedules)
                .HasForeignKey(e => e.ClientId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<JobExecution>(entity =>
        {
            entity.ToTable("JobExecution");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("JobExecutionId");
            entity.Property(e => e.Output).HasColumnType("nvarchar(max)");
            entity.Property(e => e.ErrorMessage).HasColumnType("nvarchar(max)");
            entity.Property(e => e.StackTrace).HasColumnType("nvarchar(max)");
            entity.Property(e => e.TriggeredBy).HasMaxLength(100);
            entity.Property(e => e.CancelledBy).HasMaxLength(100);
            
            entity.HasOne(e => e.Schedule)
                .WithMany(s => s.JobExecutions)
                .HasForeignKey(e => e.ScheduleId)
                .OnDelete(DeleteBehavior.Cascade);
                
            entity.HasIndex(e => e.StartDateTime);
            entity.HasIndex(e => e.Status);
        });

        modelBuilder.Entity<JobParameter>(entity =>
        {
            entity.ToTable("JobParameter");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("JobParameterId");
            entity.Property(e => e.ParameterName).IsRequired().HasMaxLength(100);
            entity.Property(e => e.ParameterType).IsRequired().HasMaxLength(50);
            entity.Property(e => e.ParameterValue).HasColumnType("nvarchar(max)");
            entity.Property(e => e.SourceQuery).HasColumnType("nvarchar(max)");
            entity.Property(e => e.SourceConnectionString).HasMaxLength(500);
            
            entity.HasOne(e => e.Schedule)
                .WithMany(s => s.JobParameters)
                .HasForeignKey(e => e.ScheduleId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<AuditLog>(entity =>
        {
            entity.ToTable("AuditLog");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("AuditLogId");
            entity.Property(e => e.EventType).IsRequired().HasMaxLength(100);
            entity.Property(e => e.EntityType).IsRequired().HasMaxLength(100);
            entity.Property(e => e.Action).IsRequired().HasMaxLength(50);
            entity.Property(e => e.UserName).IsRequired().HasMaxLength(200);
            entity.Property(e => e.IpAddress).HasMaxLength(50);
            entity.Property(e => e.UserAgent).HasMaxLength(500);
            entity.Property(e => e.OldValues).HasColumnType("nvarchar(max)");
            entity.Property(e => e.NewValues).HasColumnType("nvarchar(max)");
            entity.Property(e => e.AdditionalData).HasColumnType("nvarchar(max)");
            entity.HasIndex(e => e.TimestampDateTime);
            entity.HasIndex(e => new { e.EntityType, e.EntityId });
        });

        modelBuilder.Entity<NotificationSetting>(entity =>
        {
            entity.ToTable("NotificationSetting");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("NotificationSettingId");
            entity.Property(e => e.SuccessEmailRecipients).HasMaxLength(1000);
            entity.Property(e => e.FailureEmailRecipients).HasMaxLength(1000);
            entity.Property(e => e.SuccessEmailSubject).HasMaxLength(500);
            entity.Property(e => e.FailureEmailSubject).HasMaxLength(500);
            entity.HasOne(e => e.Schedule)
                .WithOne(s => s.NotificationSetting)
                .HasForeignKey<NotificationSetting>(e => e.ScheduleId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ScheduleSyncSource>(entity =>
        {
            entity.ToTable("ScheduleSyncSource");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("ScheduleSyncSourceId");
            entity.Property(e => e.AccountNumber).IsRequired().HasMaxLength(128);
            entity.Property(e => e.AccountName).HasMaxLength(64);
            entity.Property(e => e.VendorName).HasMaxLength(64);
            entity.Property(e => e.ClientName).HasMaxLength(64);
            entity.Property(e => e.TandemAccountId).HasMaxLength(64);
            
            entity.Property(e => e.ClientId)
                .HasColumnType("bigint")
                .HasConversion(nullableIntToLongConverter);
            
            entity.HasOne(e => e.Client)
                .WithMany(c => c.ScheduleSyncSources)
                .HasForeignKey(e => e.ClientId)
                .OnDelete(DeleteBehavior.Restrict);
                
            entity.HasIndex(e => e.ExternalAccountId).IsUnique();
            entity.HasIndex(e => e.ExternalClientId);
            entity.HasIndex(e => e.ExternalVendorId);
            entity.HasIndex(e => e.ClientId);
            entity.HasIndex(e => e.LastSyncedDateTime);
            entity.HasIndex(e => new { e.ExternalClientId, e.ExternalVendorId, e.AccountNumber });
        });

        // ADR Account Rule entity configuration
        modelBuilder.Entity<AdrAccountRule>(entity =>
        {
            entity.ToTable("AdrAccountRule");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("AdrAccountRuleId");
            
            entity.Property(e => e.RuleName).IsRequired().HasMaxLength(200);
            entity.Property(e => e.PeriodType).HasMaxLength(13);
            entity.Property(e => e.OverriddenBy).HasMaxLength(200);
            entity.Property(e => e.Notes).HasColumnType("nvarchar(max)");
            
                        entity.HasOne(e => e.AdrAccount)
                            .WithMany(a => a.AdrAccountRules)
                            .HasForeignKey(e => e.AdrAccountId)
                            .OnDelete(DeleteBehavior.Cascade);
            
                        entity.HasOne(e => e.AdrJobType)
                            .WithMany(jt => jt.AdrAccountRules)
                            .HasForeignKey(e => e.JobTypeId)
                            .OnDelete(DeleteBehavior.NoAction);
            
                        entity.HasIndex(e => e.AdrAccountId);
            entity.HasIndex(e => e.JobTypeId);
            entity.HasIndex(e => e.IsEnabled);
            entity.HasIndex(e => e.NextRunDateTime);
            entity.HasIndex(e => new { e.AdrAccountId, e.JobTypeId });
            entity.HasIndex(e => new { e.IsDeleted, e.IsEnabled, e.NextRunDateTime });
            // Optimized index for sync query: WHERE AdrAccountId IN (...) AND IsDeleted = 0 AND JobTypeId = 2
            entity.HasIndex(e => new { e.IsDeleted, e.JobTypeId, e.AdrAccountId });
        });

        // ADR Account entity configuration
        modelBuilder.Entity<AdrAccount>(entity =>
        {
            entity.ToTable("AdrAccount");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("AdrAccountId");
            
            entity.Property(e => e.VMAccountNumber).IsRequired().HasMaxLength(128);
            entity.Property(e => e.InterfaceAccountId).HasMaxLength(128);
            entity.Property(e => e.ClientName).HasMaxLength(128);
            entity.Property(e => e.PrimaryVendorCode).HasMaxLength(128);
            entity.Property(e => e.MasterVendorCode).HasMaxLength(128);
            entity.Property(e => e.PeriodType).HasMaxLength(13);
            entity.Property(e => e.NextRunStatus).HasMaxLength(10);
            entity.Property(e => e.HistoricalBillingStatus).HasMaxLength(10);
            entity.Property(e => e.OverriddenBy).HasMaxLength(200);
            
            entity.Property(e => e.ClientId)
                .HasColumnType("bigint")
                .HasConversion(nullableIntToLongConverter);
            
            entity.HasOne(e => e.Client)
                .WithMany()
                .HasForeignKey(e => e.ClientId)
                .OnDelete(DeleteBehavior.Restrict);
            
            entity.HasIndex(e => e.VMAccountId);
            entity.HasIndex(e => e.VMAccountNumber);
            entity.HasIndex(e => e.CredentialId);
            entity.HasIndex(e => e.ClientId);
            entity.HasIndex(e => e.NextRunStatus);
            entity.HasIndex(e => e.HistoricalBillingStatus);
            entity.HasIndex(e => new { e.VMAccountId, e.VMAccountNumber });
            
            // Performance indexes for paged queries
            entity.HasIndex(e => e.NextRunDateTime);
            entity.HasIndex(e => e.InterfaceAccountId);
            entity.HasIndex(e => e.PrimaryVendorCode);
            entity.HasIndex(e => e.MasterVendorCode);
            // Composite indexes for common filter + sort patterns
            entity.HasIndex(e => new { e.IsDeleted, e.NextRunStatus, e.NextRunDateTime });
            entity.HasIndex(e => new { e.IsDeleted, e.HistoricalBillingStatus });
            entity.HasIndex(e => new { e.IsDeleted, e.ClientId, e.NextRunStatus });
        });

        // ADR Job entity configuration
        modelBuilder.Entity<AdrJob>(entity =>
        {
            entity.ToTable("AdrJob");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("AdrJobId");
            
            entity.Property(e => e.VMAccountNumber).IsRequired().HasMaxLength(128);
            entity.Property(e => e.PrimaryVendorCode).HasMaxLength(128);
            entity.Property(e => e.MasterVendorCode).HasMaxLength(128);
            entity.Property(e => e.PeriodType).HasMaxLength(13);
            entity.Property(e => e.Status).IsRequired().HasMaxLength(50);
            entity.Property(e => e.AdrStatusDescription).HasMaxLength(100);
            entity.Property(e => e.ErrorMessage).HasColumnType("nvarchar(max)");
            
            entity.HasOne(e => e.AdrAccount)
                .WithMany(a => a.AdrJobs)
                .HasForeignKey(e => e.AdrAccountId)
                .OnDelete(DeleteBehavior.Cascade);
            
                        entity.HasOne(e => e.AdrAccountRule)
                            .WithMany(r => r.AdrJobs)
                            .HasForeignKey(e => e.AdrAccountRuleId)
                            .OnDelete(DeleteBehavior.NoAction);
            
            entity.HasIndex(e => e.AdrAccountId);
            entity.HasIndex(e => e.AdrAccountRuleId);
            entity.HasIndex(e => e.VMAccountId);
            entity.HasIndex(e => e.CredentialId);
            entity.HasIndex(e => e.Status);
            entity.HasIndex(e => e.BillingPeriodStartDateTime);
            entity.HasIndex(e => new { e.AdrAccountId, e.BillingPeriodStartDateTime, e.BillingPeriodEndDateTime });
            
            // Performance indexes for paged queries and search
            entity.HasIndex(e => e.VMAccountNumber);
            entity.HasIndex(e => e.PrimaryVendorCode);
            entity.HasIndex(e => e.MasterVendorCode);
            entity.HasIndex(e => e.ModifiedDateTime);
            // Composite indexes for common filter + sort patterns
            entity.HasIndex(e => new { e.IsDeleted, e.Status });
            entity.HasIndex(e => new { e.IsDeleted, e.Status, e.BillingPeriodStartDateTime });
            entity.HasIndex(e => new { e.IsDeleted, e.AdrAccountId, e.BillingPeriodStartDateTime });
            // Performance indexes for export queries that need latest job status per account
            entity.HasIndex(e => e.ScrapingCompletedDateTime);
            entity.HasIndex(e => new { e.IsDeleted, e.AdrAccountId, e.ScrapingCompletedDateTime });
        });

        // ADR Job Execution entity configuration
        modelBuilder.Entity<AdrJobExecution>(entity =>
        {
            entity.ToTable("AdrJobExecution");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("AdrJobExecutionId");
            
            entity.Property(e => e.AdrStatusDescription).HasMaxLength(100);
            entity.Property(e => e.ErrorMessage).HasColumnType("nvarchar(max)");
            entity.Property(e => e.ApiResponse).HasColumnType("nvarchar(max)");
            entity.Property(e => e.RequestPayload).HasColumnType("nvarchar(max)");
            
            entity.HasOne(e => e.AdrJob)
                .WithMany(j => j.AdrJobExecutions)
                .HasForeignKey(e => e.AdrJobId)
                .OnDelete(DeleteBehavior.Cascade);
            
            entity.HasIndex(e => e.AdrJobId);
            entity.HasIndex(e => e.StartDateTime);
            entity.HasIndex(e => e.AdrRequestTypeId);
            entity.HasIndex(e => e.IsSuccess);
        });

        // ADR Orchestration Run entity configuration
        modelBuilder.Entity<AdrOrchestrationRun>(entity =>
        {
            entity.ToTable("AdrOrchestrationRun");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("AdrOrchestrationRunId");
            
            entity.Property(e => e.RequestId).IsRequired().HasMaxLength(50);
            entity.Property(e => e.RequestedBy).IsRequired().HasMaxLength(200);
            entity.Property(e => e.Status).IsRequired().HasMaxLength(20);
            entity.Property(e => e.CurrentStep).HasMaxLength(50);
            entity.Property(e => e.CurrentProgress).HasMaxLength(50);
            entity.Property(e => e.ErrorMessage).HasColumnType("nvarchar(max)");
            
            entity.HasIndex(e => e.RequestId).IsUnique();
            entity.HasIndex(e => e.RequestedDateTime);
            entity.HasIndex(e => e.Status);
            entity.HasIndex(e => new { e.Status, e.RequestedDateTime });
        });

        // ADR Configuration entity configuration (single-row table for global settings)
        modelBuilder.Entity<AdrConfiguration>(entity =>
        {
            entity.ToTable("AdrConfiguration");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("AdrConfigurationId");
            
            entity.Property(e => e.MissingInvoiceAlertEmail).HasMaxLength(255);
            entity.Property(e => e.Notes).HasColumnType("nvarchar(max)");
        });

        // ADR Account Blacklist entity configuration
        modelBuilder.Entity<AdrAccountBlacklist>(entity =>
        {
            entity.ToTable("AdrAccountBlacklist");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("AdrAccountBlacklistId");
            
            entity.Property(e => e.PrimaryVendorCode).HasMaxLength(128);
            entity.Property(e => e.MasterVendorCode).HasMaxLength(128);
            entity.Property(e => e.VMAccountNumber).HasMaxLength(128);
            entity.Property(e => e.ExclusionType).IsRequired().HasMaxLength(20);
            entity.Property(e => e.Reason).IsRequired().HasMaxLength(500);
            entity.Property(e => e.BlacklistedBy).HasMaxLength(200);
            entity.Property(e => e.Notes).HasColumnType("nvarchar(max)");
            
            entity.HasIndex(e => e.PrimaryVendorCode);
            entity.HasIndex(e => e.MasterVendorCode);
            entity.HasIndex(e => e.VMAccountId);
            entity.HasIndex(e => e.VMAccountNumber);
            entity.HasIndex(e => e.CredentialId);
            entity.HasIndex(e => e.IsActive);
            entity.HasIndex(e => new { e.IsDeleted, e.IsActive });
            entity.HasIndex(e => new { e.PrimaryVendorCode, e.VMAccountId, e.CredentialId });
            entity.HasIndex(e => new { e.MasterVendorCode, e.VMAccountId, e.CredentialId });
        });

        // ADR Job Type entity configuration (replaces hardcoded AdrRequestType enum)
        modelBuilder.Entity<AdrJobType>(entity =>
        {
            entity.ToTable("AdrJobType");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("AdrJobTypeId");
            
            entity.Property(e => e.Code).IsRequired().HasMaxLength(50);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(100);
            entity.Property(e => e.Description).HasMaxLength(500);
            entity.Property(e => e.EndpointUrl).HasMaxLength(500);
            entity.Property(e => e.Notes).HasColumnType("nvarchar(max)");
            
            entity.HasIndex(e => e.Code).IsUnique();
            entity.HasIndex(e => e.IsActive);
            entity.HasIndex(e => e.AdrRequestTypeId);
        });

        // Power BI Report entity configuration
        modelBuilder.Entity<PowerBiReport>(entity =>
        {
            entity.ToTable("PowerBiReport");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("PowerBiReportId");
            
            entity.Property(e => e.Name).IsRequired().HasMaxLength(200);
            entity.Property(e => e.Url).IsRequired().HasMaxLength(2000);
            entity.Property(e => e.Description).HasMaxLength(500);
            entity.Property(e => e.Category).IsRequired().HasMaxLength(50);
            
            entity.HasIndex(e => e.Category);
            entity.HasIndex(e => e.IsActive);
            entity.HasIndex(e => new { e.Category, e.DisplayOrder });
            entity.HasIndex(e => new { e.IsDeleted, e.IsActive, e.Category, e.DisplayOrder });
        });

        // Archive table configurations
        modelBuilder.Entity<AdrJobArchive>(entity =>
        {
            entity.ToTable("AdrJobArchive");
            entity.HasKey(e => e.AdrJobArchiveId);
            
            entity.Property(e => e.VMAccountNumber).IsRequired().HasMaxLength(128);
            entity.Property(e => e.PrimaryVendorCode).HasMaxLength(128);
            entity.Property(e => e.MasterVendorCode).HasMaxLength(128);
            entity.Property(e => e.PeriodType).HasMaxLength(13);
            entity.Property(e => e.Status).IsRequired().HasMaxLength(50);
            entity.Property(e => e.AdrStatusDescription).HasMaxLength(100);
            entity.Property(e => e.ErrorMessage).HasColumnType("nvarchar(max)");
            entity.Property(e => e.ManualRequestReason).HasColumnType("nvarchar(max)");
            entity.Property(e => e.LastStatusCheckResponse).HasColumnType("nvarchar(max)");
            entity.Property(e => e.CreatedBy).IsRequired().HasMaxLength(200);
            entity.Property(e => e.ModifiedBy).IsRequired().HasMaxLength(200);
            entity.Property(e => e.ArchivedBy).IsRequired().HasMaxLength(200);
            
            entity.HasIndex(e => e.OriginalAdrJobId);
            entity.HasIndex(e => e.AdrAccountId);
            entity.HasIndex(e => e.VMAccountId);
            entity.HasIndex(e => e.ArchivedDateTime);
            entity.HasIndex(e => e.BillingPeriodStartDateTime);
        });

        modelBuilder.Entity<AdrJobExecutionArchive>(entity =>
        {
            entity.ToTable("AdrJobExecutionArchive");
            entity.HasKey(e => e.AdrJobExecutionArchiveId);
            
            entity.Property(e => e.AdrStatusDescription).HasMaxLength(100);
            entity.Property(e => e.ErrorMessage).HasColumnType("nvarchar(max)");
            entity.Property(e => e.ApiResponse).HasColumnType("nvarchar(max)");
            entity.Property(e => e.RequestPayload).HasColumnType("nvarchar(max)");
            entity.Property(e => e.CreatedBy).IsRequired().HasMaxLength(200);
            entity.Property(e => e.ModifiedBy).IsRequired().HasMaxLength(200);
            entity.Property(e => e.ArchivedBy).IsRequired().HasMaxLength(200);
            
            entity.HasIndex(e => e.OriginalAdrJobExecutionId);
            entity.HasIndex(e => e.AdrJobId);
            entity.HasIndex(e => e.ArchivedDateTime);
            entity.HasIndex(e => e.StartDateTime);
        });

        modelBuilder.Entity<AuditLogArchive>(entity =>
        {
            entity.ToTable("AuditLogArchive");
            entity.HasKey(e => e.AuditLogArchiveId);
            
            entity.Property(e => e.EventType).IsRequired().HasMaxLength(100);
            entity.Property(e => e.EntityType).IsRequired().HasMaxLength(100);
            entity.Property(e => e.Action).IsRequired().HasMaxLength(50);
            entity.Property(e => e.UserName).IsRequired().HasMaxLength(200);
            entity.Property(e => e.IpAddress).HasMaxLength(50);
            entity.Property(e => e.UserAgent).HasMaxLength(500);
            entity.Property(e => e.OldValues).HasColumnType("nvarchar(max)");
            entity.Property(e => e.NewValues).HasColumnType("nvarchar(max)");
            entity.Property(e => e.AdditionalData).HasColumnType("nvarchar(max)");
            entity.Property(e => e.CreatedBy).IsRequired().HasMaxLength(200);
            entity.Property(e => e.ModifiedBy).IsRequired().HasMaxLength(200);
            entity.Property(e => e.ArchivedBy).IsRequired().HasMaxLength(200);
            
            entity.HasIndex(e => e.OriginalAuditLogId);
            entity.HasIndex(e => e.ArchivedDateTime);
            entity.HasIndex(e => e.TimestampDateTime);
            entity.HasIndex(e => new { e.EntityType, e.EntityId });
        });

        modelBuilder.Entity<JobExecutionArchive>(entity =>
        {
            entity.ToTable("JobExecutionArchive");
            entity.HasKey(e => e.JobExecutionArchiveId);
            
            entity.Property(e => e.Output).HasColumnType("nvarchar(max)");
            entity.Property(e => e.ErrorMessage).HasColumnType("nvarchar(max)");
            entity.Property(e => e.StackTrace).HasColumnType("nvarchar(max)");
            entity.Property(e => e.TriggeredBy).HasMaxLength(100);
            entity.Property(e => e.CancelledBy).HasMaxLength(100);
            entity.Property(e => e.CreatedBy).IsRequired().HasMaxLength(200);
            entity.Property(e => e.ModifiedBy).IsRequired().HasMaxLength(200);
            entity.Property(e => e.ArchivedBy).IsRequired().HasMaxLength(200);
            
            entity.HasIndex(e => e.OriginalJobExecutionId);
            entity.HasIndex(e => e.ScheduleId);
            entity.HasIndex(e => e.ArchivedDateTime);
            entity.HasIndex(e => e.StartDateTime);
            entity.HasIndex(e => e.Status);
        });
    }
}
