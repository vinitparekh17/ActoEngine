using ActoX.Domain.Entities;

namespace ActoX.Domain.Interfaces;

public interface IUserRepository
{
    Task<(IEnumerable<User> Users, int TotalCount)> GetAllAsync(int page = 1, int pageSize = 10, CancellationToken cancellationToken = default);
    Task<User?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<User?> GetByUserNameAsync(string userName, CancellationToken cancellationToken = default);
    Task<User> AddAsync(User user, CancellationToken cancellationToken = default);
    Task UpdateAsync(User user, CancellationToken cancellationToken = default);
    Task DeleteAsync(int id, CancellationToken cancellationToken = default);
}
