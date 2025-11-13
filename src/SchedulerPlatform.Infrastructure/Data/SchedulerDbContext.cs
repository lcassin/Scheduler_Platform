using Microsoft.EntityFrameworkCore;
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
    public DbSet<Schedule> Schedules { get; set; }
    public DbSet<JobExecution> JobExecutions { get; set; }
    public DbSet<JobParameter> JobParameters { get; set; }
    public DbSet<VendorCredential> VendorCredentials { get; set; }
    public DbSet<AuditLog> AuditLogs { get; set; }
    public DbSet<NotificationSetting> NotificationSettings { get; set; }
    public DbSet<ScheduleSyncSource> ScheduleSyncSources { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Client>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.ClientName).IsRequired().HasMaxLength(200);
            entity.Property(e => e.ClientCode).IsRequired().HasMaxLength(50);
            entity.HasIndex(e => e.ClientCode).IsUnique();
            entity.Property(e => e.ContactEmail).HasMaxLength(255);
            entity.Property(e => e.ContactPhone).HasMaxLength(50);
        });

        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Username).IsRequired().HasMaxLength(100);
            entity.Property(e => e.Email).IsRequired().HasMaxLength(255);
            entity.HasIndex(e => e.Email).IsUnique();
            entity.Property(e => e.FirstName).IsRequired().HasMaxLength(100);
            entity.Property(e => e.LastName).IsRequired().HasMaxLength(100);
            entity.Property(e => e.ExternalUserId).HasMaxLength(255);
            entity.Property(e => e.ExternalIssuer).HasMaxLength(500);
            entity.Property(e => e.PasswordHash).HasMaxLength(500);
            entity.HasIndex(e => new { e.ExternalIssuer, e.ExternalUserId });
            
            entity.HasOne(e => e.Client)
                .WithMany(c => c.Users)
                .HasForeignKey(e => e.ClientId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<UserPermission>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.PermissionName).IsRequired().HasMaxLength(100);
            entity.Property(e => e.ResourceType).HasMaxLength(50);
            
            entity.HasOne(e => e.User)
                .WithMany(u => u.Permissions)
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Schedule>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(200);
            entity.Property(e => e.Description).HasMaxLength(1000);
            entity.Property(e => e.CronExpression).IsRequired().HasMaxLength(100);
            entity.Property(e => e.TimeZone).HasMaxLength(100);
            entity.Property(e => e.JobConfiguration).HasColumnType("nvarchar(max)");
            
            entity.HasOne(e => e.Client)
                .WithMany(c => c.Schedules)
                .HasForeignKey(e => e.ClientId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<JobExecution>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Output).HasColumnType("nvarchar(max)");
            entity.Property(e => e.ErrorMessage).HasColumnType("nvarchar(max)");
            entity.Property(e => e.StackTrace).HasColumnType("nvarchar(max)");
            entity.Property(e => e.TriggeredBy).HasMaxLength(100);
            entity.Property(e => e.CancelledBy).HasMaxLength(100);
            
            entity.HasOne(e => e.Schedule)
                .WithMany(s => s.JobExecutions)
                .HasForeignKey(e => e.ScheduleId)
                .OnDelete(DeleteBehavior.Cascade);
                
            entity.HasIndex(e => e.StartTime);
            entity.HasIndex(e => e.Status);
        });

        modelBuilder.Entity<JobParameter>(entity =>
        {
            entity.HasKey(e => e.Id);
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

        modelBuilder.Entity<VendorCredential>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.VendorName).IsRequired().HasMaxLength(200);
            entity.Property(e => e.VendorUrl).IsRequired().HasMaxLength(500);
            entity.Property(e => e.Username).IsRequired().HasMaxLength(200);
            entity.Property(e => e.EncryptedPassword).IsRequired().HasMaxLength(500);
            entity.Property(e => e.AdditionalData).HasColumnType("nvarchar(max)");
            
            entity.HasOne(e => e.Client)
                .WithMany(c => c.VendorCredentials)
                .HasForeignKey(e => e.ClientId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<AuditLog>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.EventType).IsRequired().HasMaxLength(100);
            entity.Property(e => e.EntityType).IsRequired().HasMaxLength(100);
            entity.Property(e => e.Action).IsRequired().HasMaxLength(50);
            entity.Property(e => e.UserName).IsRequired().HasMaxLength(200);
            entity.Property(e => e.IpAddress).HasMaxLength(50);
            entity.Property(e => e.UserAgent).HasMaxLength(500);
            entity.Property(e => e.OldValues).HasColumnType("nvarchar(max)");
            entity.Property(e => e.NewValues).HasColumnType("nvarchar(max)");
            entity.Property(e => e.AdditionalData).HasColumnType("nvarchar(max)");
            entity.HasIndex(e => e.Timestamp);
            entity.HasIndex(e => new { e.EntityType, e.EntityId });
        });

        modelBuilder.Entity<NotificationSetting>(entity =>
        {
            entity.HasKey(e => e.Id);
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
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Vendor).IsRequired().HasMaxLength(200);
            entity.Property(e => e.AccountNumber).IsRequired().HasMaxLength(100);
            
            entity.HasOne(e => e.Client)
                .WithMany(c => c.ScheduleSyncSources)
                .HasForeignKey(e => e.ClientId)
                .OnDelete(DeleteBehavior.Restrict);
                
            entity.HasIndex(e => new { e.ClientId, e.Vendor, e.AccountNumber });
            entity.HasIndex(e => e.ScheduleFrequency);
            entity.HasIndex(e => e.ScheduleDate);
            entity.HasIndex(e => e.ClientId);
        });
    }
}
