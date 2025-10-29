using Heron.MudCalendar;

namespace SchedulerPlatform.UI.Models;

public class ScheduleCalendarItem : CalendarItem
{
    public int ScheduleId { get; set; }
    public int ClientId { get; set; }
    public string ClientName { get; set; } = string.Empty;
    public bool IsEnabled { get; set; }
}
