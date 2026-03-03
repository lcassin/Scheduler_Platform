# ADR Status Guide

## Introduction

This guide explains every status you will see across the ADR (Automated Document Retrieval) platform. It is organized into three sections matching the main areas of the application:

1. **Dashboard** - The overview cards and charts you see when you first log in
2. **Accounts** - The statuses shown on the ADR Accounts page
3. **Jobs** - The statuses shown on the ADR Jobs page

Each status is explained in plain language: what it means, why it appears, and what (if anything) you need to do about it.

---

## Section 1: Dashboard

The Dashboard is the first thing you see when you open the application. It gives you a quick snapshot of everything happening in the ADR system.

### 1.1 ADR Overview Cards

These are the five colored cards at the top of the Dashboard page.

![Dashboard Overview Cards](diagrams/status-guide-dashboard-cards.png)

| Card | Color | What It Shows | How It Is Calculated |
|------|-------|---------------|----------------------|
| **Total Accounts** | Blue | The total number of vendor accounts that are set up for automated document retrieval. | Counts all ADR-enabled accounts in the system (excludes any that have been removed or deactivated). |
| **Run Now** | Red | Accounts that are ready to be processed right now. Their scheduled run date has arrived or already passed. | Counts accounts where the Next Run Date is today or earlier. These are the accounts the system will attempt to retrieve invoices for during the next orchestration run. |
| **Due Soon** | Orange | Accounts that will need to be processed within the next few days (within the billing window). | Counts accounts where the Next Run Date is coming up within the account's billing window (typically 3-7 days depending on billing frequency). |
| **Missing Invoices** | Orange/Red | Accounts where invoices have not been found for two or more billing cycles. These need investigation. | Counts accounts where the expected invoice date is more than two full billing periods in the past. For example, a monthly account would be "Missing" if no invoice has been found for over 60 days past its expected date. |
| **Active Jobs** | Green | The total number of jobs currently being worked on by the system. | Counts all jobs that are in an active state (Pending, Credential Verified, or ADR Request Sent). Jobs that are Completed, Failed, or Cancelled are not included. |

**What should I do?**
- **Run Now** and **Due Soon** cards are informational - the system handles these automatically.
- **Missing Invoices** may require investigation. Click the card to see which accounts are missing invoices and determine if action is needed (e.g., checking if the vendor portal has changed, or if billing data needs correction).

### 1.2 ADR Account Status Chart

Below the overview cards, you will see a donut chart titled **"ADR Account Status."** This chart shows the breakdown of all accounts by their **Run Status** (also called "Next Run Status").

![ADR Account Status Chart](diagrams/status-guide-account-chart.png)

| Status | Chart Color | What It Means |
|--------|-------------|---------------|
| **Upcoming** | Cyan/Light Blue | The account's next run date is within the next 30 days, but not within the immediate billing window yet. No action is needed - the system will process it when the time comes. |
| **Due Soon** | Orange | The account's next run date is within the billing window (typically 3-7 days away). The system is preparing to process this account. |
| **Missing** | Red | The account has not had a successful invoice retrieval for two or more billing cycles. This usually means something needs to be investigated. |
| **Future** | Green | The account's next run date is more than 30 days away. Everything is on track and no action is needed. |
| **Run Now** | Dark Red/Crimson | The account's next run date is today or has already passed. The system will process this account during the next orchestration run. |

Below the chart, you will also see counts for each status and a **Blacklist Status** section showing how many accounts are currently excluded from processing.

### 1.3 ADR Job Status Section

This section shows the current state of all jobs in the system, displayed both as colored cards and as a pie chart.

![ADR Job Status Section](diagrams/status-guide-job-status.png)

