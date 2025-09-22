using ActoX.Application.DTOs;

namespace ActoX.Application.Interfaces
{
    public interface IProjectService
    {
        Task<bool> VerifyConnectionAsync(VerifyConnectionRequest request);
        Task<ProjectResponse> LinkProjectAsync(LinkProjectRequest request, int userId);
    }
}