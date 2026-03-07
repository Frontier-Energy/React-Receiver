using Microsoft.Extensions.Options;
using React_Receiver.Application.FormSchemas;
using React_Receiver.Application.TenantConfig;
using React_Receiver.Application.Translations;

namespace React_Receiver.Services;

public sealed class BootstrapDataHostedService : IHostedService
{
    private readonly IFormSchemaApplicationService _formSchemaService;
    private readonly ITranslationApplicationService _translationService;
    private readonly ITenantConfigApplicationService _tenantConfigService;
    private readonly BootstrapDataOptions _options;

    public BootstrapDataHostedService(
        IFormSchemaApplicationService formSchemaService,
        ITranslationApplicationService translationService,
        ITenantConfigApplicationService tenantConfigService,
        IOptions<BootstrapDataOptions> options)
    {
        _formSchemaService = formSchemaService;
        _translationService = translationService;
        _tenantConfigService = tenantConfigService;
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
        await _tenantConfigService.ImportSeedDataAsync(_options.OverwriteExisting, cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}
