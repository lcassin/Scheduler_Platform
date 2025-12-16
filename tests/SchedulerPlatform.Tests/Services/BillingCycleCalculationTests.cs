using FluentAssertions;
using Xunit;

namespace SchedulerPlatform.Tests.Services;

/// <summary>
/// Unit tests for billing cycle calculations, specifically the LastSuccessfulDownloadDate
/// anti-creep logic that prevents scheduling drift when vendors post invoices late.
/// 
/// The logic is:
/// - If no previous date exists, use the job's scheduled date (establish baseline)
/// - If job date is earlier or equal to expected, use job date (allow earlier)
/// - If job date is later than expected (vendor posted late), use expected date (prevent creep)
/// </summary>
public class BillingCycleCalculationTests
{
    #region CalculateLastSuccessfulDownloadDate Tests

    [Fact]
    public void CalculateLastSuccessfulDownloadDate_FirstDownload_EstablishesBaseline()
    {
        // Arrange - First successful download, no previous date
        DateTime? currentLastSuccessfulDownloadDate = null;
        var jobNextRunDateTime = new DateTime(2024, 12, 15);
        int? periodDays = 30;

        // Act
        var result = CalculateLastSuccessfulDownloadDate(
            currentLastSuccessfulDownloadDate,
            jobNextRunDateTime,
            periodDays);

        // Assert - Should use job date as baseline
        result.Should().Be(new DateTime(2024, 12, 15));
    }

    [Fact]
    public void CalculateLastSuccessfulDownloadDate_VendorPostsOnTime_UsesJobDate()
    {
        // Arrange - Previous download on Nov 23, period is 31 days, expected Dec 24
        // Vendor posts on Dec 23 (one day early)
        var currentLastSuccessfulDownloadDate = new DateTime(2024, 11, 23);
        var jobNextRunDateTime = new DateTime(2024, 12, 23);
        int? periodDays = 31;

        // Act
        var result = CalculateLastSuccessfulDownloadDate(
            currentLastSuccessfulDownloadDate,
            jobNextRunDateTime,
            periodDays);

        // Assert - Job date is earlier than expected (Dec 24), so use job date
        result.Should().Be(new DateTime(2024, 12, 23));
    }

    [Fact]
    public void CalculateLastSuccessfulDownloadDate_VendorPostsEarly_UsesJobDate()
    {
        // Arrange - Previous download on Nov 23, period is 31 days, expected Dec 24
        // Vendor posts early on Dec 20
        var currentLastSuccessfulDownloadDate = new DateTime(2024, 11, 23);
        var jobNextRunDateTime = new DateTime(2024, 12, 20);
        int? periodDays = 31;

        // Act
        var result = CalculateLastSuccessfulDownloadDate(
            currentLastSuccessfulDownloadDate,
            jobNextRunDateTime,
            periodDays);

        // Assert - Job date is earlier than expected (Dec 24), so use job date
        result.Should().Be(new DateTime(2024, 12, 20));
    }

    [Fact]
    public void CalculateLastSuccessfulDownloadDate_VendorPostsLate_UsesExpectedDateToPreventCreep()
    {
        // Arrange - Previous download on Dec 23, period is 31 days, expected Jan 23
        // Vendor posts late on Jan 27
        var currentLastSuccessfulDownloadDate = new DateTime(2024, 12, 23);
        var jobNextRunDateTime = new DateTime(2025, 1, 27);
        int? periodDays = 31;

        // Act
        var result = CalculateLastSuccessfulDownloadDate(
            currentLastSuccessfulDownloadDate,
            jobNextRunDateTime,
            periodDays);

        // Assert - Job date (Jan 27) is later than expected (Jan 23), so use expected date
        // This prevents the schedule from "creeping" later each month
        result.Should().Be(new DateTime(2025, 1, 23));
    }

    [Fact]
    public void CalculateLastSuccessfulDownloadDate_VendorPostsOnExpectedDate_UsesJobDate()
    {
        // Arrange - Previous download on Nov 23, period is 30 days, expected Dec 23
        // Vendor posts exactly on Dec 23
        var currentLastSuccessfulDownloadDate = new DateTime(2024, 11, 23);
        var jobNextRunDateTime = new DateTime(2024, 12, 23);
        int? periodDays = 30;

        // Act
        var result = CalculateLastSuccessfulDownloadDate(
            currentLastSuccessfulDownloadDate,
            jobNextRunDateTime,
            periodDays);

        // Assert - Job date equals expected date, use job date
        result.Should().Be(new DateTime(2024, 12, 23));
    }

    [Fact]
    public void CalculateLastSuccessfulDownloadDate_NullPeriodDays_DefaultsTo30Days()
    {
        // Arrange - No period specified, should default to 30 days
        var currentLastSuccessfulDownloadDate = new DateTime(2024, 11, 15);
        var jobNextRunDateTime = new DateTime(2024, 12, 20); // 5 days after expected (Dec 15)
        int? periodDays = null;

        // Act
        var result = CalculateLastSuccessfulDownloadDate(
            currentLastSuccessfulDownloadDate,
            jobNextRunDateTime,
            periodDays);

        // Assert - Expected date is Nov 15 + 30 = Dec 15
        // Job date (Dec 20) is later, so use expected date (Dec 15)
        result.Should().Be(new DateTime(2024, 12, 15));
    }

