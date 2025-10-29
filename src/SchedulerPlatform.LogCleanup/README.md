# SchedulerPlatform.LogCleanup

Console application to automatically delete log files older than a specified number of days from the SchedulerPlatform.API and SchedulerPlatform.IdentityServer log directories.

## Usage

```
SchedulerPlatform.LogCleanup.exe [retentionDays]
```

Where:
- `retentionDays` (optional): Number of days to keep log files (default: 7)

## Examples

```
# Delete logs older than 7 days (default)
SchedulerPlatform.LogCleanup.exe

# Delete logs older than 14 days
SchedulerPlatform.LogCleanup.exe 14
```

## Features

- Configurable retention period
- Scans multiple log directories
- Reports total files deleted and space freed
- Continues processing if individual files fail to delete

## Log Directories

The application searches for log files in the following directories (relative to the solution root):
- SchedulerPlatform.API/logs
- SchedulerPlatform.IdentityServer/Logs

## Scheduling

This application can be scheduled to run daily using the SchedulerPlatform itself. See the script `scripts/schedule_log_cleanup.sql` for an example.
