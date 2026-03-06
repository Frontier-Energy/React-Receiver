using React_Receiver.Models;
using React_Receiver.Services;
using Azure.Storage.Blobs;
using Azure.Data.Tables;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace React_Receiver.Tests;

public sealed class FormSchemaEntityTests
{
    [Fact]
    public void FromResponse_StoresBlobReference_WhenBlobNameProvided()
    {
        var entity = FormSchemaEntity.FromResponse("safety-checklist", "form-schemas/safety-checklist.json");

        Assert.Equal("form-schemas/safety-checklist.json", entity.SchemaBlobName);
        Assert.Equal(FormSchemaEntity.EmptySectionsJson, entity.SectionsJson);
        Assert.Equal(string.Empty, entity.FormName);
        Assert.True(entity.HasSchemaBlob);
        Assert.False(entity.HasInlineSections);
    }

    [Fact]
    public void ToResponse_ReadsLegacyInlineSections()
    {
        var entity = new FormSchemaEntity
        {
            FormName = "Legacy Schema",
            SectionsJson =
                "[{\"Title\":\"General\",\"Fields\":[{\"Id\":\"field-1\",\"Label\":\"Field 1\",\"Type\":\"text\",\"Required\":true}]}]"
        };

        var response = entity.ToResponse();

        Assert.Equal("Legacy Schema", response.FormName);
        Assert.Single(response.Sections);
        Assert.Single(response.Sections[0].Fields);
    }

    [Fact]
    public async Task ReadSchemaAsync_FallsBackToInlineSections_WhenBlobStorageIsNotConfigured()
    {
        var service = new FormSchemaService(
            new BlobServiceClient("UseDevelopmentStorage=true"),
            new TableServiceClient("UseDevelopmentStorage=true"),
            new FileBootstrapDataProvider(),
            NullLogger<FormSchemaService>.Instance,
            Options.Create(new BlobStorageOptions()),
            Options.Create(new TableStorageOptions()));
        var entity = new FormSchemaEntity
        {
            RowKey = "legacy-hvac",
            FormName = "Legacy Schema",
            SchemaBlobName = "form-schemas/legacy-hvac.json",
            SectionsJson =
                "[{\"Title\":\"General\",\"Fields\":[{\"Id\":\"field-1\",\"Label\":\"Field 1\",\"Type\":\"text\",\"Required\":true}]}]"
        };

        var response = await service.ReadSchemaAsync(entity, CancellationToken.None);

        Assert.Equal("Legacy Schema", response.FormName);
        Assert.Single(response.Sections);
    }
}
