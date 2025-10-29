# SchedulerPlatform.ScheduleSync

Console application that synchronizes schedule data from the `ScheduleSyncSources` table to create or update `Schedule` records based on predefined rules.

## Features

- Groups records by ClientID, Vendor, and AccountNumber
- Generates CRON expressions based on ScheduleFrequency
- Updates existing schedules with matching Vendor_AccountNumber in the name
- Creates new schedules for new combinations
- Handles multiple ScheduleDate entries per combination
- Uses Central Time Zone by default
- Batched processing for performance with large datasets
- Support for millions of records

## Configuration

Settings are stored in `appsettings.json`:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=localhost;Database=SchedulerPlatform;Trusted_Connection=True;TrustServerCertificate=True;"
  },
  "SyncSettings": {
    "BatchSize": 1000,
    "DefaultTimeZone": "Central Standard Time"
  }
}
```

## Usage

```
SchedulerPlatform.ScheduleSync.exe
```

## Scheduling

This application can be scheduled to run at regular intervals using the SchedulerPlatform itself with a Process job type.

## Implementation Details

### Grouping Logic
- Records are grouped by ClientID, Vendor, AccountNumber, and ScheduleFrequency
- Each group may contain multiple ScheduleDate values
- The earliest date in each group is used as reference for CRON generation

### CRON Generation
- Uses the ScheduleFrequency enum value to generate appropriate CRON expressions:
  - Manual: Never runs automatically
  - Daily: Runs daily at 9:00 AM
  - Weekly: Runs weekly on Mondays at 9:00 AM
  - Monthly: Runs on the 1st of each month at 9:00 AM
  - Quarterly: Runs on the 1st of Jan, Apr, Jul, Oct at 9:00 AM
  - Annually: Runs once a year on a specific date at 9:00 AM

### Schedule Matching
- Existing schedules are matched by ClientID and Vendor_AccountNumber in the Schedule Name
- Names are trimmed to ensure consistent matching

### StoredProcedure Configuration
- Creates schedules with JobType=StoredProcedure
- Parameters for stored procedure execution can be added later