| Status | Card Color | What It Means |
|--------|------------|---------------|
| **Pending** | Gray | The job has been created but the system has not yet sent an ADR request. The job is waiting for its scheduled date to arrive. |
| **Credential Verified** | Blue | The system has confirmed that the login credentials for this vendor account are working. The job is ready for an ADR request to be sent when the run date arrives. |
| **ADR Request Sent** | Purple | An ADR request has been submitted to retrieve the invoice. The system is waiting for a response. |
| **Completed** | Green | The invoice was successfully retrieved. This job is finished. |
| **Failed** | Red | The system was unable to retrieve the invoice after multiple attempts. This may require investigation. |
| **Needs Review** | Orange | The ADR system returned a result that requires a person to look at it. This could mean the invoice was partially retrieved or there was an issue that the system cannot resolve on its own. |

### 1.4 Job Pipeline Status

This panel shows jobs organized by which phase of processing they are in.

![Job Pipeline Status](diagrams/status-guide-job-pipeline.png)

**Credential Phase** - These jobs are in the early stage of processing:
| Status | What It Means |
|--------|---------------|
| **Pending** | Job created, waiting to be processed. |
| **Credential Verified** | Login credentials confirmed as working. |
| **Credential Failed** | Login credentials did not work. The helpdesk has been notified. The system will still attempt to retrieve the invoice in case the credentials are fixed. |

**ADR Document Phase** - These jobs have moved past credential checking and are in the document retrieval stage:
| Status | What It Means |
|--------|---------------|
| **ADR Request Sent** | A request to retrieve the invoice has been submitted. Waiting for results. |
| **Completed** | Invoice successfully retrieved and delivered. |
| **Failed** | Invoice retrieval failed after all retry attempts. |
| **Needs Review** | A person needs to review the result and take action. |

### 1.5 Scheduler Metrics

If your system also uses the Scheduler feature (for running automated tasks like stored procedures or API calls), you will see a **Scheduler Metrics** section. This section has its own filters, charts, and a recent executions grid.

#### Filters

At the top of the Scheduler Metrics section you will find two filters:

| Filter | Options | What It Does |
|--------|---------|--------------|
| **Time Window** | Last 24 Hours, Last 7 Days, Last 30 Days | Controls the date range for all cards, charts, and the executions grid below. For example, selecting "Last 7 Days" shows data from the past week. |
| **Filter by Status** | Failed, Running, Completed, Retrying, Cancelled | Lets you narrow down which execution statuses are shown. You can select multiple statuses. When nothing is selected, all statuses are included. |

A **Refresh** button is also available to manually reload the data. The section auto-refreshes every 60 seconds.

#### Overview Cards

![Scheduler Metrics](diagrams/status-guide-scheduler-metrics.png)

| Card | Color | What It Shows |
|------|-------|---------------|
| **Total Schedules** | Blue | The total number of scheduled tasks configured in the system, showing how many are enabled vs. disabled. Click to go to the Schedules page. |
| **Running Now** | Blue | The number of scheduled tasks that are actively running at this moment. "Peak concurrent" shows the most tasks that were running at the same time during the selected time window. Click to view running executions. |
| **Completed Today** | Green | The number of scheduled tasks that finished successfully today. "Avg" shows the average time each task took to complete. Click to view completed executions. |
| **Failed Today** | Red | The number of scheduled tasks that failed today. "Total in window" shows how many tasks ran in the selected time window. Click to view failed executions. |
| **Missed Schedules** | Orange | Scheduled tasks that were supposed to run but did not (in the last 24 hours). This could mean the system was down or the task could not start for some reason. Click to view details. |

#### Charts

Below the overview cards, four charts provide visual insight into schedule execution patterns:

| Chart | Type | What It Shows |
|-------|------|---------------|
| **Status Breakdown** | Donut chart | A visual breakdown of execution statuses (Completed, Failed, Running, etc.) as percentages for the selected time window. Each slice represents one status, and the counts and percentages are listed below the chart. |
| **Execution Duration Trends** | Line chart | Shows how long executions took over time within the selected time window. Helps identify if tasks are getting slower or faster. |
| **Concurrent Executions Over Time** | Line chart | Shows how many tasks were running at the same time throughout the selected time window. Useful for spotting peak load times. |
| **Top 10 Longest Running Schedules** | Bar chart | Shows the 10 schedules that took the longest to complete within the selected time window. Helps identify tasks that may need optimization. |

