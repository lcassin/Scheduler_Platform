using Microsoft.EntityFrameworkCore;
using SchedulerPlatform.Core.Domain.Entities;
using SchedulerPlatform.Core.Domain.Interfaces;
using SchedulerPlatform.Infrastructure.Data;

namespace SchedulerPlatform.Infrastructure.Repositories;

public class UserPermissionRepository : Repository<UserPermission>, IUserPermissionRepository
{
    public UserPermissionRepository(SchedulerDbContext context) : base(context)
    {
    }

    public async Task<IEnumerable<UserPermission>> GetByUserIdAsync(int userId)
    {
        return await _dbSet
            .Where(p => p.UserId == userId)
            .ToListAsync();
    }

    public async Task RemoveAllForUserAsync(int userId)
    {
        var permissions = await _dbSet
            .Where(p => p.UserId == userId)
            .ToListAsync();
        
        _dbSet.RemoveRange(permissions);
    }
}
