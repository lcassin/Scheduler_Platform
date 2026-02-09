using Microsoft.EntityFrameworkCore;
using SchedulerPlatform.Core.Domain.Entities;
using SchedulerPlatform.Core.Domain.Interfaces;
using SchedulerPlatform.Infrastructure.Data;

namespace SchedulerPlatform.Infrastructure.Repositories;

public class AdrAccountRepository : Repository<AdrAccount>, IAdrAccountRepository
{
    public AdrAccountRepository(SchedulerDbContext context) : base(context)
    {
    }

    public async Task<AdrAccount?> GetByVMAccountIdAsync(long vmAccountId)
    {
        return await _dbSet
            .FirstOrDefaultAsync(a => a.VMAccountId == vmAccountId && !a.IsDeleted);
    }

    public async Task<IEnumerable<AdrAccount>> GetAccountsDueForRunAsync(DateTime currentDate)
    {
        return await _dbSet
            .Where(a => !a.IsDeleted && 
                        a.NextRunDateTime.HasValue && 
                        a.NextRunDateTime.Value.Date <= currentDate.Date)
            .ToListAsync();
    }

    public async Task<IEnumerable<AdrAccount>> GetAccountsNeedingCredentialCheckAsync(DateTime currentDate, int leadTimeDays = 7)
    {
        var checkDate = currentDate.AddDays(leadTimeDays);
        return await _dbSet
            .Where(a => !a.IsDeleted && 
                        a.NextRunDateTime.HasValue && 
                        a.NextRunDateTime.Value.Date <= checkDate.Date &&
                        (a.NextRunStatus == null || a.NextRunStatus == "Pending"))
            .ToListAsync();
    }

    public async Task<(IEnumerable<AdrAccount> items, int totalCount)> GetPagedAsync(
        int pageNumber,
        int pageSize,
        int? clientId = null,
        int? credentialId = null,
        string? nextRunStatus = null,
        string? searchTerm = null,
        string? historicalBillingStatus = null,
        bool? isOverridden = null,
        string? sortColumn = null,
        bool sortDescending = false,
        List<int>? accountIdsFilter = null,
        string? primaryVendorCode = null,
        string? masterVendorCode = null)
    {
        var query = _dbSet.Where(a => !a.IsDeleted);

        // Apply account IDs filter (for job status filtering)
        if (accountIdsFilter != null)
        {
            query = query.Where(a => accountIdsFilter.Contains(a.Id));
        }

        if (clientId.HasValue)
        {
            query = query.Where(a => a.ClientId == clientId.Value);
        }

        if (credentialId.HasValue)
        {
            query = query.Where(a => a.CredentialId == credentialId.Value);
        }

        if (!string.IsNullOrWhiteSpace(nextRunStatus))
        {
            query = query.Where(a => a.NextRunStatus == nextRunStatus);
        }

        if (!string.IsNullOrWhiteSpace(historicalBillingStatus))
        {
            query = query.Where(a => a.HistoricalBillingStatus == historicalBillingStatus);
        }

        if (isOverridden.HasValue)
        {
            query = query.Where(a => a.IsManuallyOverridden == isOverridden.Value);
        }

        if (!string.IsNullOrWhiteSpace(primaryVendorCode))
        {
            query = query.Where(a => a.PrimaryVendorCode == primaryVendorCode);
        }

        if (!string.IsNullOrWhiteSpace(masterVendorCode))
        {
            query = query.Where(a => a.MasterVendorCode == masterVendorCode);
        }

        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            query = query.Where(a => 
                a.VMAccountNumber.Contains(searchTerm) ||
                (a.InterfaceAccountId != null && a.InterfaceAccountId.Contains(searchTerm)) ||
                (a.ClientName != null && a.ClientName.Contains(searchTerm)) ||
                (a.PrimaryVendorCode != null && a.PrimaryVendorCode.Contains(searchTerm)) ||
                (a.MasterVendorCode != null && a.MasterVendorCode.Contains(searchTerm)));
        }

        var totalCount = await query.CountAsync();