All four charts respect the **Time Window** and **Filter by Status** selections. Changing either filter updates all charts automatically.

#### Top 10 Schedule Executions Grid

At the bottom of the Scheduler Metrics section, a table shows the **10 most recent schedule executions** for the selected time window, sorted by start time (newest first).

| Column | What It Shows |
|--------|---------------|
| **Schedule Name** | The name of the scheduled task that ran. |
| **Status** | The execution result, shown as a color-coded label (green for Completed, red for Failed, blue for Running, etc.). |
| **Started** | The date and time the execution began. |
| **Ended** | The date and time the execution finished. Shows "Running..." if still in progress. |
| **Duration** | How long the execution took (e.g., "2m 37s"). |
| **Actions** | A view details icon (eye icon) that opens a dialog with full execution details including: Execution ID, Schedule name, Status, Duration, Start/End times, Retry count, Triggered by, and any Output or Error messages. |

If there are more than 10 executions in the selected time window, a **"View All Executions"** button appears that takes you to the full Executions page.

---

## Section 2: Accounts

The **ADR Accounts** page shows all vendor accounts in the system. Each row represents one vendor account and displays several status columns.

![Accounts Page](diagrams/status-guide-accounts-page.png)

### 2.1 Historical Status

The **Historical Status** column tells you about the account's billing history - specifically, how the account's expected invoice date compares to today. This status is calculated during the daily account sync based on the account's billing pattern.

| Status | Color | What It Means | How It Is Calculated |
|--------|-------|---------------|----------------------|
| **Missing** | Red | No invoice has been found for this account for two or more full billing cycles. Something may be wrong. | The expected invoice date is more than (2 x billing period) days in the past. For a monthly account, this means the expected date is over 60 days ago. |
| **Overdue** | Orange | The expected invoice date has passed, but it has not been long enough to be considered "Missing" yet. | The expected date is past due but within the "missing" threshold. The invoice might just be late. |
| **Due Now** | Blue | The expected invoice date is right around now (within the billing window before the expected date). | The expected date is in the recent past (within the window days). |
| **Due Soon** | Default | The expected invoice date is coming up within the billing window. | The expected date is in the near future, within the window days. |
| **Upcoming** | Default | The expected invoice date is within the next 30 days but not yet in the billing window. | The expected date is 1-30 days away, outside the immediate window. |
| **Future** | Default | The expected invoice date is more than 30 days away. | The expected date is over 30 days in the future. |

**What should I do?**
- **Missing** accounts need investigation. The vendor may have changed their billing, the account may be inactive, or there could be a credential issue.
- **Overdue** accounts may resolve on their own if the invoice is just late. Monitor them.
- All other statuses are normal and require no action.

### 2.2 Run Status (Next Run Status)

The **Run Status** column tells you when the system will next attempt to retrieve an invoice for this account. This is based on the account's **Next Run Date** - the calculated date when the system should start looking for the next invoice.

| Status | Color | What It Means | How It Is Calculated |
|--------|-------|---------------|----------------------|
| **Run Now** | Red | The system is ready to process this account. The next run date is today or has already passed. | Days until Next Run Date is 0 or negative (date is today or in the past). |
| **Due Soon** | Orange | The next run date is coming up within the billing window (typically 3-7 days). | Days until Next Run Date is within the window days for this billing frequency. |
| **Upcoming** | Cyan | The next run date is within the next 30 days. | Days until Next Run Date is between the window days and 30 days. |
| **Future** | Green | The next run date is more than 30 days away. | Days until Next Run Date is more than 30. |
| **Missing** | Red | This account is in "Missing" status (see Historical Status above) and the system will not create new jobs for it until the issue is resolved. | Automatically set to "Missing" when the Historical Status is "Missing." |

