using Actox.Domain.Entities;

namespace Actox.Domain.Interfaces
{
    public interface IProjectRepository
    {
        Task<int> AddOrUpdateProjectAsync(Project project, int userId);
    }
}