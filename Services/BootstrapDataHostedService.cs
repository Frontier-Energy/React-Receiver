using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using React_Receiver.Application.FormSchemas;
using React_Receiver.Application.TenantConfig;
using React_Receiver.Application.Translations;

namespace React_Receiver.Services;

public sealed class BootstrapDataHostedService : IHostedService
{
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly BootstrapDataOptions _options;

    public BootstrapDataHostedService(
        IServiceScopeFactory serviceScopeFactory,
        IOptions<BootstrapDataOptions> options)
    {
        _serviceScopeFactory = serviceScopeFactory;
        _options = options.Value;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (!_options.SeedOnStartup)
        {
            return;
        }

        using var scope = _serviceScopeFactory.CreateScope();
        var formSchemaService = scope.ServiceProvider.GetRequiredService<IFormSchemaApplicationService>();
        var translationService = scope.ServiceProvider.GetRequiredService<ITranslationApplicationService>();
        var tenantConfigService = scope.ServiceProvider.GetRequiredService<ITenantConfigApplicationService>();

        await formSchemaService.ImportSeedDataAsync(_options.OverwriteExisting, cancellationToken);
        await translationService.ImportSeedDataAsync(_options.OverwriteExisting, cancellationToken);
        await tenantConfigService.ImportSeedDataAsync(_options.OverwriteExisting, cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}
