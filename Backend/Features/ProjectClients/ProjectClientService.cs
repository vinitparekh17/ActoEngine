using ActoEngine.WebApi.Models;
using ActoEngine.WebApi.Repositories;
using ActoEngine.WebApi.Features.Clients;

namespace ActoEngine.WebApi.Services.ProjectClientService
{
    public interface IProjectClientService
    {
        /// <summary>
        /// Link a client to a project, creating the association if it does not already exist.
        /// </summary>
        /// <param name="projectId">The identifier of the project to link the client to.</param>
        /// <param name="clientId">The identifier of the client to link to the project.</param>
        /// <param name="userId">The identifier of the user performing the linking operation.</param>
        /// <returns>The ProjectClientDetailResponse describing the client-project association.</returns>
        /// <exception cref="InvalidOperationException">Thrown when the specified project or client does not exist.</exception>
        Task<ProjectClientDetailResponse> LinkClientToProjectAsync(int projectId, int clientId, int userId);
        /// <summary>
        /// Unlinks a client from a project.
        /// </summary>
        /// <param name="projectId">The identifier of the project.</param>
        /// <param name="clientId">The identifier of the client.</param>
        /// <param name="userId">The identifier of the user performing the operation.</param>
        /// <returns>`true` if the client was unlinked from the project; `false` if the client was not linked.</returns>
        Task<bool> UnlinkClientFromProjectAsync(int projectId, int clientId, int userId);
        /// <summary>
        /// Links the specified clients to a single project and returns details for each successful link.
        /// </summary>
        /// <param name="request">Request containing the target project ID and the collection of client IDs to link.</param>
        /// <param name="userId">Identifier of the user performing the operation.</param>
        /// <returns>A collection of ProjectClientDetailResponse entries for each client successfully linked to the project.</returns>
        Task<IEnumerable<ProjectClientDetailResponse>> LinkMultipleClientsToProjectAsync(LinkMultipleClientsRequest request, int userId);
        /// <summary>
        /// Links the specified client to each project listed in the request.
        /// </summary>
        /// <param name="request">Request containing the target client ID and the list of project IDs to link.</param>
        /// <param name="userId">ID of the user performing the operation.</param>
        /// <returns>A collection of ProjectClientDetailResponse entries for each successful link.</returns>
        /// <exception cref="InvalidOperationException">Thrown when the specified client does not exist.</exception>
        Task<IEnumerable<ProjectClientDetailResponse>> LinkClientToMultipleProjectsAsync(LinkClientToMultipleProjectsRequest request, int userId);
        /// <summary>
        /// Retrieves the clients that are linked to the specified project.
        /// </summary>
        /// <param name="projectId">The identifier of the project to query.</param>
        /// <returns>A collection of ProjectClientDetailResponse objects representing clients linked to the project.</returns>
        Task<IEnumerable<ProjectClientDetailResponse>> GetClientsByProjectAsync(int projectId);
        /// <summary>
        /// Retrieves all projects linked to the specified client.
        /// </summary>
        /// <param name="clientId">The identifier of the client whose linked projects to retrieve.</param>
        /// <returns>A collection of project-client detail responses representing projects linked to the client.</returns>
        Task<IEnumerable<ProjectClientDetailResponse>> GetProjectsByClientAsync(int clientId);
        /// <summary>
        /// Determines whether the specified client is linked to the specified project.
        /// </summary>
        /// <returns>`true` if the client is linked to the project, `false` otherwise.</returns>
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

        /// <summary>
        /// Links a client to a project and returns the resulting association details.
        /// </summary>
        /// <param name="projectId">ID of the project to link the client to.</param>
        /// <param name="clientId">ID of the client to link to the project.</param>
        /// <param name="userId">ID of the user performing the operation.</param>
        /// <returns>The ProjectClientDetailResponse representing the linked client and project.</returns>
        /// <exception cref="InvalidOperationException">Thrown if the project or client does not exist, or if the linked record cannot be retrieved after linking.</exception>
        public async Task<ProjectClientDetailResponse> LinkClientToProjectAsync(int projectId, int clientId, int userId)
        {
            try
            {
                // Validate that project exists
                var project = await _projectRepository.GetByIdAsync(projectId) ?? throw new InvalidOperationException($"Project with ID {projectId} not found");

                // Validate that client exists
                var client = await _clientRepository.GetByIdAsync(clientId) ?? throw new InvalidOperationException($"Client with ID {clientId} not found");

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
                var linkedClient = linkedClients.FirstOrDefault(l => l.ClientId == clientId) ?? throw new InvalidOperationException($"Failed to retrieve linked client {clientId} for project {projectId}");
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

        /// <summary>
        /// Unlinks the specified client from the specified project if a link exists.
        /// </summary>
        /// <param name="projectId">The ID of the project.</param>
        /// <param name="clientId">The ID of the client to unlink from the project.</param>
        /// <param name="userId">The ID of the user performing the unlink operation.</param>
        /// <returns>`true` if the link was removed, `false` otherwise.</returns>
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

        /// <summary>
        /// Links multiple clients to a single project.
        /// Attempts to link each client ID from the request and returns details only for clients that were successfully linked; individual failures are logged and skipped.
        /// </summary>
        /// <param name="request">Request containing the target ProjectId and the collection of ClientIds to link.</param>
        /// <param name="userId">Identifier of the user performing the linking operation.</param>
        /// <returns>A collection of ProjectClientDetailResponse objects for clients that were successfully linked to the project.</returns>
        /// <exception cref="InvalidOperationException">Thrown when the target project specified by the request does not exist.</exception>
        public async Task<IEnumerable<ProjectClientDetailResponse>> LinkMultipleClientsToProjectAsync(LinkMultipleClientsRequest request, int userId)
        {
            try
            {
                // Validate that project exists
                var project = await _projectRepository.GetByIdAsync(request.ProjectId) ?? throw new InvalidOperationException($"Project with ID {request.ProjectId} not found");

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

        /// <summary>
        /// Links a single client to multiple projects specified in the request.
        /// </summary>
        /// <param name="request">Request containing the target ClientId and the list of ProjectIds to link the client to.</param>
        /// <param name="userId">ID of the user performing the linking operation.</param>
        /// <returns>An enumerable of ProjectClientDetailResponse objects for each project the client was successfully linked to.</returns>
        public async Task<IEnumerable<ProjectClientDetailResponse>> LinkClientToMultipleProjectsAsync(LinkClientToMultipleProjectsRequest request, int userId)
        {
            try
            {
                // Validate that client exists
                var client = await _clientRepository.GetByIdAsync(request.ClientId) ?? throw new InvalidOperationException($"Client with ID {request.ClientId} not found");

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

        /// <summary>
        /// Retrieves the clients linked to the specified project.
        /// </summary>
        /// <param name="projectId">The identifier of the project to retrieve linked clients for.</param>
        /// <returns>A collection of ProjectClientDetailResponse representing clients linked to the specified project.</returns>
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

        /// <summary>
        /// Retrieves the projects associated with the specified client.
        /// </summary>
        /// <param name="clientId">The identifier of the client whose linked projects to retrieve.</param>
        /// <returns>A collection of <see cref="ProjectClientDetailResponse"/> entries representing projects linked to the client.</returns>
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

        /// <summary>
        /// Determines whether the specified client is linked to the specified project.
        /// </summary>
        /// <param name="projectId">The identifier of the project to check.</param>
        /// <param name="clientId">The identifier of the client to check.</param>
        /// <returns>`true` if the client is linked to the project, `false` otherwise.</returns>
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