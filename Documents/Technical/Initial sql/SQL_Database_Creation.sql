/****** Object:  Table [dbo].[__EFMigrationsHistory]    Script Date: 12/12/2025 2:22:55 PM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[__EFMigrationsHistory](
	[MigrationId] [nvarchar](150) NOT NULL,
	[ProductVersion] [nvarchar](32) NOT NULL,
 CONSTRAINT [PK___EFMigrationsHistory] PRIMARY KEY CLUSTERED 
(
	[MigrationId] ASC
)WITH (STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[AdrAccount]    Script Date: 12/12/2025 2:22:55 PM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[AdrAccount](
	[AdrAccountId] [int] IDENTITY(1,1) NOT NULL,
	[VMAccountId] [bigint] NOT NULL,
	[VMAccountNumber] [nvarchar](128) NULL,
	[InterfaceAccountId] [nvarchar](128) NULL,
	[ClientId] [bigint] NULL,
	[ClientName] [nvarchar](128) NULL,
	[CredentialId] [int] NOT NULL,
	[VendorCode] [nvarchar](128) NULL,
	[PeriodType] [nvarchar](13) NULL,
	[PeriodDays] [int] NULL,
	[MedianDays] [float] NULL,
	[InvoiceCount] [int] NOT NULL,
	[LastInvoiceDateTime] [datetime2](7) NULL,
	[ExpectedNextDateTime] [datetime2](7) NULL,
	[ExpectedRangeStartDateTime] [datetime2](7) NULL,
	[ExpectedRangeEndDateTime] [datetime2](7) NULL,
	[NextRunDateTime] [datetime2](7) NULL,
	[NextRangeStartDateTime] [datetime2](7) NULL,
	[NextRangeEndDateTime] [datetime2](7) NULL,
	[DaysUntilNextRun] [int] NULL,
	[NextRunStatus] [nvarchar](10) NULL,
	[HistoricalBillingStatus] [nvarchar](10) NULL,
	[LastSyncedDateTime] [datetime2](7) NULL,
	[CreatedDateTime] [datetime2](7) NOT NULL,
	[ModifiedDateTime] [datetime2](7) NOT NULL,
	[CreatedBy] [nvarchar](max) NOT NULL,
	[ModifiedBy] [nvarchar](max) NOT NULL,
	[IsDeleted] [bit] NOT NULL,
	[IsManuallyOverridden] [bit] NOT NULL,
	[OverriddenBy] [nvarchar](256) NULL,
	[OverriddenDateTime] [datetime2](7) NULL,
 CONSTRAINT [PK_AdrAccount] PRIMARY KEY CLUSTERED 
(
	[AdrAccountId] ASC
)WITH (STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
GO
/****** Object:  Table [dbo].[AdrJob]    Script Date: 12/12/2025 2:22:55 PM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[AdrJob](
	[AdrJobId] [int] IDENTITY(1,1) NOT NULL,
	[AdrAccountId] [int] NOT NULL,
	[VMAccountId] [bigint] NOT NULL,
	[VMAccountNumber] [nvarchar](30) NOT NULL,
	[VendorCode] [nvarchar](128) NULL,
	[CredentialId] [int] NOT NULL,
	[PeriodType] [nvarchar](13) NULL,
	[BillingPeriodStartDateTime] [datetime2](7) NOT NULL,
	[BillingPeriodEndDateTime] [datetime2](7) NOT NULL,
	[NextRunDateTime] [datetime2](7) NULL,
	[NextRangeStartDateTime] [datetime2](7) NULL,
	[NextRangeEndDateTime] [datetime2](7) NULL,
	[Status] [nvarchar](50) NOT NULL,
	[IsMissing] [bit] NOT NULL,
	[AdrStatusId] [int] NULL,
	[AdrStatusDescription] [nvarchar](100) NULL,
	[AdrIndexId] [bigint] NULL,
	[CredentialVerifiedDateTime] [datetime2](7) NULL,
	[ScrapingCompletedDateTime] [datetime2](7) NULL,
	[ErrorMessage] [nvarchar](max) NULL,
	[RetryCount] [int] NOT NULL,
	[CreatedDateTime] [datetime2](7) NOT NULL,
	[ModifiedDateTime] [datetime2](7) NOT NULL,
	[CreatedBy] [nvarchar](max) NOT NULL,
	[ModifiedBy] [nvarchar](max) NOT NULL,
	[IsDeleted] [bit] NOT NULL,
	[IsManualRequest] [bit] NOT NULL,
	[ManualRequestReason] [nvarchar](500) NULL,
 CONSTRAINT [PK_AdrJob] PRIMARY KEY CLUSTERED 
(
	[AdrJobId] ASC
)WITH (STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
GO
/****** Object:  Table [dbo].[AdrJobExecution]    Script Date: 12/12/2025 2:22:55 PM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[AdrJobExecution](
	[AdrJobExecutionId] [int] IDENTITY(1,1) NOT NULL,
	[AdrJobId] [int] NOT NULL,
	[AdrRequestTypeId] [int] NOT NULL,
	[StartDateTime] [datetime2](7) NOT NULL,
	[EndDateTime] [datetime2](7) NULL,
	[AdrStatusId] [int] NULL,
	[AdrStatusDescription] [nvarchar](100) NULL,
	[IsError] [bit] NOT NULL,
	[IsFinal] [bit] NOT NULL,
	[AdrIndexId] [bigint] NULL,
	[HttpStatusCode] [int] NULL,
	[IsSuccess] [bit] NOT NULL,
	[ErrorMessage] [nvarchar](max) NULL,
	[ApiResponse] [nvarchar](max) NULL,
	[RequestPayload] [nvarchar](max) NULL,
	[CreatedDateTime] [datetime2](7) NOT NULL,
	[ModifiedDateTime] [datetime2](7) NOT NULL,
	[CreatedBy] [nvarchar](max) NOT NULL,
	[ModifiedBy] [nvarchar](max) NOT NULL,
	[IsDeleted] [bit] NOT NULL,
 CONSTRAINT [PK_AdrJobExecution] PRIMARY KEY CLUSTERED 
(
	[AdrJobExecutionId] ASC
)WITH (STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
GO
/****** Object:  Table [dbo].[AdrOrchestrationRun]    Script Date: 12/12/2025 2:22:55 PM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[AdrOrchestrationRun](
	[AdrOrchestrationRunId] [int] IDENTITY(1,1) NOT NULL,
	[RequestId] [nvarchar](50) NOT NULL,
	[RequestedBy] [nvarchar](200) NOT NULL,
	[RequestedDateTime] [datetime2](7) NOT NULL,
	[StartedDateTime] [datetime2](7) NULL,
	[CompletedDateTime] [datetime2](7) NULL,
	[Status] [nvarchar](20) NOT NULL,
	[CurrentStep] [nvarchar](50) NULL,
	[CurrentProgress] [nvarchar](50) NULL,
	[TotalItems] [int] NULL,
	[ProcessedItems] [int] NULL,
	[ErrorMessage] [nvarchar](max) NULL,
	[SyncAccountsInserted] [int] NULL,
	[SyncAccountsUpdated] [int] NULL,
	[SyncAccountsTotal] [int] NULL,
	[JobsCreated] [int] NULL,
	[JobsSkipped] [int] NULL,
	[CredentialsVerified] [int] NULL,
	[CredentialsFailed] [int] NULL,
	[ScrapingRequested] [int] NULL,
	[ScrapingFailed] [int] NULL,
	[StatusesChecked] [int] NULL,
	[StatusesFailed] [int] NULL,
	[CreatedDateTime] [datetime2](7) NOT NULL,
	[CreatedBy] [nvarchar](200) NOT NULL,
	[ModifiedDateTime] [datetime2](7) NOT NULL,
	[ModifiedBy] [nvarchar](200) NOT NULL,
	[IsDeleted] [bit] NOT NULL,
 CONSTRAINT [PK_AdrOrchestrationRun] PRIMARY KEY CLUSTERED 
(
	[AdrOrchestrationRunId] ASC
)WITH (STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
GO
/****** Object:  Table [dbo].[AuditLog]    Script Date: 12/12/2025 2:22:55 PM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[AuditLog](
	[AuditLogId] [int] IDENTITY(1,1) NOT NULL,
	[EventType] [nvarchar](100) NOT NULL,
	[EntityType] [nvarchar](100) NOT NULL,
	[EntityId] [int] NULL,
	[Action] [nvarchar](50) NOT NULL,
	[OldValues] [nvarchar](max) NULL,
	[NewValues] [nvarchar](max) NULL,
	[UserName] [nvarchar](200) NOT NULL,
	[ClientId] [int] NULL,
	[IpAddress] [nvarchar](50) NULL,
	[UserAgent] [nvarchar](500) NULL,
	[TimestampDateTime] [datetime2](7) NOT NULL,
	[AdditionalData] [nvarchar](max) NULL,
	[CreatedDateTime] [datetime2](7) NOT NULL,
	[ModifiedDateTime] [datetime2](7) NOT NULL,
	[CreatedBy] [nvarchar](max) NOT NULL,
	[ModifiedBy] [nvarchar](max) NOT NULL,
	[IsDeleted] [bit] NOT NULL,
 CONSTRAINT [PK_AuditLog] PRIMARY KEY CLUSTERED 
(
	[AuditLogId] ASC
)WITH (STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
GO
/****** Object:  Table [dbo].[Client]    Script Date: 12/12/2025 2:22:55 PM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[Client](
	[ClientId] [bigint] IDENTITY(1,1) NOT NULL,
	[ExternalClientId] [int] NOT NULL,
	[ClientCode] [nvarchar](max) NOT NULL,
	[ClientName] [nvarchar](200) NOT NULL,
	[IsActive] [bit] NOT NULL,
	[ContactEmail] [nvarchar](255) NULL,
	[ContactPhone] [nvarchar](50) NULL,
	[LastSyncedDateTime] [datetime2](7) NULL,
	[CreatedDateTime] [datetime2](7) NOT NULL,
	[ModifiedDateTime] [datetime2](7) NOT NULL,
	[CreatedBy] [nvarchar](max) NOT NULL,
	[ModifiedBy] [nvarchar](max) NOT NULL,
	[IsDeleted] [bit] NOT NULL,
 CONSTRAINT [PK_Client] PRIMARY KEY CLUSTERED 
(
	[ClientId] ASC
)WITH (STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
GO
/****** Object:  Table [dbo].[JobExecution]    Script Date: 12/12/2025 2:22:55 PM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[JobExecution](
	[JobExecutionId] [int] IDENTITY(1,1) NOT NULL,
	[ScheduleId] [int] NOT NULL,
	[StartDateTime] [datetime2](7) NOT NULL,
	[EndDateTime] [datetime2](7) NULL,
	[Status] [int] NOT NULL,
	[Output] [nvarchar](max) NULL,
	[ErrorMessage] [nvarchar](max) NULL,
	[StackTrace] [nvarchar](max) NULL,
	[RetryCount] [int] NOT NULL,
	[DurationSeconds] [int] NULL,
	[TriggeredBy] [nvarchar](100) NULL,
	[CancelledBy] [nvarchar](100) NULL,
	[CreatedDateTime] [datetime2](7) NOT NULL,
	[ModifiedDateTime] [datetime2](7) NOT NULL,
	[CreatedBy] [nvarchar](max) NOT NULL,
	[ModifiedBy] [nvarchar](max) NOT NULL,
	[IsDeleted] [bit] NOT NULL,
 CONSTRAINT [PK_JobExecution] PRIMARY KEY CLUSTERED 
(
	[JobExecutionId] ASC
)WITH (STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
GO
/****** Object:  Table [dbo].[JobParameter]    Script Date: 12/12/2025 2:22:55 PM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[JobParameter](
	[JobParameterId] [int] IDENTITY(1,1) NOT NULL,
	[ScheduleId] [int] NOT NULL,
	[ParameterName] [nvarchar](100) NOT NULL,
	[ParameterType] [nvarchar](50) NOT NULL,
	[ParameterValue] [nvarchar](max) NULL,
	[SourceQuery] [nvarchar](max) NULL,
	[SourceConnectionString] [nvarchar](500) NULL,
	[IsDynamic] [bit] NOT NULL,
	[DisplayOrder] [int] NOT NULL,
	[CreatedDateTime] [datetime2](7) NOT NULL,
	[ModifiedDateTime] [datetime2](7) NOT NULL,
	[CreatedBy] [nvarchar](max) NOT NULL,
	[ModifiedBy] [nvarchar](max) NOT NULL,
	[IsDeleted] [bit] NOT NULL,
 CONSTRAINT [PK_JobParameter] PRIMARY KEY CLUSTERED 
(
	[JobParameterId] ASC
)WITH (STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
GO
/****** Object:  Table [dbo].[NotificationSetting]    Script Date: 12/12/2025 2:22:55 PM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[NotificationSetting](
	[NotificationSettingId] [int] IDENTITY(1,1) NOT NULL,
	[ScheduleId] [int] NOT NULL,
	[EnableSuccessNotifications] [bit] NOT NULL,
	[EnableFailureNotifications] [bit] NOT NULL,
	[SuccessEmailRecipients] [nvarchar](1000) NULL,
	[FailureEmailRecipients] [nvarchar](1000) NULL,
	[SuccessEmailSubject] [nvarchar](500) NULL,
	[FailureEmailSubject] [nvarchar](500) NULL,
	[IncludeExecutionDetails] [bit] NOT NULL,
	[IncludeOutput] [bit] NOT NULL,
	[CreatedDateTime] [datetime2](7) NOT NULL,
	[ModifiedDateTime] [datetime2](7) NOT NULL,
	[CreatedBy] [nvarchar](max) NOT NULL,
	[ModifiedBy] [nvarchar](max) NOT NULL,
	[IsDeleted] [bit] NOT NULL,
 CONSTRAINT [PK_NotificationSetting] PRIMARY KEY CLUSTERED 
(
	[NotificationSettingId] ASC
)WITH (STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
GO
/****** Object:  Table [dbo].[PasswordHistory]    Script Date: 12/12/2025 2:22:55 PM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[PasswordHistory](
	[PasswordHistoryId] [int] IDENTITY(1,1) NOT NULL,
	[UserId] [int] NOT NULL,
	[PasswordHash] [nvarchar](500) NOT NULL,
	[ChangedDateTime] [datetime2](7) NOT NULL,
	[CreatedDateTime] [datetime2](7) NOT NULL,
	[ModifiedDateTime] [datetime2](7) NOT NULL,
	[CreatedBy] [nvarchar](max) NOT NULL,
	[ModifiedBy] [nvarchar](max) NOT NULL,
	[IsDeleted] [bit] NOT NULL,
 CONSTRAINT [PK_PasswordHistory] PRIMARY KEY CLUSTERED 
(
	[PasswordHistoryId] ASC
)WITH (STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
GO
/****** Object:  Table [dbo].[QRTZ_BLOB_TRIGGERS]    Script Date: 12/12/2025 2:22:55 PM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[QRTZ_BLOB_TRIGGERS](
	[SCHED_NAME] [nvarchar](120) NOT NULL,
	[TRIGGER_NAME] [nvarchar](150) NOT NULL,
	[TRIGGER_GROUP] [nvarchar](150) NOT NULL,
	[BLOB_DATA] [varbinary](max) NULL,
 CONSTRAINT [PK_QRTZ_BLOB_TRIGGERS] PRIMARY KEY CLUSTERED 
(
	[SCHED_NAME] ASC,
	[TRIGGER_NAME] ASC,
	[TRIGGER_GROUP] ASC
)WITH (STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
GO
/****** Object:  Table [dbo].[QRTZ_CALENDARS]    Script Date: 12/12/2025 2:22:55 PM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[QRTZ_CALENDARS](
	[SCHED_NAME] [nvarchar](120) NOT NULL,
	[CALENDAR_NAME] [nvarchar](200) NOT NULL,
	[CALENDAR] [varbinary](max) NOT NULL,
 CONSTRAINT [PK_QRTZ_CALENDARS] PRIMARY KEY CLUSTERED 
(
	[SCHED_NAME] ASC,
	[CALENDAR_NAME] ASC
)WITH (STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
GO
/****** Object:  Table [dbo].[QRTZ_CRON_TRIGGERS]    Script Date: 12/12/2025 2:22:55 PM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[QRTZ_CRON_TRIGGERS](
	[SCHED_NAME] [nvarchar](120) NOT NULL,
	[TRIGGER_NAME] [nvarchar](150) NOT NULL,
	[TRIGGER_GROUP] [nvarchar](150) NOT NULL,
	[CRON_EXPRESSION] [nvarchar](120) NOT NULL,
	[TIME_ZONE_ID] [nvarchar](80) NULL,
 CONSTRAINT [PK_QRTZ_CRON_TRIGGERS] PRIMARY KEY CLUSTERED 
(
	[SCHED_NAME] ASC,
	[TRIGGER_NAME] ASC,
	[TRIGGER_GROUP] ASC
)WITH (STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[QRTZ_FIRED_TRIGGERS]    Script Date: 12/12/2025 2:22:55 PM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[QRTZ_FIRED_TRIGGERS](
	[SCHED_NAME] [nvarchar](120) NOT NULL,
	[ENTRY_ID] [nvarchar](140) NOT NULL,
	[TRIGGER_NAME] [nvarchar](150) NOT NULL,
	[TRIGGER_GROUP] [nvarchar](150) NOT NULL,
	[INSTANCE_NAME] [nvarchar](200) NOT NULL,
	[FIRED_TIME] [bigint] NOT NULL,
	[SCHED_TIME] [bigint] NOT NULL,
	[PRIORITY] [int] NOT NULL,
	[STATE] [nvarchar](16) NOT NULL,
	[JOB_NAME] [nvarchar](150) NULL,
	[JOB_GROUP] [nvarchar](150) NULL,
	[IS_NONCONCURRENT] [bit] NULL,
	[REQUESTS_RECOVERY] [bit] NULL,
 CONSTRAINT [PK_QRTZ_FIRED_TRIGGERS] PRIMARY KEY CLUSTERED 
(
	[SCHED_NAME] ASC,
	[ENTRY_ID] ASC
)WITH (STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[QRTZ_JOB_DETAILS]    Script Date: 12/12/2025 2:22:55 PM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[QRTZ_JOB_DETAILS](
	[SCHED_NAME] [nvarchar](120) NOT NULL,
	[JOB_NAME] [nvarchar](150) NOT NULL,
	[JOB_GROUP] [nvarchar](150) NOT NULL,
	[DESCRIPTION] [nvarchar](250) NULL,
	[JOB_CLASS_NAME] [nvarchar](250) NOT NULL,
	[IS_DURABLE] [bit] NOT NULL,
	[IS_NONCONCURRENT] [bit] NOT NULL,
	[IS_UPDATE_DATA] [bit] NOT NULL,
	[REQUESTS_RECOVERY] [bit] NOT NULL,
	[JOB_DATA] [varbinary](max) NULL,
 CONSTRAINT [PK_QRTZ_JOB_DETAILS] PRIMARY KEY CLUSTERED 
(
	[SCHED_NAME] ASC,
	[JOB_NAME] ASC,
	[JOB_GROUP] ASC
)WITH (STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
GO
/****** Object:  Table [dbo].[QRTZ_LOCKS]    Script Date: 12/12/2025 2:22:55 PM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[QRTZ_LOCKS](
	[SCHED_NAME] [nvarchar](120) NOT NULL,
	[LOCK_NAME] [nvarchar](40) NOT NULL,
 CONSTRAINT [PK_QRTZ_LOCKS] PRIMARY KEY CLUSTERED 
(
	[SCHED_NAME] ASC,
	[LOCK_NAME] ASC
)WITH (STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[QRTZ_PAUSED_TRIGGER_GRPS]    Script Date: 12/12/2025 2:22:55 PM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[QRTZ_PAUSED_TRIGGER_GRPS](
	[SCHED_NAME] [nvarchar](120) NOT NULL,
	[TRIGGER_GROUP] [nvarchar](150) NOT NULL,
 CONSTRAINT [PK_QRTZ_PAUSED_TRIGGER_GRPS] PRIMARY KEY CLUSTERED 
(
	[SCHED_NAME] ASC,
	[TRIGGER_GROUP] ASC
)WITH (STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[QRTZ_SCHEDULER_STATE]    Script Date: 12/12/2025 2:22:55 PM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[QRTZ_SCHEDULER_STATE](
	[SCHED_NAME] [nvarchar](120) NOT NULL,
	[INSTANCE_NAME] [nvarchar](200) NOT NULL,
	[LAST_CHECKIN_TIME] [bigint] NOT NULL,
	[CHECKIN_INTERVAL] [bigint] NOT NULL,
 CONSTRAINT [PK_QRTZ_SCHEDULER_STATE] PRIMARY KEY CLUSTERED 
(
	[SCHED_NAME] ASC,
	[INSTANCE_NAME] ASC
)WITH (STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[QRTZ_SIMPLE_TRIGGERS]    Script Date: 12/12/2025 2:22:55 PM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[QRTZ_SIMPLE_TRIGGERS](
	[SCHED_NAME] [nvarchar](120) NOT NULL,
	[TRIGGER_NAME] [nvarchar](150) NOT NULL,
	[TRIGGER_GROUP] [nvarchar](150) NOT NULL,
	[REPEAT_COUNT] [int] NOT NULL,
	[REPEAT_INTERVAL] [bigint] NOT NULL,
	[TIMES_TRIGGERED] [int] NOT NULL,
 CONSTRAINT [PK_QRTZ_SIMPLE_TRIGGERS] PRIMARY KEY CLUSTERED 
(
	[SCHED_NAME] ASC,
	[TRIGGER_NAME] ASC,
	[TRIGGER_GROUP] ASC
)WITH (STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[QRTZ_SIMPROP_TRIGGERS]    Script Date: 12/12/2025 2:22:55 PM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[QRTZ_SIMPROP_TRIGGERS](
	[SCHED_NAME] [nvarchar](120) NOT NULL,
	[TRIGGER_NAME] [nvarchar](150) NOT NULL,
	[TRIGGER_GROUP] [nvarchar](150) NOT NULL,
	[STR_PROP_1] [nvarchar](512) NULL,
	[STR_PROP_2] [nvarchar](512) NULL,
	[STR_PROP_3] [nvarchar](512) NULL,
	[INT_PROP_1] [int] NULL,
	[INT_PROP_2] [int] NULL,
	[LONG_PROP_1] [bigint] NULL,
	[LONG_PROP_2] [bigint] NULL,
	[DEC_PROP_1] [numeric](13, 4) NULL,
	[DEC_PROP_2] [numeric](13, 4) NULL,
	[BOOL_PROP_1] [bit] NULL,
	[BOOL_PROP_2] [bit] NULL,
	[TIME_ZONE_ID] [nvarchar](80) NULL,
 CONSTRAINT [PK_QRTZ_SIMPROP_TRIGGERS] PRIMARY KEY CLUSTERED 
(
	[SCHED_NAME] ASC,
	[TRIGGER_NAME] ASC,
	[TRIGGER_GROUP] ASC
)WITH (STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[QRTZ_TRIGGERS]    Script Date: 12/12/2025 2:22:55 PM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[QRTZ_TRIGGERS](
	[SCHED_NAME] [nvarchar](120) NOT NULL,
	[TRIGGER_NAME] [nvarchar](150) NOT NULL,
	[TRIGGER_GROUP] [nvarchar](150) NOT NULL,
	[JOB_NAME] [nvarchar](150) NOT NULL,
	[JOB_GROUP] [nvarchar](150) NOT NULL,
	[DESCRIPTION] [nvarchar](250) NULL,
	[NEXT_FIRE_TIME] [bigint] NULL,
	[PREV_FIRE_TIME] [bigint] NULL,
	[PRIORITY] [int] NULL,
	[TRIGGER_STATE] [nvarchar](16) NOT NULL,
	[TRIGGER_TYPE] [nvarchar](8) NOT NULL,
	[START_TIME] [bigint] NOT NULL,
	[END_TIME] [bigint] NULL,
	[CALENDAR_NAME] [nvarchar](200) NULL,
	[MISFIRE_INSTR] [int] NULL,
	[JOB_DATA] [varbinary](max) NULL,
 CONSTRAINT [PK_QRTZ_TRIGGERS] PRIMARY KEY CLUSTERED 
(
	[SCHED_NAME] ASC,
	[TRIGGER_NAME] ASC,
	[TRIGGER_GROUP] ASC
)WITH (STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
GO
/****** Object:  Table [dbo].[Schedule]    Script Date: 12/12/2025 2:22:55 PM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[Schedule](
	[ScheduleId] [int] IDENTITY(1,1) NOT NULL,
	[Name] [nvarchar](200) NOT NULL,
	[Description] [nvarchar](1000) NOT NULL,
	[ClientId] [bigint] NOT NULL,
	[JobType] [int] NOT NULL,
	[Frequency] [int] NOT NULL,
	[CronExpression] [nvarchar](100) NOT NULL,
	[NextRunDateTime] [datetime2](7) NULL,
	[LastRunDateTime] [datetime2](7) NULL,
	[IsEnabled] [bit] NOT NULL,
	[MaxRetries] [int] NOT NULL,
	[RetryDelayMinutes] [int] NOT NULL,
	[TimeoutMinutes] [int] NULL,
	[TimeZone] [nvarchar](100) NULL,
	[JobConfiguration] [nvarchar](max) NULL,
	[CreatedDateTime] [datetime2](7) NOT NULL,
	[ModifiedDateTime] [datetime2](7) NOT NULL,
	[CreatedBy] [nvarchar](max) NOT NULL,
	[ModifiedBy] [nvarchar](max) NOT NULL,
	[IsDeleted] [bit] NOT NULL,
	[IsSystemSchedule] [bit] NOT NULL,
 CONSTRAINT [PK_Schedule] PRIMARY KEY CLUSTERED 
(
	[ScheduleId] ASC
)WITH (STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
GO
/****** Object:  Table [dbo].[ScheduleSyncSource]    Script Date: 12/12/2025 2:22:55 PM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[ScheduleSyncSource](
	[ScheduleSyncSourceId] [int] IDENTITY(1,1) NOT NULL,
	[ExternalAccountId] [bigint] NOT NULL,
	[AccountNumber] [nvarchar](128) NOT NULL,
	[ExternalVendorId] [bigint] NOT NULL,
	[ExternalClientId] [int] NOT NULL,
	[ClientId] [bigint] NULL,
	[CredentialId] [int] NOT NULL,
	[ScheduleFrequency] [int] NOT NULL,
	[LastInvoiceDateTime] [datetime2](7) NOT NULL,
	[AccountName] [nvarchar](64) NULL,
	[VendorName] [nvarchar](64) NULL,
	[ClientName] [nvarchar](64) NULL,
	[TandemAccountId] [nvarchar](64) NULL,
	[LastSyncedDateTime] [datetime2](7) NULL,
	[CreatedDateTime] [datetime2](7) NOT NULL,
	[ModifiedDateTime] [datetime2](7) NOT NULL,
	[CreatedBy] [nvarchar](max) NOT NULL,
	[ModifiedBy] [nvarchar](max) NOT NULL,
	[IsDeleted] [bit] NOT NULL,
 CONSTRAINT [PK_ScheduleSyncSource] PRIMARY KEY CLUSTERED 
(
	[ScheduleSyncSourceId] ASC
)WITH (STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
GO
/****** Object:  Table [dbo].[User]    Script Date: 12/12/2025 2:22:55 PM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[User](
	[UserId] [int] IDENTITY(1,1) NOT NULL,
	[Username] [nvarchar](100) NOT NULL,
	[Email] [nvarchar](255) NOT NULL,
	[FirstName] [nvarchar](100) NOT NULL,
	[LastName] [nvarchar](100) NOT NULL,
	[ClientId] [bigint] NOT NULL,
	[IsActive] [bit] NOT NULL,
	[ExternalUserId] [nvarchar](255) NULL,
	[ExternalIssuer] [nvarchar](500) NULL,
	[PasswordHash] [nvarchar](500) NULL,
	[IsSystemAdmin] [bit] NOT NULL,
	[LastLoginDateTime] [datetime2](7) NULL,
	[MustChangePassword] [bit] NOT NULL,
	[PasswordChangedDateTime] [datetime2](7) NULL,
	[CreatedDateTime] [datetime2](7) NOT NULL,
	[ModifiedDateTime] [datetime2](7) NOT NULL,
	[CreatedBy] [nvarchar](max) NOT NULL,
	[ModifiedBy] [nvarchar](max) NOT NULL,
	[IsDeleted] [bit] NOT NULL,
 CONSTRAINT [PK_User] PRIMARY KEY CLUSTERED 
(
	[UserId] ASC
)WITH (STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
GO
/****** Object:  Table [dbo].[UserPermission]    Script Date: 12/12/2025 2:22:55 PM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[UserPermission](
	[UserPermissionId] [int] IDENTITY(1,1) NOT NULL,
	[UserId] [int] NOT NULL,
	[PermissionName] [nvarchar](100) NOT NULL,
	[ResourceType] [nvarchar](50) NULL,
	[ResourceId] [int] NULL,
	[CanCreate] [bit] NOT NULL,
	[CanRead] [bit] NOT NULL,
	[CanUpdate] [bit] NOT NULL,
	[CanDelete] [bit] NOT NULL,
	[CanExecute] [bit] NOT NULL,
	[CreatedDateTime] [datetime2](7) NOT NULL,
	[ModifiedDateTime] [datetime2](7) NOT NULL,
	[CreatedBy] [nvarchar](max) NOT NULL,
	[ModifiedBy] [nvarchar](max) NOT NULL,
	[IsDeleted] [bit] NOT NULL,
 CONSTRAINT [PK_UserPermission] PRIMARY KEY CLUSTERED 
(
	[UserPermissionId] ASC
)WITH (STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
GO
/****** Object:  Index [IX_AdrAccount_ClientId]    Script Date: 12/12/2025 2:22:55 PM ******/
CREATE NONCLUSTERED INDEX [IX_AdrAccount_ClientId] ON [dbo].[AdrAccount]
(
	[ClientId] ASC
)WITH (STATISTICS_NORECOMPUTE = OFF, DROP_EXISTING = OFF, ONLINE = OFF, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
/****** Object:  Index [IX_AdrAccount_CredentialId]    Script Date: 12/12/2025 2:22:55 PM ******/
CREATE NONCLUSTERED INDEX [IX_AdrAccount_CredentialId] ON [dbo].[AdrAccount]
(
	[CredentialId] ASC
)WITH (STATISTICS_NORECOMPUTE = OFF, DROP_EXISTING = OFF, ONLINE = OFF, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
SET ANSI_PADDING ON
GO
/****** Object:  Index [IX_AdrAccount_HistoricalBillingStatus]    Script Date: 12/12/2025 2:22:55 PM ******/
CREATE NONCLUSTERED INDEX [IX_AdrAccount_HistoricalBillingStatus] ON [dbo].[AdrAccount]
(
	[HistoricalBillingStatus] ASC
)WITH (STATISTICS_NORECOMPUTE = OFF, DROP_EXISTING = OFF, ONLINE = OFF, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
SET ANSI_PADDING ON
GO
/****** Object:  Index [IX_AdrAccount_InterfaceAccountId]    Script Date: 12/12/2025 2:22:55 PM ******/
CREATE NONCLUSTERED INDEX [IX_AdrAccount_InterfaceAccountId] ON [dbo].[AdrAccount]
(
	[InterfaceAccountId] ASC
)WITH (STATISTICS_NORECOMPUTE = OFF, DROP_EXISTING = OFF, ONLINE = OFF, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
SET ANSI_PADDING ON
GO
/****** Object:  Index [IX_AdrAccount_IsDeleted_ClientId_NextRunStatus]    Script Date: 12/12/2025 2:22:55 PM ******/
CREATE NONCLUSTERED INDEX [IX_AdrAccount_IsDeleted_ClientId_NextRunStatus] ON [dbo].[AdrAccount]
(
	[IsDeleted] ASC,
	[ClientId] ASC,
	[NextRunStatus] ASC
)WITH (STATISTICS_NORECOMPUTE = OFF, DROP_EXISTING = OFF, ONLINE = OFF, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
SET ANSI_PADDING ON
GO
/****** Object:  Index [IX_AdrAccount_IsDeleted_HistoricalBillingStatus]    Script Date: 12/12/2025 2:22:55 PM ******/
CREATE NONCLUSTERED INDEX [IX_AdrAccount_IsDeleted_HistoricalBillingStatus] ON [dbo].[AdrAccount]
(
	[IsDeleted] ASC,
	[HistoricalBillingStatus] ASC
)WITH (STATISTICS_NORECOMPUTE = OFF, DROP_EXISTING = OFF, ONLINE = OFF, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
SET ANSI_PADDING ON
GO
/****** Object:  Index [IX_AdrAccount_IsDeleted_NextRunStatus_NextRunDateTime]    Script Date: 12/12/2025 2:22:55 PM ******/
CREATE NONCLUSTERED INDEX [IX_AdrAccount_IsDeleted_NextRunStatus_NextRunDateTime] ON [dbo].[AdrAccount]
(
	[IsDeleted] ASC,
	[NextRunStatus] ASC,
	[NextRunDateTime] ASC
)WITH (STATISTICS_NORECOMPUTE = OFF, DROP_EXISTING = OFF, ONLINE = OFF, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
/****** Object:  Index [IX_AdrAccount_NextRunDateTime]    Script Date: 12/12/2025 2:22:55 PM ******/
CREATE NONCLUSTERED INDEX [IX_AdrAccount_NextRunDateTime] ON [dbo].[AdrAccount]
(
	[NextRunDateTime] ASC
)WITH (STATISTICS_NORECOMPUTE = OFF, DROP_EXISTING = OFF, ONLINE = OFF, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
SET ANSI_PADDING ON
GO
/****** Object:  Index [IX_AdrAccount_NextRunStatus]    Script Date: 12/12/2025 2:22:55 PM ******/
CREATE NONCLUSTERED INDEX [IX_AdrAccount_NextRunStatus] ON [dbo].[AdrAccount]
(
	[NextRunStatus] ASC
)WITH (STATISTICS_NORECOMPUTE = OFF, DROP_EXISTING = OFF, ONLINE = OFF, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
SET ANSI_PADDING ON
GO
/****** Object:  Index [IX_AdrAccount_VendorCode]    Script Date: 12/12/2025 2:22:55 PM ******/
CREATE NONCLUSTERED INDEX [IX_AdrAccount_VendorCode] ON [dbo].[AdrAccount]
(
	[VendorCode] ASC
)WITH (STATISTICS_NORECOMPUTE = OFF, DROP_EXISTING = OFF, ONLINE = OFF, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
/****** Object:  Index [IX_AdrAccount_VMAccountId]    Script Date: 12/12/2025 2:22:55 PM ******/
CREATE NONCLUSTERED INDEX [IX_AdrAccount_VMAccountId] ON [dbo].[AdrAccount]
(
	[VMAccountId] ASC
)WITH (STATISTICS_NORECOMPUTE = OFF, DROP_EXISTING = OFF, ONLINE = OFF, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
SET ANSI_PADDING ON
GO
/****** Object:  Index [IX_AdrAccount_VMAccountId_VMAccountNumber]    Script Date: 12/12/2025 2:22:55 PM ******/
CREATE NONCLUSTERED INDEX [IX_AdrAccount_VMAccountId_VMAccountNumber] ON [dbo].[AdrAccount]
(
	[VMAccountId] ASC,
	[VMAccountNumber] ASC
)WITH (STATISTICS_NORECOMPUTE = OFF, DROP_EXISTING = OFF, ONLINE = OFF, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
SET ANSI_PADDING ON
GO
/****** Object:  Index [IX_AdrAccount_VMAccountNumber]    Script Date: 12/12/2025 2:22:55 PM ******/
CREATE NONCLUSTERED INDEX [IX_AdrAccount_VMAccountNumber] ON [dbo].[AdrAccount]
(
	[VMAccountNumber] ASC
)WITH (STATISTICS_NORECOMPUTE = OFF, DROP_EXISTING = OFF, ONLINE = OFF, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
/****** Object:  Index [IX_AdrJob_AdrAccountId]    Script Date: 12/12/2025 2:22:55 PM ******/
CREATE NONCLUSTERED INDEX [IX_AdrJob_AdrAccountId] ON [dbo].[AdrJob]
(
	[AdrAccountId] ASC
)WITH (STATISTICS_NORECOMPUTE = OFF, DROP_EXISTING = OFF, ONLINE = OFF, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
/****** Object:  Index [IX_AdrJob_AdrAccountId_BillingPeriodStartDateTime_BillingPeriodEndDateTime]    Script Date: 12/12/2025 2:22:55 PM ******/
CREATE NONCLUSTERED INDEX [IX_AdrJob_AdrAccountId_BillingPeriodStartDateTime_BillingPeriodEndDateTime] ON [dbo].[AdrJob]
(
	[AdrAccountId] ASC,
	[BillingPeriodStartDateTime] ASC,
	[BillingPeriodEndDateTime] ASC
)WITH (STATISTICS_NORECOMPUTE = OFF, DROP_EXISTING = OFF, ONLINE = OFF, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
/****** Object:  Index [IX_AdrJob_BillingPeriodStartDateTime]    Script Date: 12/12/2025 2:22:55 PM ******/
CREATE NONCLUSTERED INDEX [IX_AdrJob_BillingPeriodStartDateTime] ON [dbo].[AdrJob]
(
	[BillingPeriodStartDateTime] ASC
)WITH (STATISTICS_NORECOMPUTE = OFF, DROP_EXISTING = OFF, ONLINE = OFF, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
/****** Object:  Index [IX_AdrJob_CredentialId]    Script Date: 12/12/2025 2:22:55 PM ******/
CREATE NONCLUSTERED INDEX [IX_AdrJob_CredentialId] ON [dbo].[AdrJob]
(
	[CredentialId] ASC
)WITH (STATISTICS_NORECOMPUTE = OFF, DROP_EXISTING = OFF, ONLINE = OFF, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
/****** Object:  Index [IX_AdrJob_IsDeleted_AdrAccountId_BillingPeriodStartDateTime]    Script Date: 12/12/2025 2:22:55 PM ******/
CREATE NONCLUSTERED INDEX [IX_AdrJob_IsDeleted_AdrAccountId_BillingPeriodStartDateTime] ON [dbo].[AdrJob]
(
	[IsDeleted] ASC,
	[AdrAccountId] ASC,
	[BillingPeriodStartDateTime] ASC
)WITH (STATISTICS_NORECOMPUTE = OFF, DROP_EXISTING = OFF, ONLINE = OFF, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
SET ANSI_PADDING ON
GO
/****** Object:  Index [IX_AdrJob_IsDeleted_Status]    Script Date: 12/12/2025 2:22:55 PM ******/
CREATE NONCLUSTERED INDEX [IX_AdrJob_IsDeleted_Status] ON [dbo].[AdrJob]
(
	[IsDeleted] ASC,
	[Status] ASC
)WITH (STATISTICS_NORECOMPUTE = OFF, DROP_EXISTING = OFF, ONLINE = OFF, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
SET ANSI_PADDING ON
GO
/****** Object:  Index [IX_AdrJob_IsDeleted_Status_BillingPeriodStartDateTime]    Script Date: 12/12/2025 2:22:55 PM ******/
CREATE NONCLUSTERED INDEX [IX_AdrJob_IsDeleted_Status_BillingPeriodStartDateTime] ON [dbo].[AdrJob]
(
	[IsDeleted] ASC,
	[Status] ASC,
	[BillingPeriodStartDateTime] ASC
)WITH (STATISTICS_NORECOMPUTE = OFF, DROP_EXISTING = OFF, ONLINE = OFF, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
/****** Object:  Index [IX_AdrJob_IsManualRequest]    Script Date: 12/12/2025 2:22:55 PM ******/
CREATE NONCLUSTERED INDEX [IX_AdrJob_IsManualRequest] ON [dbo].[AdrJob]
(
	[IsManualRequest] ASC
)
WHERE ([IsDeleted]=(0))
WITH (STATISTICS_NORECOMPUTE = OFF, DROP_EXISTING = OFF, ONLINE = OFF, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
/****** Object:  Index [IX_AdrJob_ModifiedDateTime]    Script Date: 12/12/2025 2:22:55 PM ******/
CREATE NONCLUSTERED INDEX [IX_AdrJob_ModifiedDateTime] ON [dbo].[AdrJob]
(
	[ModifiedDateTime] ASC
)WITH (STATISTICS_NORECOMPUTE = OFF, DROP_EXISTING = OFF, ONLINE = OFF, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
SET ANSI_PADDING ON
GO
/****** Object:  Index [IX_AdrJob_Status]    Script Date: 12/12/2025 2:22:55 PM ******/
CREATE NONCLUSTERED INDEX [IX_AdrJob_Status] ON [dbo].[AdrJob]
(
	[Status] ASC
)WITH (STATISTICS_NORECOMPUTE = OFF, DROP_EXISTING = OFF, ONLINE = OFF, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
SET ANSI_PADDING ON
GO
/****** Object:  Index [IX_AdrJob_VendorCode]    Script Date: 12/12/2025 2:22:55 PM ******/
CREATE NONCLUSTERED INDEX [IX_AdrJob_VendorCode] ON [dbo].[AdrJob]
(
	[VendorCode] ASC
)WITH (STATISTICS_NORECOMPUTE = OFF, DROP_EXISTING = OFF, ONLINE = OFF, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
/****** Object:  Index [IX_AdrJob_VMAccountId]    Script Date: 12/12/2025 2:22:55 PM ******/
CREATE NONCLUSTERED INDEX [IX_AdrJob_VMAccountId] ON [dbo].[AdrJob]
(
	[VMAccountId] ASC
)WITH (STATISTICS_NORECOMPUTE = OFF, DROP_EXISTING = OFF, ONLINE = OFF, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
SET ANSI_PADDING ON
GO
/****** Object:  Index [IX_AdrJob_VMAccountNumber]    Script Date: 12/12/2025 2:22:55 PM ******/
CREATE NONCLUSTERED INDEX [IX_AdrJob_VMAccountNumber] ON [dbo].[AdrJob]
(
	[VMAccountNumber] ASC
)WITH (STATISTICS_NORECOMPUTE = OFF, DROP_EXISTING = OFF, ONLINE = OFF, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
/****** Object:  Index [UX_AdrJob_Account_BillingPeriod]    Script Date: 12/12/2025 2:22:55 PM ******/
CREATE UNIQUE NONCLUSTERED INDEX [UX_AdrJob_Account_BillingPeriod] ON [dbo].[AdrJob]
(
	[AdrAccountId] ASC,
	[BillingPeriodStartDateTime] ASC,
	[BillingPeriodEndDateTime] ASC
)
WHERE ([IsDeleted]=(0))
WITH (STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, DROP_EXISTING = OFF, ONLINE = OFF, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
/****** Object:  Index [IX_AdrJobExecution_AdrJobId]    Script Date: 12/12/2025 2:22:55 PM ******/
CREATE NONCLUSTERED INDEX [IX_AdrJobExecution_AdrJobId] ON [dbo].[AdrJobExecution]
(
	[AdrJobId] ASC
)WITH (STATISTICS_NORECOMPUTE = OFF, DROP_EXISTING = OFF, ONLINE = OFF, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
/****** Object:  Index [IX_AdrJobExecution_AdrRequestTypeId]    Script Date: 12/12/2025 2:22:55 PM ******/
CREATE NONCLUSTERED INDEX [IX_AdrJobExecution_AdrRequestTypeId] ON [dbo].[AdrJobExecution]
(
	[AdrRequestTypeId] ASC
)WITH (STATISTICS_NORECOMPUTE = OFF, DROP_EXISTING = OFF, ONLINE = OFF, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
/****** Object:  Index [IX_AdrJobExecution_IsSuccess]    Script Date: 12/12/2025 2:22:55 PM ******/
CREATE NONCLUSTERED INDEX [IX_AdrJobExecution_IsSuccess] ON [dbo].[AdrJobExecution]
(
	[IsSuccess] ASC
)WITH (STATISTICS_NORECOMPUTE = OFF, DROP_EXISTING = OFF, ONLINE = OFF, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
/****** Object:  Index [IX_AdrJobExecution_StartDateTime]    Script Date: 12/12/2025 2:22:55 PM ******/
CREATE NONCLUSTERED INDEX [IX_AdrJobExecution_StartDateTime] ON [dbo].[AdrJobExecution]
(
	[StartDateTime] ASC
)WITH (STATISTICS_NORECOMPUTE = OFF, DROP_EXISTING = OFF, ONLINE = OFF, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
/****** Object:  Index [IX_AdrOrchestrationRun_RequestedDateTime]    Script Date: 12/12/2025 2:22:55 PM ******/
CREATE NONCLUSTERED INDEX [IX_AdrOrchestrationRun_RequestedDateTime] ON [dbo].[AdrOrchestrationRun]
(
	[RequestedDateTime] DESC
)WITH (STATISTICS_NORECOMPUTE = OFF, DROP_EXISTING = OFF, ONLINE = OFF, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
SET ANSI_PADDING ON
GO
/****** Object:  Index [IX_AdrOrchestrationRun_RequestId]    Script Date: 12/12/2025 2:22:55 PM ******/
CREATE UNIQUE NONCLUSTERED INDEX [IX_AdrOrchestrationRun_RequestId] ON [dbo].[AdrOrchestrationRun]
(
	[RequestId] ASC
)WITH (STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, DROP_EXISTING = OFF, ONLINE = OFF, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
SET ANSI_PADDING ON
GO
/****** Object:  Index [IX_AdrOrchestrationRun_Status]    Script Date: 12/12/2025 2:22:55 PM ******/
CREATE NONCLUSTERED INDEX [IX_AdrOrchestrationRun_Status] ON [dbo].[AdrOrchestrationRun]
(
	[Status] ASC
)WITH (STATISTICS_NORECOMPUTE = OFF, DROP_EXISTING = OFF, ONLINE = OFF, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
SET ANSI_PADDING ON
GO
/****** Object:  Index [IX_AdrOrchestrationRun_Status_RequestedDateTime]    Script Date: 12/12/2025 2:22:55 PM ******/
CREATE NONCLUSTERED INDEX [IX_AdrOrchestrationRun_Status_RequestedDateTime] ON [dbo].[AdrOrchestrationRun]
(
	[Status] ASC,
	[RequestedDateTime] DESC
)WITH (STATISTICS_NORECOMPUTE = OFF, DROP_EXISTING = OFF, ONLINE = OFF, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
SET ANSI_PADDING ON
GO
/****** Object:  Index [IX_AuditLog_EntityType_EntityId]    Script Date: 12/12/2025 2:22:55 PM ******/
CREATE NONCLUSTERED INDEX [IX_AuditLog_EntityType_EntityId] ON [dbo].[AuditLog]
(
	[EntityType] ASC,
	[EntityId] ASC
)WITH (STATISTICS_NORECOMPUTE = OFF, DROP_EXISTING = OFF, ONLINE = OFF, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
/****** Object:  Index [IX_AuditLog_TimestampDateTime]    Script Date: 12/12/2025 2:22:55 PM ******/
CREATE NONCLUSTERED INDEX [IX_AuditLog_TimestampDateTime] ON [dbo].[AuditLog]
(
	[TimestampDateTime] ASC
)WITH (STATISTICS_NORECOMPUTE = OFF, DROP_EXISTING = OFF, ONLINE = OFF, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
/****** Object:  Index [IX_Client_ExternalClientId]    Script Date: 12/12/2025 2:22:55 PM ******/
CREATE UNIQUE NONCLUSTERED INDEX [IX_Client_ExternalClientId] ON [dbo].[Client]
(
	[ExternalClientId] ASC
)WITH (STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, DROP_EXISTING = OFF, ONLINE = OFF, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
/****** Object:  Index [IX_Client_LastSyncedDateTime]    Script Date: 12/12/2025 2:22:55 PM ******/
CREATE NONCLUSTERED INDEX [IX_Client_LastSyncedDateTime] ON [dbo].[Client]
(
	[LastSyncedDateTime] ASC
)WITH (STATISTICS_NORECOMPUTE = OFF, DROP_EXISTING = OFF, ONLINE = OFF, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
/****** Object:  Index [IX_JobExecution_ScheduleId]    Script Date: 12/12/2025 2:22:55 PM ******/
CREATE NONCLUSTERED INDEX [IX_JobExecution_ScheduleId] ON [dbo].[JobExecution]
(
	[ScheduleId] ASC
)WITH (STATISTICS_NORECOMPUTE = OFF, DROP_EXISTING = OFF, ONLINE = OFF, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
/****** Object:  Index [IX_JobExecution_StartDateTime]    Script Date: 12/12/2025 2:22:55 PM ******/
CREATE NONCLUSTERED INDEX [IX_JobExecution_StartDateTime] ON [dbo].[JobExecution]
(
	[StartDateTime] ASC
)WITH (STATISTICS_NORECOMPUTE = OFF, DROP_EXISTING = OFF, ONLINE = OFF, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
/****** Object:  Index [IX_JobExecution_Status]    Script Date: 12/12/2025 2:22:55 PM ******/
CREATE NONCLUSTERED INDEX [IX_JobExecution_Status] ON [dbo].[JobExecution]
(
	[Status] ASC
)WITH (STATISTICS_NORECOMPUTE = OFF, DROP_EXISTING = OFF, ONLINE = OFF, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
/****** Object:  Index [IX_JobParameter_ScheduleId]    Script Date: 12/12/2025 2:22:55 PM ******/
CREATE NONCLUSTERED INDEX [IX_JobParameter_ScheduleId] ON [dbo].[JobParameter]
(
	[ScheduleId] ASC
)WITH (STATISTICS_NORECOMPUTE = OFF, DROP_EXISTING = OFF, ONLINE = OFF, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
/****** Object:  Index [IX_NotificationSetting_ScheduleId]    Script Date: 12/12/2025 2:22:55 PM ******/
CREATE UNIQUE NONCLUSTERED INDEX [IX_NotificationSetting_ScheduleId] ON [dbo].[NotificationSetting]
(
	[ScheduleId] ASC
)WITH (STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, DROP_EXISTING = OFF, ONLINE = OFF, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
/****** Object:  Index [IX_PasswordHistory_UserId_ChangedDateTime]    Script Date: 12/12/2025 2:22:55 PM ******/
CREATE NONCLUSTERED INDEX [IX_PasswordHistory_UserId_ChangedDateTime] ON [dbo].[PasswordHistory]
(
	[UserId] ASC,
	[ChangedDateTime] ASC
)WITH (STATISTICS_NORECOMPUTE = OFF, DROP_EXISTING = OFF, ONLINE = OFF, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
SET ANSI_PADDING ON
GO
/****** Object:  Index [IDX_QRTZ_FT_INST_JOB_REQ_RCVRY]    Script Date: 12/12/2025 2:22:55 PM ******/
CREATE NONCLUSTERED INDEX [IDX_QRTZ_FT_INST_JOB_REQ_RCVRY] ON [dbo].[QRTZ_FIRED_TRIGGERS]
(
	[SCHED_NAME] ASC,
	[INSTANCE_NAME] ASC,
	[REQUESTS_RECOVERY] ASC
)WITH (STATISTICS_NORECOMPUTE = OFF, DROP_EXISTING = OFF, ONLINE = OFF, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
SET ANSI_PADDING ON
GO
/****** Object:  Index [IDX_QRTZ_FT_J_G]    Script Date: 12/12/2025 2:22:55 PM ******/
CREATE NONCLUSTERED INDEX [IDX_QRTZ_FT_J_G] ON [dbo].[QRTZ_FIRED_TRIGGERS]
(
	[SCHED_NAME] ASC,
	[JOB_NAME] ASC,
	[JOB_GROUP] ASC
)WITH (STATISTICS_NORECOMPUTE = OFF, DROP_EXISTING = OFF, ONLINE = OFF, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
SET ANSI_PADDING ON
GO
/****** Object:  Index [IDX_QRTZ_FT_JG]    Script Date: 12/12/2025 2:22:55 PM ******/
CREATE NONCLUSTERED INDEX [IDX_QRTZ_FT_JG] ON [dbo].[QRTZ_FIRED_TRIGGERS]
(
	[SCHED_NAME] ASC,
	[JOB_GROUP] ASC
)WITH (STATISTICS_NORECOMPUTE = OFF, DROP_EXISTING = OFF, ONLINE = OFF, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
SET ANSI_PADDING ON
GO
/****** Object:  Index [IDX_QRTZ_FT_T_G]    Script Date: 12/12/2025 2:22:55 PM ******/
CREATE NONCLUSTERED INDEX [IDX_QRTZ_FT_T_G] ON [dbo].[QRTZ_FIRED_TRIGGERS]
(
	[SCHED_NAME] ASC,
	[TRIGGER_NAME] ASC,
	[TRIGGER_GROUP] ASC
)WITH (STATISTICS_NORECOMPUTE = OFF, DROP_EXISTING = OFF, ONLINE = OFF, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
SET ANSI_PADDING ON
GO
/****** Object:  Index [IDX_QRTZ_FT_TG]    Script Date: 12/12/2025 2:22:55 PM ******/
CREATE NONCLUSTERED INDEX [IDX_QRTZ_FT_TG] ON [dbo].[QRTZ_FIRED_TRIGGERS]
(
	[SCHED_NAME] ASC,
	[TRIGGER_GROUP] ASC
)WITH (STATISTICS_NORECOMPUTE = OFF, DROP_EXISTING = OFF, ONLINE = OFF, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
SET ANSI_PADDING ON
GO
/****** Object:  Index [IDX_QRTZ_FT_TRIG_INST_NAME]    Script Date: 12/12/2025 2:22:55 PM ******/
CREATE NONCLUSTERED INDEX [IDX_QRTZ_FT_TRIG_INST_NAME] ON [dbo].[QRTZ_FIRED_TRIGGERS]
(
	[SCHED_NAME] ASC,
	[INSTANCE_NAME] ASC
)WITH (STATISTICS_NORECOMPUTE = OFF, DROP_EXISTING = OFF, ONLINE = OFF, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
SET ANSI_PADDING ON
GO
/****** Object:  Index [IDX_QRTZ_T_C]    Script Date: 12/12/2025 2:22:55 PM ******/
CREATE NONCLUSTERED INDEX [IDX_QRTZ_T_C] ON [dbo].[QRTZ_TRIGGERS]
(
	[SCHED_NAME] ASC,
	[CALENDAR_NAME] ASC
)WITH (STATISTICS_NORECOMPUTE = OFF, DROP_EXISTING = OFF, ONLINE = OFF, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
SET ANSI_PADDING ON
GO
/****** Object:  Index [IDX_QRTZ_T_G]    Script Date: 12/12/2025 2:22:55 PM ******/
CREATE NONCLUSTERED INDEX [IDX_QRTZ_T_G] ON [dbo].[QRTZ_TRIGGERS]
(
	[SCHED_NAME] ASC,
	[TRIGGER_GROUP] ASC
)WITH (STATISTICS_NORECOMPUTE = OFF, DROP_EXISTING = OFF, ONLINE = OFF, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
SET ANSI_PADDING ON
GO
/****** Object:  Index [IDX_QRTZ_T_J]    Script Date: 12/12/2025 2:22:55 PM ******/
CREATE NONCLUSTERED INDEX [IDX_QRTZ_T_J] ON [dbo].[QRTZ_TRIGGERS]
(
	[SCHED_NAME] ASC,
	[JOB_NAME] ASC,
	[JOB_GROUP] ASC
)WITH (STATISTICS_NORECOMPUTE = OFF, DROP_EXISTING = OFF, ONLINE = OFF, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
SET ANSI_PADDING ON
GO
/****** Object:  Index [IDX_QRTZ_T_JG]    Script Date: 12/12/2025 2:22:55 PM ******/
CREATE NONCLUSTERED INDEX [IDX_QRTZ_T_JG] ON [dbo].[QRTZ_TRIGGERS]
(
	[SCHED_NAME] ASC,
	[JOB_GROUP] ASC
)WITH (STATISTICS_NORECOMPUTE = OFF, DROP_EXISTING = OFF, ONLINE = OFF, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
SET ANSI_PADDING ON
GO
/****** Object:  Index [IDX_QRTZ_T_N_G_STATE]    Script Date: 12/12/2025 2:22:55 PM ******/
CREATE NONCLUSTERED INDEX [IDX_QRTZ_T_N_G_STATE] ON [dbo].[QRTZ_TRIGGERS]
(
	[SCHED_NAME] ASC,
	[TRIGGER_GROUP] ASC,
	[TRIGGER_STATE] ASC
)WITH (STATISTICS_NORECOMPUTE = OFF, DROP_EXISTING = OFF, ONLINE = OFF, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
SET ANSI_PADDING ON
GO
/****** Object:  Index [IDX_QRTZ_T_N_STATE]    Script Date: 12/12/2025 2:22:55 PM ******/
CREATE NONCLUSTERED INDEX [IDX_QRTZ_T_N_STATE] ON [dbo].[QRTZ_TRIGGERS]
(
	[SCHED_NAME] ASC,
	[TRIGGER_NAME] ASC,
	[TRIGGER_GROUP] ASC,
	[TRIGGER_STATE] ASC
)WITH (STATISTICS_NORECOMPUTE = OFF, DROP_EXISTING = OFF, ONLINE = OFF, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
SET ANSI_PADDING ON
GO
/****** Object:  Index [IDX_QRTZ_T_NEXT_FIRE_TIME]    Script Date: 12/12/2025 2:22:55 PM ******/
CREATE NONCLUSTERED INDEX [IDX_QRTZ_T_NEXT_FIRE_TIME] ON [dbo].[QRTZ_TRIGGERS]
(
	[SCHED_NAME] ASC,
	[NEXT_FIRE_TIME] ASC
)WITH (STATISTICS_NORECOMPUTE = OFF, DROP_EXISTING = OFF, ONLINE = OFF, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
SET ANSI_PADDING ON
GO
/****** Object:  Index [IDX_QRTZ_T_NFT_MISFIRE]    Script Date: 12/12/2025 2:22:55 PM ******/
CREATE NONCLUSTERED INDEX [IDX_QRTZ_T_NFT_MISFIRE] ON [dbo].[QRTZ_TRIGGERS]
(
	[SCHED_NAME] ASC,
	[MISFIRE_INSTR] ASC,
	[NEXT_FIRE_TIME] ASC
)WITH (STATISTICS_NORECOMPUTE = OFF, DROP_EXISTING = OFF, ONLINE = OFF, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
SET ANSI_PADDING ON
GO
/****** Object:  Index [IDX_QRTZ_T_NFT_ST]    Script Date: 12/12/2025 2:22:55 PM ******/
CREATE NONCLUSTERED INDEX [IDX_QRTZ_T_NFT_ST] ON [dbo].[QRTZ_TRIGGERS]
(
	[SCHED_NAME] ASC,
	[TRIGGER_STATE] ASC,
	[NEXT_FIRE_TIME] ASC
)WITH (STATISTICS_NORECOMPUTE = OFF, DROP_EXISTING = OFF, ONLINE = OFF, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
SET ANSI_PADDING ON
GO
/****** Object:  Index [IDX_QRTZ_T_NFT_ST_MISFIRE]    Script Date: 12/12/2025 2:22:55 PM ******/
CREATE NONCLUSTERED INDEX [IDX_QRTZ_T_NFT_ST_MISFIRE] ON [dbo].[QRTZ_TRIGGERS]
(
	[SCHED_NAME] ASC,
	[MISFIRE_INSTR] ASC,
	[NEXT_FIRE_TIME] ASC,
	[TRIGGER_STATE] ASC
)WITH (STATISTICS_NORECOMPUTE = OFF, DROP_EXISTING = OFF, ONLINE = OFF, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
SET ANSI_PADDING ON
GO
/****** Object:  Index [IDX_QRTZ_T_NFT_ST_MISFIRE_GRP]    Script Date: 12/12/2025 2:22:55 PM ******/
CREATE NONCLUSTERED INDEX [IDX_QRTZ_T_NFT_ST_MISFIRE_GRP] ON [dbo].[QRTZ_TRIGGERS]
(
	[SCHED_NAME] ASC,
	[MISFIRE_INSTR] ASC,
	[NEXT_FIRE_TIME] ASC,
	[TRIGGER_GROUP] ASC,
	[TRIGGER_STATE] ASC
)WITH (STATISTICS_NORECOMPUTE = OFF, DROP_EXISTING = OFF, ONLINE = OFF, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
SET ANSI_PADDING ON
GO
/****** Object:  Index [IDX_QRTZ_T_STATE]    Script Date: 12/12/2025 2:22:55 PM ******/
CREATE NONCLUSTERED INDEX [IDX_QRTZ_T_STATE] ON [dbo].[QRTZ_TRIGGERS]
(
	[SCHED_NAME] ASC,
	[TRIGGER_STATE] ASC
)WITH (STATISTICS_NORECOMPUTE = OFF, DROP_EXISTING = OFF, ONLINE = OFF, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
/****** Object:  Index [IX_Schedule_ClientId]    Script Date: 12/12/2025 2:22:55 PM ******/
CREATE NONCLUSTERED INDEX [IX_Schedule_ClientId] ON [dbo].[Schedule]
(
	[ClientId] ASC
)WITH (STATISTICS_NORECOMPUTE = OFF, DROP_EXISTING = OFF, ONLINE = OFF, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
/****** Object:  Index [IX_ScheduleSyncSource_ClientId]    Script Date: 12/12/2025 2:22:55 PM ******/
CREATE NONCLUSTERED INDEX [IX_ScheduleSyncSource_ClientId] ON [dbo].[ScheduleSyncSource]
(
	[ClientId] ASC
)WITH (STATISTICS_NORECOMPUTE = OFF, DROP_EXISTING = OFF, ONLINE = OFF, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
/****** Object:  Index [IX_ScheduleSyncSource_ExternalAccountId]    Script Date: 12/12/2025 2:22:55 PM ******/
CREATE UNIQUE NONCLUSTERED INDEX [IX_ScheduleSyncSource_ExternalAccountId] ON [dbo].[ScheduleSyncSource]
(
	[ExternalAccountId] ASC
)WITH (STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, DROP_EXISTING = OFF, ONLINE = OFF, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
/****** Object:  Index [IX_ScheduleSyncSource_ExternalClientId]    Script Date: 12/12/2025 2:22:55 PM ******/
CREATE NONCLUSTERED INDEX [IX_ScheduleSyncSource_ExternalClientId] ON [dbo].[ScheduleSyncSource]
(
	[ExternalClientId] ASC
)WITH (STATISTICS_NORECOMPUTE = OFF, DROP_EXISTING = OFF, ONLINE = OFF, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
SET ANSI_PADDING ON
GO
/****** Object:  Index [IX_ScheduleSyncSource_ExternalClientId_ExternalVendorId_AccountNumber]    Script Date: 12/12/2025 2:22:55 PM ******/
CREATE NONCLUSTERED INDEX [IX_ScheduleSyncSource_ExternalClientId_ExternalVendorId_AccountNumber] ON [dbo].[ScheduleSyncSource]
(
	[ExternalClientId] ASC,
	[ExternalVendorId] ASC,
	[AccountNumber] ASC
)WITH (STATISTICS_NORECOMPUTE = OFF, DROP_EXISTING = OFF, ONLINE = OFF, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
/****** Object:  Index [IX_ScheduleSyncSource_ExternalVendorId]    Script Date: 12/12/2025 2:22:55 PM ******/
CREATE NONCLUSTERED INDEX [IX_ScheduleSyncSource_ExternalVendorId] ON [dbo].[ScheduleSyncSource]
(
	[ExternalVendorId] ASC
)WITH (STATISTICS_NORECOMPUTE = OFF, DROP_EXISTING = OFF, ONLINE = OFF, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
/****** Object:  Index [IX_ScheduleSyncSource_LastSyncedDateTime]    Script Date: 12/12/2025 2:22:55 PM ******/
CREATE NONCLUSTERED INDEX [IX_ScheduleSyncSource_LastSyncedDateTime] ON [dbo].[ScheduleSyncSource]
(
	[LastSyncedDateTime] ASC
)WITH (STATISTICS_NORECOMPUTE = OFF, DROP_EXISTING = OFF, ONLINE = OFF, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
/****** Object:  Index [IX_User_ClientId]    Script Date: 12/12/2025 2:22:55 PM ******/
CREATE NONCLUSTERED INDEX [IX_User_ClientId] ON [dbo].[User]
(
	[ClientId] ASC
)WITH (STATISTICS_NORECOMPUTE = OFF, DROP_EXISTING = OFF, ONLINE = OFF, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
SET ANSI_PADDING ON
GO
/****** Object:  Index [IX_User_Email]    Script Date: 12/12/2025 2:22:55 PM ******/
CREATE UNIQUE NONCLUSTERED INDEX [IX_User_Email] ON [dbo].[User]
(
	[Email] ASC
)WITH (STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, DROP_EXISTING = OFF, ONLINE = OFF, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
SET ANSI_PADDING ON
GO
/****** Object:  Index [IX_User_ExternalIssuer_ExternalUserId]    Script Date: 12/12/2025 2:22:55 PM ******/
CREATE NONCLUSTERED INDEX [IX_User_ExternalIssuer_ExternalUserId] ON [dbo].[User]
(
	[ExternalIssuer] ASC,
	[ExternalUserId] ASC
)WITH (STATISTICS_NORECOMPUTE = OFF, DROP_EXISTING = OFF, ONLINE = OFF, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
/****** Object:  Index [IX_UserPermission_UserId]    Script Date: 12/12/2025 2:22:55 PM ******/
CREATE NONCLUSTERED INDEX [IX_UserPermission_UserId] ON [dbo].[UserPermission]
(
	[UserId] ASC
)WITH (STATISTICS_NORECOMPUTE = OFF, DROP_EXISTING = OFF, ONLINE = OFF, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
ALTER TABLE [dbo].[AdrAccount] ADD  CONSTRAINT [DF_AdrAccount_IsManuallyOverridden]  DEFAULT ((0)) FOR [IsManuallyOverridden]
GO
ALTER TABLE [dbo].[AdrJob] ADD  DEFAULT ((0)) FOR [IsManualRequest]
GO
ALTER TABLE [dbo].[AdrOrchestrationRun] ADD  DEFAULT ((0)) FOR [IsDeleted]
GO
ALTER TABLE [dbo].[Schedule] ADD  CONSTRAINT [DF__Schedule__IsSyst__01142BA1]  DEFAULT ((0)) FOR [IsSystemSchedule]
GO
ALTER TABLE [dbo].[AdrAccount]  WITH CHECK ADD  CONSTRAINT [FK_AdrAccount_Client_ClientId] FOREIGN KEY([ClientId])
REFERENCES [dbo].[Client] ([ClientId])
GO
ALTER TABLE [dbo].[AdrAccount] CHECK CONSTRAINT [FK_AdrAccount_Client_ClientId]
GO
ALTER TABLE [dbo].[AdrJob]  WITH CHECK ADD  CONSTRAINT [FK_AdrJob_AdrAccount_AdrAccountId] FOREIGN KEY([AdrAccountId])
REFERENCES [dbo].[AdrAccount] ([AdrAccountId])
ON DELETE CASCADE
GO
ALTER TABLE [dbo].[AdrJob] CHECK CONSTRAINT [FK_AdrJob_AdrAccount_AdrAccountId]
GO
ALTER TABLE [dbo].[AdrJobExecution]  WITH CHECK ADD  CONSTRAINT [FK_AdrJobExecution_AdrJob_AdrJobId] FOREIGN KEY([AdrJobId])
REFERENCES [dbo].[AdrJob] ([AdrJobId])
ON DELETE CASCADE
GO
ALTER TABLE [dbo].[AdrJobExecution] CHECK CONSTRAINT [FK_AdrJobExecution_AdrJob_AdrJobId]
GO
ALTER TABLE [dbo].[JobExecution]  WITH CHECK ADD  CONSTRAINT [FK_JobExecution_Schedule_ScheduleId] FOREIGN KEY([ScheduleId])
REFERENCES [dbo].[Schedule] ([ScheduleId])
ON DELETE CASCADE
GO
ALTER TABLE [dbo].[JobExecution] CHECK CONSTRAINT [FK_JobExecution_Schedule_ScheduleId]
GO
ALTER TABLE [dbo].[JobParameter]  WITH CHECK ADD  CONSTRAINT [FK_JobParameter_Schedule_ScheduleId] FOREIGN KEY([ScheduleId])
REFERENCES [dbo].[Schedule] ([ScheduleId])
ON DELETE CASCADE
GO
ALTER TABLE [dbo].[JobParameter] CHECK CONSTRAINT [FK_JobParameter_Schedule_ScheduleId]
GO
ALTER TABLE [dbo].[NotificationSetting]  WITH CHECK ADD  CONSTRAINT [FK_NotificationSetting_Schedule_ScheduleId] FOREIGN KEY([ScheduleId])
REFERENCES [dbo].[Schedule] ([ScheduleId])
ON DELETE CASCADE
GO
ALTER TABLE [dbo].[NotificationSetting] CHECK CONSTRAINT [FK_NotificationSetting_Schedule_ScheduleId]
GO
ALTER TABLE [dbo].[PasswordHistory]  WITH CHECK ADD  CONSTRAINT [FK_PasswordHistory_User_UserId] FOREIGN KEY([UserId])
REFERENCES [dbo].[User] ([UserId])
ON DELETE CASCADE
GO
ALTER TABLE [dbo].[PasswordHistory] CHECK CONSTRAINT [FK_PasswordHistory_User_UserId]
GO
ALTER TABLE [dbo].[QRTZ_BLOB_TRIGGERS]  WITH CHECK ADD  CONSTRAINT [FK_QRTZ_BLOB_TRIGGERS_QRTZ_TRIGGERS] FOREIGN KEY([SCHED_NAME], [TRIGGER_NAME], [TRIGGER_GROUP])
REFERENCES [dbo].[QRTZ_TRIGGERS] ([SCHED_NAME], [TRIGGER_NAME], [TRIGGER_GROUP])
ON DELETE CASCADE
GO
ALTER TABLE [dbo].[QRTZ_BLOB_TRIGGERS] CHECK CONSTRAINT [FK_QRTZ_BLOB_TRIGGERS_QRTZ_TRIGGERS]
GO
ALTER TABLE [dbo].[QRTZ_CRON_TRIGGERS]  WITH CHECK ADD  CONSTRAINT [FK_QRTZ_CRON_TRIGGERS_QRTZ_TRIGGERS] FOREIGN KEY([SCHED_NAME], [TRIGGER_NAME], [TRIGGER_GROUP])
REFERENCES [dbo].[QRTZ_TRIGGERS] ([SCHED_NAME], [TRIGGER_NAME], [TRIGGER_GROUP])
ON DELETE CASCADE
GO
ALTER TABLE [dbo].[QRTZ_CRON_TRIGGERS] CHECK CONSTRAINT [FK_QRTZ_CRON_TRIGGERS_QRTZ_TRIGGERS]
GO
ALTER TABLE [dbo].[QRTZ_SIMPLE_TRIGGERS]  WITH CHECK ADD  CONSTRAINT [FK_QRTZ_SIMPLE_TRIGGERS_QRTZ_TRIGGERS] FOREIGN KEY([SCHED_NAME], [TRIGGER_NAME], [TRIGGER_GROUP])
REFERENCES [dbo].[QRTZ_TRIGGERS] ([SCHED_NAME], [TRIGGER_NAME], [TRIGGER_GROUP])
ON DELETE CASCADE
GO
ALTER TABLE [dbo].[QRTZ_SIMPLE_TRIGGERS] CHECK CONSTRAINT [FK_QRTZ_SIMPLE_TRIGGERS_QRTZ_TRIGGERS]
GO
ALTER TABLE [dbo].[QRTZ_SIMPROP_TRIGGERS]  WITH CHECK ADD  CONSTRAINT [FK_QRTZ_SIMPROP_TRIGGERS_QRTZ_TRIGGERS] FOREIGN KEY([SCHED_NAME], [TRIGGER_NAME], [TRIGGER_GROUP])
REFERENCES [dbo].[QRTZ_TRIGGERS] ([SCHED_NAME], [TRIGGER_NAME], [TRIGGER_GROUP])
ON DELETE CASCADE
GO
ALTER TABLE [dbo].[QRTZ_SIMPROP_TRIGGERS] CHECK CONSTRAINT [FK_QRTZ_SIMPROP_TRIGGERS_QRTZ_TRIGGERS]
GO
ALTER TABLE [dbo].[QRTZ_TRIGGERS]  WITH CHECK ADD  CONSTRAINT [FK_QRTZ_TRIGGERS_QRTZ_JOB_DETAILS] FOREIGN KEY([SCHED_NAME], [JOB_NAME], [JOB_GROUP])
REFERENCES [dbo].[QRTZ_JOB_DETAILS] ([SCHED_NAME], [JOB_NAME], [JOB_GROUP])
GO
ALTER TABLE [dbo].[QRTZ_TRIGGERS] CHECK CONSTRAINT [FK_QRTZ_TRIGGERS_QRTZ_JOB_DETAILS]
GO
ALTER TABLE [dbo].[Schedule]  WITH CHECK ADD  CONSTRAINT [FK_Schedule_Client_ClientId] FOREIGN KEY([ClientId])
REFERENCES [dbo].[Client] ([ClientId])
GO
ALTER TABLE [dbo].[Schedule] CHECK CONSTRAINT [FK_Schedule_Client_ClientId]
GO
ALTER TABLE [dbo].[ScheduleSyncSource]  WITH CHECK ADD  CONSTRAINT [FK_ScheduleSyncSource_Client_ClientId] FOREIGN KEY([ClientId])
REFERENCES [dbo].[Client] ([ClientId])
GO
ALTER TABLE [dbo].[ScheduleSyncSource] CHECK CONSTRAINT [FK_ScheduleSyncSource_Client_ClientId]
GO
ALTER TABLE [dbo].[User]  WITH CHECK ADD  CONSTRAINT [FK_User_Client_ClientId] FOREIGN KEY([ClientId])
REFERENCES [dbo].[Client] ([ClientId])
GO
ALTER TABLE [dbo].[User] CHECK CONSTRAINT [FK_User_Client_ClientId]
GO
ALTER TABLE [dbo].[UserPermission]  WITH CHECK ADD  CONSTRAINT [FK_UserPermission_User_UserId] FOREIGN KEY([UserId])
REFERENCES [dbo].[User] ([UserId])
ON DELETE CASCADE
GO
ALTER TABLE [dbo].[UserPermission] CHECK CONSTRAINT [FK_UserPermission_User_UserId]
GO

ALTER TABLE [AdrAccount] ADD [LastSuccessfulDownloadDate] datetime2 NULL;
GO
