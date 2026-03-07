using React_Receiver.Application.Concurrency;
using React_Receiver.Application.FormSchemas;
using React_Receiver.Application.TenantConfig;
using React_Receiver.Application.Translations;
using React_Receiver.Domain.Tenants;
using React_Receiver.Models;
using React_Receiver.Services;
using Xunit;

namespace React_Receiver.Tests;

public sealed class OptimisticConcurrencySeedStoreTests
{
    [Fact]
    public void FormSchemaSeedStore_RequiresIfMatch_ForExistingSchema()
    {
        var store = new FormSchemaSeedStore(new FileBootstrapDataProvider());
        var updated = new FormSchemaResponse("Updated", []);

        var exception = Assert.Throws<PreconditionRequiredException>(() => store.Upsert("hvac", updated, null));

        Assert.Contains("If-Match", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void TranslationSeedStore_RejectsStaleIfMatch()
    {
        var store = new TranslationSeedStore(new FileBootstrapDataProvider());
        var updated = new TranslationsResponse
        {
            LanguageName = "English",
            App = new AppTranslations { Brand = "QControl" }
        };

        var exception = Assert.Throws<ConcurrencyConflictException>(() => store.Upsert("en", updated, "\"stale\""));

        Assert.Contains("did not match", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void TenantConfigSeedStore_AcceptsMatchingIfMatch()
    {
        var store = new TenantConfigSeedStore(new FileBootstrapDataProvider());
        var existing = store.Get("qhvac");
        Assert.NotNull(existing);

        var updated = new TenantConfiguration(
            "qhvac",
            "QHVAC Updated",
            new TenantUiDefaults("harbor", "Tahoma", "en", true, true, false),
            ["hvac"],
            true);

        var result = store.Upsert(updated, existing!.ETag);

        Assert.False(result.Created);
        Assert.Equal("QHVAC Updated", result.Resource.DisplayName);
        Assert.NotEqual(existing.ETag, result.ETag);
    }
}
