namespace ActoX.Domain.Interfaces;

public interface IDataSeeder
{
    /// <summary>
    /// Seeds all necessary data for the application, including shared data and admin user.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token to cancel the operation.</param>
    /// <returns>A task representing the asynchronous seeding operation.</returns>
    Task SeedAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Seeds development-specific data (not used in this implementation).
    /// </summary>
    /// <param name="cancellationToken">Cancellation token to cancel the operation.</param>
    /// <returns>A task representing the asynchronous seeding operation.</returns>
    Task SeedDevelopmentDataAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Seeds production-specific data (not used in this implementation).
    /// </summary>
    /// <param name="cancellationToken">Cancellation token to cancel the operation.</param>
    /// <returns>A task representing the asynchronous seeding operation.</returns>
    Task SeedProductionDataAsync(CancellationToken cancellationToken = default);
}