        // Apply dynamic sorting
        IQueryable<AdrAccount> orderedQuery = sortColumn switch
        {
            "VMAccountNumber" => sortDescending 
                ? query.OrderByDescending(a => a.VMAccountNumber ?? "") 
                : query.OrderBy(a => a.VMAccountNumber ?? ""),
            "InterfaceAccountId" => sortDescending 
                ? query.OrderByDescending(a => a.InterfaceAccountId ?? "") 
                : query.OrderBy(a => a.InterfaceAccountId ?? ""),
            "ClientName" => sortDescending 
                ? query.OrderByDescending(a => a.ClientName ?? "") 
                : query.OrderBy(a => a.ClientName ?? ""),
            "PrimaryVendorCode" => sortDescending 
                ? query.OrderByDescending(a => a.PrimaryVendorCode ?? "") 
                : query.OrderBy(a => a.PrimaryVendorCode ?? ""),
            "MasterVendorCode" => sortDescending 
                ? query.OrderByDescending(a => a.MasterVendorCode ?? "") 
                : query.OrderBy(a => a.MasterVendorCode ?? ""),
            "PeriodType" => sortDescending 
                ? query.OrderByDescending(a => a.PeriodType ?? "") 
                : query.OrderBy(a => a.PeriodType ?? ""),
            "NextRunDateTime" => sortDescending 
                ? query.OrderByDescending(a => a.NextRunDateTime ?? DateTime.MaxValue) 
                : query.OrderBy(a => a.NextRunDateTime ?? DateTime.MaxValue),
            "NextRunStatus" => sortDescending 
                ? query.OrderByDescending(a => a.NextRunStatus ?? "") 
                : query.OrderBy(a => a.NextRunStatus ?? ""),
            "HistoricalBillingStatus" => sortDescending 
                ? query.OrderByDescending(a => a.HistoricalBillingStatus ?? "") 
                : query.OrderBy(a => a.HistoricalBillingStatus ?? ""),
            "LastInvoiceDateTime" => sortDescending 
                ? query.OrderByDescending(a => a.LastInvoiceDateTime ?? DateTime.MinValue) 
                : query.OrderBy(a => a.LastInvoiceDateTime ?? DateTime.MinValue),
            "ExpectedNextDateTime" => sortDescending 
                ? query.OrderByDescending(a => a.ExpectedNextDateTime ?? DateTime.MaxValue) 
                : query.OrderBy(a => a.ExpectedNextDateTime ?? DateTime.MaxValue),
            _ => sortDescending 
                ? query.OrderByDescending(a => a.NextRunDateTime ?? DateTime.MaxValue) 
                : query.OrderBy(a => a.NextRunDateTime ?? DateTime.MaxValue)
        };

        var items = await orderedQuery
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return (items, totalCount);
    }

    public async Task<int> GetTotalCountAsync(int? clientId = null)
    {
        var query = _dbSet.Where(a => !a.IsDeleted);
        
        if (clientId.HasValue)
        {
            query = query.Where(a => a.ClientId == clientId.Value);
        }

        return await query.CountAsync();
    }

    public async Task<int> GetCountByNextRunStatusAsync(string status, int? clientId = null)
    {
        var query = _dbSet.Where(a => !a.IsDeleted && a.NextRunStatus == status);
        
        if (clientId.HasValue)
        {
            query = query.Where(a => a.ClientId == clientId.Value);
        }

        return await query.CountAsync();
    }

    public async Task<int> GetCountByHistoricalStatusAsync(string status, int? clientId = null)
    {
        var query = _dbSet.Where(a => !a.IsDeleted && a.HistoricalBillingStatus == status);
        
        if (clientId.HasValue)
        {
            query = query.Where(a => a.ClientId == clientId.Value);
        }

        return await query.CountAsync();
    }

    public async Task<IEnumerable<AdrAccount>> GetDueAccountsWithRulesAsync()
    {
        // Query based on RULE scheduling data, not account data
        // Rules drive the orchestrator per BRD requirements
        //
        // Jobs are created when NextRunDate <= today (the day scraping should start)
        // Since the orchestration runs Job Creation before Scraping in the same run,
        // jobs will exist when the scraping step executes.
        var today = DateTime.UtcNow.Date;
        
        return await _dbSet
            .Include(a => a.AdrAccountRules.Where(r => !r.IsDeleted && r.IsEnabled))
            .Where(a =>
                !a.IsDeleted &&
                a.HistoricalBillingStatus != "Missing" &&
                // Account must have at least one enabled rule that is due
                a.AdrAccountRules.Any(r => 
                    !r.IsDeleted && 
                    r.IsEnabled &&
                    r.JobTypeId == 2 && // DownloadInvoice
                    r.NextRunDateTime.HasValue &&
                    r.NextRunDateTime.Value.Date <= today &&
                    r.NextRangeStartDateTime.HasValue &&
                    r.NextRangeEndDateTime.HasValue))
            .ToListAsync();
    }

    public async Task<IEnumerable<AdrAccount>> GetAllActiveAccountsForCredentialCheckAsync()
    {
        return await _dbSet
            .Where(a => !a.IsDeleted && a.CredentialId > 0)
            .ToListAsync();
    }
}
