using SchedulerPlatform.UI.Models;
using SchedulerPlatform.Core.Domain.Enums;

namespace SchedulerPlatform.UI.Services;

public interface IDashboardService
{
    Task<DashboardOverview?> GetOverviewAsync(int? clientId = null, int hours = 24);
    Task<List<StatusBreakdownItem>> GetStatusBreakdownAsync(int hours = 24, int? clientId = null);
    Task<List<ExecutionTrendItem>> GetExecutionTrendsAsync(int hours = 24, int? clientId = null, List<JobStatus>? statuses = null);
    Task<List<TopLongestExecutionItem>> GetTopLongestExecutionsAsync(int limit = 10, int hours = 24, int? clientId = null, List<JobStatus>? statuses = null);
    Task<List<InvalidScheduleInfo>> GetInvalidSchedulesAsync(int? clientId = null);
}
