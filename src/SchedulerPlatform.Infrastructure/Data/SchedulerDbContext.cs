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
    }
}
