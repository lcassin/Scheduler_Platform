using SchedulerPlatform.Core.Domain.Entities;

namespace SchedulerPlatform.Core.Domain.Interfaces;

public interface IUserPermissionRepository : IRepository<UserPermission>
{
    Task<IEnumerable<UserPermission>> GetByUserIdAsync(int userId);
    Task RemoveAllForUserAsync(int userId);
}