**What should I do?**
- **Run Now** is normal - the system handles these automatically.
- **Missing** accounts need investigation before the system will resume processing them.
- All other statuses are informational and require no action.

### 2.3 Job Status (on the Accounts page)

The **Job Status** column on the Accounts page shows the status of the **most recent job** for that account. This gives you a quick view of where the account's latest invoice retrieval stands without needing to go to the Jobs page.

| Status | Display Name | Color | What It Means |
|--------|-------------|-------|---------------|
| **Pending** | Pending | Gray | A job exists but the ADR request has not been sent yet. |
| **CredentialVerified** | Credential Verified | Blue | Login credentials have been confirmed as working. |
| **CredentialFailed** | Credential Failed | Red | Login credentials failed. The system will still attempt retrieval. |
| **ScrapeRequested** | Request Sent | Purple | An ADR request has been sent and we are waiting for a response. |
| **Completed** | Completed | Green | The invoice was successfully retrieved. |
| **Failed** | Failed | Red | Invoice retrieval failed after all attempts. |
| **NeedsReview** | Needs Review | Orange | The result requires manual review. |
| *(blank/N/A)* | N/A | Gray | No job exists for this account yet. |

### 2.4 Blacklist Status (on the Accounts page)

Some accounts may have blacklist indicators showing that they are excluded from automatic processing.

| Indicator | What It Means |
|-----------|---------------|
| **Currently Blacklisted** (red icon) | This account is currently excluded from automatic ADR processing. New jobs will not be created for it. You can hover over the icon to see the reason and date range. |
| **Future Blacklist** (yellow icon) | A blacklist entry is scheduled to take effect in the future. The account is still being processed normally for now. |

---

## Section 3: Jobs

The **ADR Jobs** page shows all individual invoice retrieval jobs. Each job represents one attempt to retrieve an invoice for one account for one billing period.

![Jobs Page](diagrams/status-guide-jobs-page.png)

### 3.1 Status (Job Status)

The **Status** column shows where the job is in the retrieval process. Jobs move through these statuses as the system processes them.

| Status | Color | What It Means | What Happens Next |
|--------|-------|---------------|-------------------|
| **Pending** | Gray | The job has been created and is waiting to be processed. The system will begin working on it when the scheduled date arrives. | The system will first verify credentials (if time permits before the run date), then send an ADR request on the run date. |
| **CredentialCheckInProgress** | Blue | The system is currently checking if the login credentials for this vendor work. | If successful, the job moves to "Credential Verified." If it fails, it moves to "Credential Failed." |
| **CredentialVerified** | Blue | The login credentials have been confirmed as working. The job is ready for an ADR request. | On the Next Run Date, the system will send an ADR request to retrieve the invoice. |
| **CredentialFailed** | Red | The login credentials did not work. A notification has been sent to the helpdesk. | The system will still attempt to send ADR requests daily. If the helpdesk fixes the credentials, the next attempt should succeed. |
| **ScrapeInProgress** | Blue | An ADR request is currently being sent to the vendor portal. | Once the request is submitted, the status changes to "ADR Request Sent." |
| **ScrapeRequested** (displays as "ADR Request Sent") | Orange | An ADR request has been submitted and the system is waiting for the vendor portal to process it. The system checks for results the next day. | The system will check the status the following day. If the invoice is found, the job is marked Complete. If not, another request is sent (daily until the billing window closes). |
| **StatusCheckInProgress** | Blue | The system is currently checking the result of a previously sent ADR request. | If the invoice was found, the job moves to "Completed." If not found yet, it stays as "ADR Request Sent" for another try tomorrow. |
| **Completed** | Green | The invoice was successfully retrieved and delivered. This job is finished. | No further action needed. The account will be processed again in the next billing cycle. |
| **Failed** | Red | The system was unable to retrieve the invoice after all retry attempts, or a critical error occurred. | If retries remain (up to 5), the system will try again. If max retries are reached, manual intervention may be needed. |
| **NeedsReview** | Orange | The ADR system returned a result that requires human review. This could mean the document was partially retrieved or there was an issue the system cannot resolve automatically. | A person needs to review this job and determine the appropriate action. |
| **Cancelled** | Gray | The job was cancelled, usually because the billing dates were changed or the job's processing window expired without any ADR requests being sent. | No further action. A new job will be created for the correct billing period if needed. |

