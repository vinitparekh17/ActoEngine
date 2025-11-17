using ActoEngine.WebApi.Models;
using ActoEngine.WebApi.Repositories;

namespace ActoEngine.WebApi.Services.ProjectClientService
{
    public interface IProjectClientService
    {
        Task<ProjectClientDetailResponse> LinkClientToProjectAsync(int projectId, int clientId, int userId);
        Task<bool> UnlinkClientFromProjectAsync(int projectId, int clientId, int userId);
        Task<IEnumerable<ProjectClientDetailResponse>> LinkMultipleClientsToProjectAsync(LinkMultipleClientsRequest request, int userId);
        Task<IEnumerable<ProjectClientDetailResponse>> LinkClientToMultipleProjectsAsync(LinkClientToMultipleProjectsRequest request, int userId);
        Task<IEnumerable<ProjectClientDetailResponse>> GetClientsByProjectAsync(int projectId);
        Task<IEnumerable<ProjectClientDetailResponse>> GetProjectsByClientAsync(int clientId);
        Task<bool> IsLinkedAsync(int projectId, int clientId);
    }

    public class ProjectClientService(
        IProjectClientRepository projectClientRepository,
        IProjectRepository projectRepository,
        IClientRepository clientRepository,
        ILogger<ProjectClientService> logger) : IProjectClientService
    {
        private readonly IProjectClientRepository _projectClientRepository = projectClientRepository;
        private readonly IProjectRepository _projectRepository = projectRepository;
        private readonly IClientRepository _clientRepository = clientRepository;
        private readonly ILogger<ProjectClientService> _logger = logger;

        public async Task<ProjectClientDetailResponse> LinkClientToProjectAsync(int projectId, int clientId, int userId)
        {
            try
            {
                // Validate that project exists
                var project = await _projectRepository.GetByIdAsync(projectId);
                if (project == null)
                {
                    throw new InvalidOperationException($"Project with ID {projectId} not found");
                }

                // Validate that client exists
                var client = await _clientRepository.GetByIdAsync(clientId);
                if (client == null)
                {
                    throw new InvalidOperationException($"Client with ID {clientId} not found");
                }

                // Check if already linked
                var isLinked = await _projectClientRepository.IsLinkedAsync(projectId, clientId);
                if (isLinked)
                {
                    _logger.LogWarning("Client {ClientId} is already linked to project {ProjectId}", clientId, projectId);
                    // Return existing link details
                    var existingLinks = await _projectClientRepository.GetClientsByProjectAsync(projectId);
                    var existingLink = existingLinks.FirstOrDefault(l => l.ClientId == clientId);
                    if (existingLink != null)
                    {
                        return existingLink;
                    }
                }

                // Link the client to the project
                await _projectClientRepository.LinkAsync(projectId, clientId, userId);

                // Fetch and return the linked details
                var linkedClients = await _projectClientRepository.GetClientsByProjectAsync(projectId);
                var linkedClient = linkedClients.FirstOrDefault(l => l.ClientId == clientId);

                if (linkedClient == null)
                {
                    throw new InvalidOperationException($"Failed to retrieve linked client {clientId} for project {projectId}");
                }

                _logger.LogInformation("Successfully linked client {ClientId} ({ClientName}) to project {ProjectId} ({ProjectName})",
                    clientId, linkedClient.ClientName, projectId, linkedClient.ProjectName);

                return linkedClient;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error linking client {ClientId} to project {ProjectId}", clientId, projectId);
                throw;
            }
        }

        public async Task<bool> UnlinkClientFromProjectAsync(int projectId, int clientId, int userId)
        {
            try
            {
                // Check if linked
                var isLinked = await _projectClientRepository.IsLinkedAsync(projectId, clientId);
                if (!isLinked)
                {
                    _logger.LogWarning("Client {ClientId} is not linked to project {ProjectId}", clientId, projectId);
                    return false;
                }

                // Unlink
                var result = await _projectClientRepository.UnlinkAsync(projectId, clientId, userId);

                if (result)
                {
                    _logger.LogInformation("Successfully unlinked client {ClientId} from project {ProjectId}", clientId, projectId);
                }

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error unlinking client {ClientId} from project {ProjectId}", clientId, projectId);
                throw;
            }
        }

        public async Task<IEnumerable<ProjectClientDetailResponse>> LinkMultipleClientsToProjectAsync(LinkMultipleClientsRequest request, int userId)
        {
            try
            {
                // Validate that project exists
                var project = await _projectRepository.GetByIdAsync(request.ProjectId);
                if (project == null)
                {
                    throw new InvalidOperationException($"Project with ID {request.ProjectId} not found");
                }

                // Link each client to the project
                var linkedClients = new List<ProjectClientDetailResponse>();
                foreach (var clientId in request.ClientIds)
                {
                    try
                    {
                        var linkedClient = await LinkClientToProjectAsync(request.ProjectId, clientId, userId);
                        linkedClients.Add(linkedClient);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error linking client {ClientId} to project {ProjectId}", clientId, request.ProjectId);
                        // Continue with other clients even if one fails
                    }
                }

                _logger.LogInformation("Successfully linked {Count} clients to project {ProjectId}", linkedClients.Count, request.ProjectId);
                return linkedClients;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error linking multiple clients to project {ProjectId}", request.ProjectId);
                throw;
            }
        }

        public async Task<IEnumerable<ProjectClientDetailResponse>> LinkClientToMultipleProjectsAsync(LinkClientToMultipleProjectsRequest request, int userId)
        {
            try
            {
                // Validate that client exists
                var client = await _clientRepository.GetByIdAsync(request.ClientId);
                if (client == null)
                {
                    throw new InvalidOperationException($"Client with ID {request.ClientId} not found");
                }

                // Link the client to each project
                var linkedProjects = new List<ProjectClientDetailResponse>();
                foreach (var projectId in request.ProjectIds)
                {
                    try
                    {
                        var linkedProject = await LinkClientToProjectAsync(projectId, request.ClientId, userId);
                        linkedProjects.Add(linkedProject);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error linking client {ClientId} to project {ProjectId}", request.ClientId, projectId);
                        // Continue with other projects even if one fails
                    }
                }

                _logger.LogInformation("Successfully linked client {ClientId} to {Count} projects", request.ClientId, linkedProjects.Count);
                return linkedProjects;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error linking client {ClientId} to multiple projects", request.ClientId);
                throw;
            }
        }

        public async Task<IEnumerable<ProjectClientDetailResponse>> GetClientsByProjectAsync(int projectId)
        {
            try
            {
                return await _projectClientRepository.GetClientsByProjectAsync(projectId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving clients for project {ProjectId}", projectId);
                throw;
            }
        }

        public async Task<IEnumerable<ProjectClientDetailResponse>> GetProjectsByClientAsync(int clientId)
        {
            try
            {
                return await _projectClientRepository.GetProjectsByClientAsync(clientId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving projects for client {ClientId}", clientId);
                throw;
            }
        }

        public async Task<bool> IsLinkedAsync(int projectId, int clientId)
        {
            try
            {
                return await _projectClientRepository.IsLinkedAsync(projectId, clientId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking if project {ProjectId} is linked to client {ClientId}", projectId, clientId);
                throw;
            }
        }
    }
}
