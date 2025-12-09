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

    public async Task<IEnumerable<AdrAccount>> GetByCredentialIdAsync(int credentialId)
    {
        return await _dbSet
            .Where(a => a.CredentialId == credentialId && !a.IsDeleted)
            .ToListAsync();
    }

    public async Task<IEnumerable<AdrAccount>> GetByClientIdAsync(int clientId)
    {
        return await _dbSet
            .Where(a => a.ClientId == clientId && !a.IsDeleted)
            .ToListAsync();
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
        string? searchTerm = null)
    {
        var query = _dbSet.Where(a => !a.IsDeleted);

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

        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            query = query.Where(a => 
                a.VMAccountNumber.Contains(searchTerm) ||
                (a.ClientName != null && a.ClientName.Contains(searchTerm)) ||
                (a.VendorCode != null && a.VendorCode.Contains(searchTerm)));
        }

        var totalCount = await query.CountAsync();
        var items = await query
            .OrderBy(a => a.NextRunDateTime)
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

    public async Task<int> GetCountByStatusAsync(string status, int? clientId = null)
    {
        var query = _dbSet.Where(a => !a.IsDeleted && a.NextRunStatus == status);
        
        if (clientId.HasValue)
        {
            query = query.Where(a => a.ClientId == clientId.Value);
        }

        return await query.CountAsync();
    }

    public async Task BulkUpsertAsync(IEnumerable<AdrAccount> accounts)
    {
        foreach (var account in accounts)
        {
            var existing = await GetByVMAccountIdAsync(account.VMAccountId);
            if (existing != null)
            {
                existing.VMAccountNumber = account.VMAccountNumber;
                existing.InterfaceAccountId = account.InterfaceAccountId;
                existing.ClientId = account.ClientId;
                existing.ClientName = account.ClientName;
                existing.CredentialId = account.CredentialId;
                existing.VendorCode = account.VendorCode;
                existing.PeriodType = account.PeriodType;
                existing.PeriodDays = account.PeriodDays;
                existing.MedianDays = account.MedianDays;
                existing.InvoiceCount = account.InvoiceCount;
                existing.LastInvoiceDateTime = account.LastInvoiceDateTime;
                existing.ExpectedNextDateTime = account.ExpectedNextDateTime;
                existing.ExpectedRangeStartDateTime = account.ExpectedRangeStartDateTime;
                existing.ExpectedRangeEndDateTime = account.ExpectedRangeEndDateTime;
                existing.NextRunDateTime = account.NextRunDateTime;
                existing.NextRangeStartDateTime = account.NextRangeStartDateTime;
                existing.NextRangeEndDateTime = account.NextRangeEndDateTime;
                existing.DaysUntilNextRun = account.DaysUntilNextRun;
                existing.NextRunStatus = account.NextRunStatus;
                existing.HistoricalBillingStatus = account.HistoricalBillingStatus;
                existing.LastSyncedDateTime = DateTime.UtcNow;
                existing.ModifiedDateTime = DateTime.UtcNow;
                existing.ModifiedBy = "System Created";
                
                _context.Entry(existing).State = EntityState.Modified;
            }
            else
            {
                account.CreatedDateTime = DateTime.UtcNow;
                account.CreatedBy = "System Created";
                account.ModifiedDateTime = DateTime.UtcNow;
                account.ModifiedBy = "System Created";
                account.LastSyncedDateTime = DateTime.UtcNow;
                
                await _dbSet.AddAsync(account);
            }
        }
    }
}
