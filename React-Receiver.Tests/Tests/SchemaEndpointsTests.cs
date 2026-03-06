using System;
using Azure.Data.Tables;
using Azure.Storage.Blobs;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using React_Receiver.Controllers;
using React_Receiver.Handlers;
using React_Receiver.Models;
using React_Receiver.Services;
using Xunit;

namespace React_Receiver.Tests;

public sealed class SchemaEndpointsTests
{
    [Fact]
    public async Task GetCurrentUser_ReturnsMePayload()
    {
        var controller = CreateController();

        var result = await controller.GetCurrentUser();

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<MeResponse>(ok.Value);
        Assert.Equal("a1b2c3", response.UserId);
        Assert.Equal(["admin"], response.Roles);
        Assert.Equal(["tenant.select", "customization.admin"], response.Permissions);
    }

    [Fact]
    public async Task ListFormSchemas_ReturnsCatalog()
    {
        var controller = CreateController();

        var result = await controller.ListFormSchemas();

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<FormSchemaCatalogResponse>(ok.Value);
        Assert.NotEmpty(response.Items);
        Assert.Contains(response.Items, item => item.FormType == "hvac");
    }

    [Fact]
    public async Task GetFormSchema_ReturnsNotFoundForUnknownFormType()
    {
        var controller = CreateController();

        var result = await controller.GetFormSchema("unknown-form");

        Assert.IsType<NotFoundResult>(result.Result);
    }

    [Fact]
    public async Task GetFormSchema_ReturnsSchemaForKnownFormType()
    {
        var controller = CreateController();

        var result = await controller.GetFormSchema("hvac");

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<FormSchemaResponse>(ok.Value);
        Assert.False(string.IsNullOrWhiteSpace(response.FormName));
        Assert.NotEmpty(response.Sections);
    }

    [Fact]
    public async Task UpsertFormSchema_ReturnsOkForKnownFormType()
    {
        var controller = CreateController(new TableStorageOptions
        {
            ConnectionString = string.Empty
        });

        var schema = new FormSchemaResponse(
            FormName: "HVAC Inspection Updated",
            Sections:
            [
                new FormSectionResponse(
                    Title: "Equipment",
                    Fields:
                    [
                        new FormFieldResponse(
                            Id: "unitLocation",
                            Label: "Unit Location",
                            Type: "text",
                            Required: true)
                    ])
            ]);

        var result = await controller.UpsertFormSchema("hvac", schema);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<FormSchemaResponse>(ok.Value);
        Assert.Equal("HVAC Inspection Updated", response.FormName);
    }

    [Fact]
    public async Task UpsertFormSchema_ReturnsNotFoundForUnknownFormType()
    {
        var controller = CreateController(new TableStorageOptions
        {
            ConnectionString = string.Empty
        });

        var schema = new FormSchemaResponse(
            FormName: "Custom",
            Sections: []);

        var result = await controller.UpsertFormSchema("custom-form", schema);

        Assert.IsType<NotFoundResult>(result.Result);
    }

    [Fact]
    public async Task GetTranslations_ReturnsTranslationForLanguage()
    {
        var controller = CreateController();

        var result = await controller.GetTranslations("es");

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<TranslationsResponse>(ok.Value);
        Assert.Equal("Espanol", response.LanguageName);
        Assert.Equal("QControl", response.App.Brand);
    }

    [Fact]
    public async Task GetTranslations_ReturnsNotFoundForUnknownLanguage()
    {
        var controller = CreateController();

        var result = await controller.GetTranslations("fr");

        Assert.IsType<NotFoundResult>(result.Result);
    }

    [Fact]
    public async Task UpsertTranslations_ReturnsOkForKnownLanguage()
    {
        var controller = CreateController(new TableStorageOptions
        {
            ConnectionString = string.Empty
        });

        var payload = new TranslationsResponse(
            LanguageName: "English",
            App: new TranslationAppResponse(
                Title: "Updated Title",
                PoweredBy: "Powered By",
                Brand: "QControl"));

        var result = await controller.UpsertTranslations("en", payload);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<TranslationsResponse>(ok.Value);
        Assert.Equal("Updated Title", response.App.Title);
    }

    [Fact]
    public async Task UpsertTranslations_ReturnsNotFoundForUnknownLanguage()
    {
        var controller = CreateController(new TableStorageOptions
        {
            ConnectionString = string.Empty
        });

        var payload = new TranslationsResponse(
            LanguageName: "French",
            App: new TranslationAppResponse(
                Title: "Titre",
                PoweredBy: "Propulse par",
                Brand: "QControl"));

        var result = await controller.UpsertTranslations("fr", payload);

        Assert.IsType<NotFoundResult>(result.Result);
    }

    private static QHVACController CreateController(TableStorageOptions? tableOptions = null)
    {
        var controller = new QHVACController(
            new FakeInspectionRequestHandler(),
            new FakeLoginRequestHandler(),
            new ReceiveInspectionRequestParser(),
            new FakeRegisterRequestHandler(),
            new FakeTenantConfigHandler(),
            new BlobServiceClient("UseDevelopmentStorage=true"),
            new TableServiceClient("UseDevelopmentStorage=true"),
            Options.Create(new BlobStorageOptions()),
            Options.Create(tableOptions ?? new TableStorageOptions()))
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };

        return controller;
    }

    private sealed class FakeInspectionRequestHandler : IInspectionRequestHandler
    {
        public Task<ReceiveInspectionResponse> SaveRequestAsync(
            ReceiveInspectionRequest request,
            CancellationToken cancellationToken)
        {
            var response = new ReceiveInspectionResponse(
                Status: "Received",
                SessionId: request.SessionId ?? string.Empty,
                Name: request.Name ?? string.Empty,
                QueryParams: request.QueryParams ?? new Dictionary<string, string>(),
                Message: "OK");
            return Task.FromResult(response);
        }
    }

    private sealed class FakeLoginRequestHandler : ILoginRequestHandler
    {
        public LoginRequestResponse HandleLogin(LoginRequestCommand request)
        {
            return new LoginRequestResponse(UserId: Guid.NewGuid().ToString("N"));
        }
    }

    private sealed class FakeRegisterRequestHandler : IRegisterRequestHandler
    {
        public Task<string> HandleRegisterAsync(
            RegisterRequestModel request,
            string userId,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(userId);
        }
    }

    private sealed class FakeTenantConfigHandler : ITenantConfigHandler
    {
        public Task<TenantBootstrapResponse> GetTenantConfigAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult(new TenantBootstrapResponse(
                TenantId: "qhvac",
                DisplayName: "QHVAC",
                UiDefaults: new UiDefaults(
                    Theme: "harbor",
                    Font: "Tahoma, \"Trebuchet MS\", Arial, sans-serif",
                    Language: "en",
                    ShowLeftFlyout: true,
                    ShowRightFlyout: true,
                    ShowInspectionStatsButton: false),
                EnabledForms: ["electrical", "electrical-sf", "hvac"],
                LoginRequired: true));
        }

        public Task<TenantBootstrapResponse> UpsertTenantConfigAsync(
            TenantBootstrapResponse tenantConfig,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(tenantConfig);
        }
    }
}
