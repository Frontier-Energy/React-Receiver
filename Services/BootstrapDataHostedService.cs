using Microsoft.Extensions.Options;
using React_Receiver.Handlers;

namespace React_Receiver.Services;

public sealed class BootstrapDataHostedService : IHostedService
{
    private readonly IFormSchemaService _formSchemaService;
    private readonly ITranslationService _translationService;
    private readonly ITenantConfigHandler _tenantConfigHandler;
    private readonly BootstrapDataOptions _options;

    public BootstrapDataHostedService(
        IFormSchemaService formSchemaService,
        ITranslationService translationService,
        ITenantConfigHandler tenantConfigHandler,
        IOptions<BootstrapDataOptions> options)
    {
        _formSchemaService = formSchemaService;
        _translationService = translationService;
        _tenantConfigHandler = tenantConfigHandler;
        _options = options.Value;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (!_options.SeedOnStartup)
        {
            return;
        }

        await _formSchemaService.ImportSeedDataAsync(_options.OverwriteExisting, cancellationToken);
        await _translationService.ImportSeedDataAsync(_options.OverwriteExisting, cancellationToken);
        await _tenantConfigHandler.ImportSeedDataAsync(_options.OverwriteExisting, cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}