**How jobs progress through statuses (typical flow):**

```
Pending --> Credential Verified --> ADR Request Sent --> Completed
```

If issues occur, the flow may look like:

```
Pending --> Credential Failed --> ADR Request Sent --> Failed
                                                   --> Needs Review
```

### 3.2 ADR Status

The **ADR Status** column shows the detailed response from the ADR vendor portal system. This is more technical than the Job Status and gives you insight into exactly what happened with the request.

| ADR Status | What It Means | Is It Final? |
|------------|---------------|--------------|
| **N/A** | No ADR request has been sent yet for this job. | - |
| **Awaiting insert** | An ADR request was sent but has not been assigned a tracking number yet. | No |
| **Inserted** | The request has been received and is queued for processing. | No |
| **Inserted with Priority** | A high-priority request has been received and queued. | No |
| **Invalid CredentialID** | The credentials provided are not valid. This is an error. | Yes (Error) |
| **Cannot Connect To VCM** | The system could not connect to the vendor portal. This is usually a temporary issue. | Yes (Error) |
| **Cannot Insert Into Queue** | The request could not be added to the processing queue. | Yes (Error) |
| **Sent To AI** | The request has been sent to the document processing engine. | No |
| **Cannot Connect To AI** | The document processing engine could not be reached. | Yes (Error) |
| **Cannot Save Result** | The retrieved document could not be saved. | Yes (Error) |
| **Needs Human Review** | The system needs a person to review the result. | Yes |
| **Received From AI** | Results have been received from the document processing engine and are being finalized. | No |
| **Complete** | The invoice was successfully retrieved and saved. | Yes (Success) |
| **Login Attempt Succeeded** | Credential verification was successful (this status appears during credential checks, not during invoice retrieval). | No |
| **No Documents Found** | No invoice was found for this billing period on this attempt. The system will try again the next day. | No (Retry) |
| **Failed To Process All Documents** | Some documents may have been retrieved but others failed. | Yes (Error) |
| **No Documents Processed** | No documents were processed. The system may retry. | No |

**What does "Final" mean?**
- **Final = Yes**: The ADR system has finished processing this request. No more updates will come.
- **Final = No**: The request is still being processed. The status may change on the next check.
- **Final = Yes (Error)**: Something went wrong. The job may be retried or may need manual attention.

### 3.3 Next Action

The **Next Action** column tells you what the system will do next for this job. It is a plain-language description of the upcoming step.

| Next Action | When You See It | What It Means |
|-------------|-----------------|---------------|
| **Scheduled** | Pending jobs with a future credential check date | The job is scheduled and the credential check date is more than 7 days away. Nothing to do yet. |
| **Cred check in Xd** | Pending jobs approaching credential check window | The credential check will happen in X days. |
| **Credential check due** | Pending jobs in the credential check window | The credential check is due now or very soon. The system will handle this automatically. |
| **ADR request due (no cred check)** | Pending jobs where the run date has arrived without credential verification | The job missed the credential check window but will still attempt the ADR request directly. |
| **Checking credentials...** | Jobs currently having credentials checked | A credential check is in progress right now. |
| **ADR request due now** | Credential Verified jobs where the run date has arrived | The system will send an ADR request during the next orchestration run. |
| **ADR request tomorrow** | Credential Verified jobs 1 day before run date | The ADR request will be sent tomorrow. |
| **ADR request in Xd** | Credential Verified jobs with a future run date | The ADR request will be sent in X days. |
| **ADR retry due** | Credential Failed jobs where the run date has arrived | Despite the credential failure, the system will attempt an ADR request (the credentials may have been fixed). |
| **ADR request in progress...** | Jobs currently sending an ADR request | An ADR request is being submitted right now. |
| **Awaiting ADR result** | Jobs where an ADR request has been sent | Waiting for the vendor portal to process the request. Status will be checked the next day. |
| **Checking status...** | Jobs currently having their ADR status checked | The system is checking the result of a previous request right now. |
| **Retry scheduled** | Failed jobs with retries remaining | The system will automatically retry. |
| **Max retries reached** | Failed jobs with no retries remaining | All automatic retries have been used. Manual intervention may be needed. You can use the "Refire" option to try again. |
| **Complete** | Completed jobs | The invoice was retrieved successfully. No further action needed. |
| **Manual review required** | Jobs in Needs Review status | A person needs to look at this job and decide what to do. |

