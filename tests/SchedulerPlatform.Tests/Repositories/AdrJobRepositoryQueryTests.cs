using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using SchedulerPlatform.Core.Domain.Entities;
using SchedulerPlatform.Infrastructure.Data;
using SchedulerPlatform.Infrastructure.Repositories;
using Xunit;

namespace SchedulerPlatform.Tests.Repositories;

/// <summary>
/// Unit tests for AdrJobRepository query methods.
/// Tests cover the critical queries used by the ADR orchestration process:
/// - Jobs needing credential verification (within lead days window)
/// - Jobs ready for scraping (on or after NextRunDateTime)
/// - Jobs needing status check (after scrape request)
/// - Jobs needing daily and final status checks
/// </summary>
public class AdrJobRepositoryQueryTests : IDisposable
{
    private readonly ApplicationDbContext _context;
    private readonly AdrJobRepository _repository;

    public AdrJobRepositoryQueryTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new ApplicationDbContext(options);
        _repository = new AdrJobRepository(_context);
    }

    public void Dispose()
    {
        _context.Dispose();
    }

    #region Credential Verification Query Tests

    [Fact]
    public async Task GetJobsNeedingCredentialVerificationAsync_ReturnsJobsWithinLeadDays()
    {
        // Arrange - Job with NextRunDateTime 5 days from now (within 7-day window)
        var currentDate = DateTime.UtcNow.Date;
        var job = CreateTestJob(1, "Pending", currentDate.AddDays(5));
        await _context.AdrJobs.AddAsync(job);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.GetJobsNeedingCredentialVerificationAsync(currentDate, 7);

        // Assert
        result.Should().HaveCount(1);
        result.First().Id.Should().Be(1);
    }

    [Fact]
    public async Task GetJobsNeedingCredentialVerificationAsync_ExcludesJobsOutsideLeadDays()
    {
        // Arrange - Job with NextRunDateTime 10 days from now (outside 7-day window)
        var currentDate = DateTime.UtcNow.Date;
        var job = CreateTestJob(1, "Pending", currentDate.AddDays(10));
        await _context.AdrJobs.AddAsync(job);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.GetJobsNeedingCredentialVerificationAsync(currentDate, 7);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetJobsNeedingCredentialVerificationAsync_ExcludesAlreadyVerifiedJobs()
    {
        // Arrange - Job already has CredentialVerified status
        var currentDate = DateTime.UtcNow.Date;
        var job = CreateTestJob(1, "CredentialVerified", currentDate.AddDays(5));
        await _context.AdrJobs.AddAsync(job);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.GetJobsNeedingCredentialVerificationAsync(currentDate, 7);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetJobsNeedingCredentialVerificationAsync_ExcludesJobsOnNextRunDate()
    {
        // Arrange - Job with NextRunDateTime = today (should be scraping, not credential check)
        var currentDate = DateTime.UtcNow.Date;
        var job = CreateTestJob(1, "Pending", currentDate);
        await _context.AdrJobs.AddAsync(job);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.GetJobsNeedingCredentialVerificationAsync(currentDate, 7);

        // Assert - Jobs on NextRunDate should NOT be picked up for credential verification
        // They should be picked up for scraping instead
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetJobsNeedingCredentialVerificationAsync_ExcludesJobsInPast()
    {
        // Arrange - Job with NextRunDateTime in the past
        var currentDate = DateTime.UtcNow.Date;
        var job = CreateTestJob(1, "Pending", currentDate.AddDays(-2));
        await _context.AdrJobs.AddAsync(job);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.GetJobsNeedingCredentialVerificationAsync(currentDate, 7);

        // Assert
        result.Should().BeEmpty();
    }

    #endregion

    #region Scraping Query Tests

    [Fact]
    public async Task GetJobsReadyForScrapingAsync_ReturnsJobsOnOrAfterNextRunDate()
    {
        // Arrange - Job with NextRunDateTime = today
        var currentDate = DateTime.UtcNow.Date;
        var job = CreateTestJob(1, "CredentialVerified", currentDate);
        await _context.AdrJobs.AddAsync(job);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.GetJobsReadyForScrapingAsync(currentDate);

        // Assert
        result.Should().HaveCount(1);
    }

    [Fact]
    public async Task GetJobsReadyForScrapingAsync_ExcludesJobsBeforeNextRunDate()
    {
        // Arrange - Job with NextRunDateTime in the future
        var currentDate = DateTime.UtcNow.Date;
        var job = CreateTestJob(1, "CredentialVerified", currentDate.AddDays(3));
        await _context.AdrJobs.AddAsync(job);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.GetJobsReadyForScrapingAsync(currentDate);

        // Assert - Should NOT include jobs before their NextRunDate
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetJobsReadyForScrapingAsync_IncludesCredentialFailedJobs()
    {
        // Arrange - CredentialFailed jobs should still proceed to scraping
        // (helpdesk may have fixed credentials in the meantime)
        var currentDate = DateTime.UtcNow.Date;
        var job = CreateTestJob(1, "CredentialFailed", currentDate);
        await _context.AdrJobs.AddAsync(job);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.GetJobsReadyForScrapingAsync(currentDate);

        // Assert
        result.Should().HaveCount(1);
    }

    [Fact]
    public async Task GetJobsReadyForScrapingAsync_ExcludesAlreadyScrapedJobs()
    {
        // Arrange - Job already has ScrapeRequested status
        var currentDate = DateTime.UtcNow.Date;
        var job = CreateTestJob(1, "ScrapeRequested", currentDate);
        await _context.AdrJobs.AddAsync(job);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.GetJobsReadyForScrapingAsync(currentDate);

        // Assert
        result.Should().BeEmpty();
    }

    #endregion

    #region Status Check Query Tests

    [Fact]
    public async Task GetJobsNeedingDailyStatusCheckAsync_ReturnsJobsScrapedYesterday()
    {
        // Arrange - Job scraped yesterday (1 day delay for status check)
        var currentDate = DateTime.UtcNow.Date;
        var job = CreateTestJob(1, "ScrapeRequested", currentDate.AddDays(-1));
        job.ScrapingRequestedDateTime = currentDate.AddDays(-1);
        await _context.AdrJobs.AddAsync(job);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.GetJobsNeedingDailyStatusCheckAsync(currentDate, 1);

        // Assert
        result.Should().HaveCount(1);
    }

    [Fact]
    public async Task GetJobsNeedingDailyStatusCheckAsync_ExcludesJobsScrapedToday()
    {
        // Arrange - Job scraped today (too soon for status check)
        var currentDate = DateTime.UtcNow.Date;
        var job = CreateTestJob(1, "ScrapeRequested", currentDate);
        job.ScrapingRequestedDateTime = currentDate;
        await _context.AdrJobs.AddAsync(job);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.GetJobsNeedingDailyStatusCheckAsync(currentDate, 1);

        // Assert - Should NOT include jobs scraped today
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetJobsNeedingFinalStatusCheckAsync_ReturnsJobsAfterBillingWindowEnds()
    {
        // Arrange - Job with billing window ended 5+ days ago
        var currentDate = DateTime.UtcNow.Date;
        var job = CreateTestJob(1, "ScrapeRequested", currentDate.AddDays(-10));
        job.BillingPeriodEndDateTime = currentDate.AddDays(-6);
        job.NextRangeEndDateTime = currentDate.AddDays(-6);
        await _context.AdrJobs.AddAsync(job);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.GetJobsNeedingFinalStatusCheckAsync(currentDate, 5);

        // Assert
        result.Should().HaveCount(1);
    }

    #endregion

    #region Billing Period Existence Tests

    [Fact]
    public async Task ExistsForBillingPeriodAsync_ReturnsTrueWhenJobExists()
    {
        // Arrange
        var startDate = new DateTime(2024, 12, 1);
        var endDate = new DateTime(2024, 12, 31);
        var job = CreateTestJob(1, "Pending", DateTime.UtcNow.AddDays(5));
        job.AdrAccountId = 100;
        job.BillingPeriodStartDateTime = startDate;
        job.BillingPeriodEndDateTime = endDate;
        await _context.AdrJobs.AddAsync(job);
        await _context.SaveChangesAsync();

        // Act
        var exists = await _repository.ExistsForBillingPeriodAsync(100, startDate, endDate);

        // Assert
        exists.Should().BeTrue();
    }

    [Fact]
    public async Task ExistsForBillingPeriodAsync_ReturnsFalseWhenNoJobExists()
    {
        // Arrange - No jobs in database

        // Act
        var exists = await _repository.ExistsForBillingPeriodAsync(100, 
            new DateTime(2024, 12, 1), 
            new DateTime(2024, 12, 31));

        // Assert
        exists.Should().BeFalse();
    }

    [Fact]
    public async Task ExistsForBillingPeriodAsync_ReturnsFalseForDifferentAccount()
    {
        // Arrange - Job exists but for different account
        var startDate = new DateTime(2024, 12, 1);
        var endDate = new DateTime(2024, 12, 31);
        var job = CreateTestJob(1, "Pending", DateTime.UtcNow.AddDays(5));
        job.AdrAccountId = 100;
        job.BillingPeriodStartDateTime = startDate;
        job.BillingPeriodEndDateTime = endDate;
        await _context.AdrJobs.AddAsync(job);
        await _context.SaveChangesAsync();

        // Act - Check for different account
        var exists = await _repository.ExistsForBillingPeriodAsync(200, startDate, endDate);

        // Assert
        exists.Should().BeFalse();
    }

    #endregion

    #region Helper Methods

    private static AdrJob CreateTestJob(int id, string status, DateTime? nextRunDateTime = null)
    {
        var now = DateTime.UtcNow;
        return new AdrJob
        {
            Id = id,
            AdrAccountId = 100 + id,
            VMAccountId = 1000 + id,
            VMAccountNumber = $"ACC{id:D4}",
            VendorCode = "TEST",
            CredentialId = 100 + id,
            Status = status,
            NextRunDateTime = nextRunDateTime ?? now.AddDays(1),
            NextRangeStartDateTime = now.AddDays(-30),
            NextRangeEndDateTime = now.AddDays(1),
            BillingPeriodStartDateTime = now.AddDays(-30),
            BillingPeriodEndDateTime = now.AddDays(1),
            PeriodType = "Monthly",
            RetryCount = 0,
            CreatedDateTime = now,
            CreatedBy = "Test",
            ModifiedDateTime = now,
            ModifiedBy = "Test"
        };
    }

    #endregion
}