    [Fact]
    public void CalculateLastSuccessfulDownloadDate_BiWeeklyPeriod_CalculatesCorrectly()
    {
        // Arrange - Bi-weekly billing (14 days)
        var currentLastSuccessfulDownloadDate = new DateTime(2024, 12, 1);
        var jobNextRunDateTime = new DateTime(2024, 12, 15);
        int? periodDays = 14;

        // Act
        var result = CalculateLastSuccessfulDownloadDate(
            currentLastSuccessfulDownloadDate,
            jobNextRunDateTime,
            periodDays);

        // Assert - Expected date is Dec 1 + 14 = Dec 15, job date matches
        result.Should().Be(new DateTime(2024, 12, 15));
    }

    [Fact]
    public void CalculateLastSuccessfulDownloadDate_WeeklyPeriod_CalculatesCorrectly()
    {
        // Arrange - Weekly billing (7 days)
        var currentLastSuccessfulDownloadDate = new DateTime(2024, 12, 1);
        var jobNextRunDateTime = new DateTime(2024, 12, 10); // 2 days late
        int? periodDays = 7;

        // Act
        var result = CalculateLastSuccessfulDownloadDate(
            currentLastSuccessfulDownloadDate,
            jobNextRunDateTime,
            periodDays);

        // Assert - Expected date is Dec 1 + 7 = Dec 8
        // Job date (Dec 10) is later, so use expected date (Dec 8) to prevent creep
        result.Should().Be(new DateTime(2024, 12, 8));
    }

    [Fact]
    public void CalculateLastSuccessfulDownloadDate_NullJobDate_UsesCurrentDate()
    {
        // Arrange - Job date is null (edge case)
        var currentLastSuccessfulDownloadDate = new DateTime(2024, 11, 15);
        DateTime? jobNextRunDateTime = null;
        int? periodDays = 30;

        // Act
        var result = CalculateLastSuccessfulDownloadDate(
            currentLastSuccessfulDownloadDate,
            jobNextRunDateTime,
            periodDays);

        // Assert - Should use current date (today) as fallback
        // Since current date varies, we just verify it's a valid date
        result.Should().BeOnOrAfter(DateTime.UtcNow.Date.AddDays(-1));
        result.Should().BeOnOrBefore(DateTime.UtcNow.Date.AddDays(1));
    }

    [Fact]
    public void CalculateLastSuccessfulDownloadDate_MultipleConsecutiveLateVendors_PreventsAccumulatedCreep()
    {
        // Arrange - Simulate multiple months where vendor posts late
        // This test verifies that using expected date prevents accumulated drift
        
        var initialDate = new DateTime(2024, 1, 15);
        int periodDays = 30;
        
        // Month 1: Vendor posts 3 days late (Jan 18 instead of Jan 15)
        var month1Result = CalculateLastSuccessfulDownloadDate(
            null, // First download
            new DateTime(2024, 1, 18),
            periodDays);
        
        // Month 2: Expected Feb 17 (Jan 18 + 30), vendor posts Feb 20 (3 days late)
        var month2Result = CalculateLastSuccessfulDownloadDate(
            month1Result,
            new DateTime(2024, 2, 20),
            periodDays);
        
        // Month 3: Expected Mar 17 (Feb 17 + 30), vendor posts Mar 22 (5 days late)
        var month3Result = CalculateLastSuccessfulDownloadDate(
            month2Result,
            new DateTime(2024, 3, 22),
            periodDays);

        // Assert - Without anti-creep, dates would drift: Jan 18 -> Feb 20 -> Mar 22
        // With anti-creep, dates stay anchored: Jan 18 -> Feb 17 -> Mar 18
        month1Result.Should().Be(new DateTime(2024, 1, 18)); // First download establishes baseline
        month2Result.Should().Be(new DateTime(2024, 2, 17)); // Expected date used (vendor late)
        month3Result.Should().Be(new DateTime(2024, 3, 18)); // Expected date used (vendor late)
    }

    #endregion

    #region Helper Method (mirrors the private method in AdrOrchestratorService)

    /// <summary>
    /// Calculates the LastSuccessfulDownloadDate with anti-creep logic.
    /// This mirrors the private method in AdrOrchestratorService for testing purposes.
    /// </summary>
    private static DateTime CalculateLastSuccessfulDownloadDate(
        DateTime? currentLastSuccessfulDownloadDate,
        DateTime? jobNextRunDateTime,
        int? periodDays)
    {
        var jobDate = jobNextRunDateTime?.Date ?? DateTime.UtcNow.Date;
        
        // First successful download - establish baseline
        if (!currentLastSuccessfulDownloadDate.HasValue)
        {
            return jobDate;
        }
        
        // Calculate expected date based on previous anchor + period
        var previousAnchor = currentLastSuccessfulDownloadDate.Value;
        var period = periodDays ?? 30; // Default to monthly if not specified
        var expectedDate = previousAnchor.AddDays(period);
        
        // Allow earlier or same, but don't let late vendors cause creep
        if (jobDate <= expectedDate)
        {
            return jobDate; // OK to move earlier or keep same
        }
        else
        {
            return expectedDate; // Vendor late - use expected date to prevent creep
        }
    }

    #endregion
}
