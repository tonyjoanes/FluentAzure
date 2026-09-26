using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

namespace FluentAzure.Guard;

/// <summary>
/// Runs the <see cref="ConfigurationGuard"/> when the host validates options on start, so misconfiguration
/// stops the app before it serves traffic, with every failure listed.
/// </summary>
internal sealed class ConfigurationGuardStartupValidator : IValidateOptions<ConfigurationGuardStartupCheck>
{
    private readonly ConfigurationGuard _guard;
    private readonly IConfiguration _configuration;

    public ConfigurationGuardStartupValidator(ConfigurationGuard guard, IConfiguration configuration)
    {
        _guard = guard;
        _configuration = configuration;
    }

    public ValidateOptionsResult Validate(string? name, ConfigurationGuardStartupCheck options)
    {
        var failures = _guard.Check(_configuration);
        return failures.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);
    }
}
