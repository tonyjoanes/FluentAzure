namespace FluentAzure.Core;

/// <summary>
/// A configuration source that caches its values and can be asked to fetch them again,
/// e.g. to pick up rotated Key Vault secrets or changed environment variables.
/// Sources that re-read on every <see cref="IConfigurationSource.LoadAsync"/> call do not need this.
/// </summary>
public interface IReloadableConfigurationSource : IConfigurationSource
{
    /// <summary>
    /// Discards any cached values and loads them again from the underlying store.
    /// </summary>
    /// <returns>A task that represents the asynchronous reload operation. The task result contains the reloaded configuration values.</returns>
    Task<Result<Dictionary<string, string>>> ReloadAsync();
}