---

## Quick Reference: Where Do I See Each Status?

| Page | Column | Possible Values |
|------|--------|-----------------|
| Dashboard Cards | Total Accounts | Count of all ADR-enabled accounts |
| Dashboard Cards | Run Now | Count of accounts ready to process |
| Dashboard Cards | Due Soon | Count of accounts due within billing window |
| Dashboard Cards | Missing Invoices | Count of accounts missing 2+ billing cycles |
| Dashboard Cards | Active Jobs | Count of jobs in Pending, Credential Verified, or ADR Request Sent status |
| Account Status Chart | Run Status | Run Now, Due Soon, Upcoming, Future, Missing |
| Job Status Cards/Chart | Job Status | Pending, Credential Verified, ADR Request Sent, Completed, Failed, Needs Review |
| Accounts Page | Historical Status | Missing, Overdue, Due Now, Due Soon, Upcoming, Future |
| Accounts Page | Run Status | Run Now, Due Soon, Upcoming, Future, Missing |
| Accounts Page | Job Status | Pending, Credential Verified, Credential Failed, Request Sent, Completed, Failed, Needs Review |
| Jobs Page | Status | Pending, Credential Verified, Credential Failed, ADR Request Sent, Completed, Failed, Needs Review, Cancelled |
| Jobs Page | ADR Status | N/A, Inserted, Sent To AI, Complete, No Documents Found, Needs Human Review, and others (see Section 3.2) |
| Jobs Page | Next Action | Scheduled, Cred check in Xd, ADR request due now, Awaiting ADR result, Complete, and others (see Section 3.3) |

---

## Frequently Asked Questions

**Q: An account shows "Missing" - what should I do?**
A: This means no invoice has been found for two or more billing cycles. Check if the vendor portal still has invoices available, verify the account's billing frequency is correct, and check if the credentials are working. You may need to manually correct the billing dates.

**Q: A job shows "Failed" - will the system try again?**
A: Yes, if the job has fewer than 5 retry attempts. You can see the retry count on the Jobs page. If all retries are exhausted, you can use the "Refire" option from the job's action menu to try again.

**Q: What does "Needs Review" mean?**
A: The ADR system returned a result that it could not process automatically. This usually means a person needs to check the vendor portal directly to see what happened. Common causes include partial document retrieval or unexpected portal changes.

**Q: Why does an account show "Run Now" but there is no job for it?**
A: Jobs are created during the daily orchestration run. If the orchestration has not run yet today, the job may not have been created yet. Jobs are also not created for accounts in "Missing" status - those need to be investigated first.

**Q: What is the difference between "Historical Status" and "Run Status"?**
A: **Historical Status** is based on the account's billing history (when was the last invoice found vs. when was it expected). **Run Status** is based on the scheduling (when will the system next attempt to retrieve an invoice). An account could have a Historical Status of "Due Now" (the invoice is expected around now based on history) while having a Run Status of "Future" (the next scheduled run is far away) if the system has already handled the current period.

**Q: What does "Blacklisted" mean?**
A: A blacklisted account is temporarily excluded from automatic ADR processing. This is usually done intentionally by an administrator - for example, when a vendor is known to be having portal issues. Blacklisted accounts will not have new jobs created for them until the blacklist is removed or expires.
