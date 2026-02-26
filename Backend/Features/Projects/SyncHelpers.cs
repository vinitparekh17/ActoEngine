using ActoEngine.WebApi.Features.LogicalFk;
using ActoEngine.WebApi.Infrastructure.Database;
using Microsoft.Extensions.Logging;

namespace ActoEngine.WebApi.Features.Projects
{
    public static class SyncHelpers
    {
        public static async Task PerformFaultTolerantLogicalFkDetectionAsync(
            int projectId,
            IProjectRepository projectRepository,
            ILogicalFkService logicalFkService,
            ILogger logger,
            CancellationToken cancellationToken = default)
        {
            try
            {
                await projectRepository.UpdateSyncStatusAsync(projectId, "Detecting logical FKs...", 97, cancellationToken);
                await logicalFkService.DetectAndPersistCandidatesAsync(projectId, cancellationToken);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Logical FK detection failed for project {ProjectId}. Sync continues.", projectId);
            }
        }
    }
}
