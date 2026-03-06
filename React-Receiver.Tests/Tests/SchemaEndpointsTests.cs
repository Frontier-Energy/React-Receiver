using System;
using Azure.Data.Tables;
using Azure.Storage.Blobs;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using React_Receiver.Controllers;
using React_Receiver.Models;
using React_Receiver.Services;
using Xunit;

namespace React_Receiver.Tests;

public sealed class SchemaEndpointsTests
{
    [Fact]
    public async Task GetCurrentUser_ReturnsMePayload()
    {
        var controller = CreateUsersController();

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
        var controller = CreateFormSchemasController();

        var result = await controller.ListFormSchemas();

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<FormSchemaCatalogResponse>(ok.Value);
        Assert.NotEmpty(response.Items);
        Assert.Contains(response.Items, item => item.FormType == "hvac");
    }

    [Fact]
    public async Task GetFormSchema_ReturnsNotFoundForUnknownFormType()
    {
        var controller = CreateFormSchemasController();

        var result = await controller.GetFormSchema(new FormSchemaRouteRequest { FormType = "unknown-form" });

        Assert.IsType<NotFoundResult>(result.Result);
    }

    [Fact]
    public async Task GetFormSchema_ReturnsSchemaForKnownFormType()
    {
        var controller = CreateFormSchemasController();

        var result = await controller.GetFormSchema(new FormSchemaRouteRequest { FormType = "hvac" });

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<FormSchemaResponse>(ok.Value);
        Assert.False(string.IsNullOrWhiteSpace(response.FormName));
        Assert.NotEmpty(response.Sections);
    }

    [Fact]
    public async Task GetFormSchema_ReturnsInternalServerErrorWhenBlobContentIsMissing()
    {
        var controller = new FormSchemasController(
            new ThrowingFormSchemaService(new FormSchemaBlobContentException("missing blob")),
            NullLogger<FormSchemasController>.Instance)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };

        var result = await controller.GetFormSchema(new FormSchemaRouteRequest { FormType = "hvac" });

        var objectResult = Assert.IsType<ObjectResult>(result.Result);
        Assert.Equal(StatusCodes.Status500InternalServerError, objectResult.StatusCode);
    }

    [Fact]
    public async Task UpsertFormSchema_ReturnsOkForKnownFormType()
    {
        var controller = CreateFormSchemasController(new TableStorageOptions
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

        var result = await controller.UpsertFormSchema(new FormSchemaRouteRequest { FormType = "hvac" }, schema);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<FormSchemaResponse>(ok.Value);
        Assert.Equal("HVAC Inspection Updated", response.FormName);
    }

    [Fact]
    public async Task UpsertFormSchema_ReturnsNotFoundForUnknownFormType()
    {
        var controller = CreateFormSchemasController(new TableStorageOptions
        {
            ConnectionString = string.Empty
        });

        var schema = new FormSchemaResponse(
            FormName: "Custom",
            Sections: []);

        var result = await controller.UpsertFormSchema(new FormSchemaRouteRequest { FormType = "custom-form" }, schema);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<FormSchemaResponse>(ok.Value);
        Assert.Equal("Custom", response.FormName);
    }

    [Fact]
    public async Task GetTranslations_ReturnsTranslationForLanguage()
    {
        var controller = CreateTranslationsController();

        var result = await controller.GetTranslations(new TranslationLanguageRequest { Language = "es" });

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<TranslationsResponse>(ok.Value);
        Assert.Equal("Espanol", response.LanguageName);
        var app = Assert.IsType<AppTranslations>(response.App);
        Assert.Equal("QControl", app.Brand);
    }

    [Fact]
    public async Task GetTranslations_ReturnsNotFoundForUnknownLanguage()
    {
        var controller = CreateTranslationsController();

        var result = await controller.GetTranslations(new TranslationLanguageRequest { Language = "fr" });

        Assert.IsType<NotFoundResult>(result.Result);
    }

    [Fact]
    public async Task UpsertTranslations_ReturnsOkForKnownLanguage()
    {
        var controller = CreateTranslationsController(new TableStorageOptions
        {
            ConnectionString = string.Empty
        });

        var payload = new TranslationsResponse
        {
            LanguageName = "English",
            App = new AppTranslations
            {
                Title = "Updated Title",
                PoweredBy = "Powered By",
                Brand = "QControl"
            }
        };

        var result = await controller.UpsertTranslations(new TranslationLanguageRequest { Language = "en" }, payload);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<TranslationsResponse>(ok.Value);
        var app = Assert.IsType<AppTranslations>(response.App);
        Assert.Equal("Updated Title", app.Title);
    }

    [Fact]
    public async Task UpsertTranslations_ReturnsNotFoundForUnknownLanguage()
    {
        var controller = CreateTranslationsController(new TableStorageOptions
        {
            ConnectionString = string.Empty
        });

        var payload = new TranslationsResponse
        {
            LanguageName = "French",
            App = new AppTranslations
            {
                Title = "Titre",
                PoweredBy = "Propulse par",
                Brand = "QControl"
            }
        };

        var result = await controller.UpsertTranslations(new TranslationLanguageRequest { Language = "fr" }, payload);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<TranslationsResponse>(ok.Value);
        Assert.Equal("French", response.LanguageName);
    }

    private static UsersController CreateUsersController()
    {
        var controller = new UsersController(new FakeUserQueryService())
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };

        return controller;
    }

    private static FormSchemasController CreateFormSchemasController(TableStorageOptions? tableOptions = null)
    {
        var controller = new FormSchemasController(
            new FormSchemaService(
                new BlobServiceClient("UseDevelopmentStorage=true"),
                new TableServiceClient("UseDevelopmentStorage=true"),
                new FileBootstrapDataProvider(),
                NullLogger<FormSchemaService>.Instance,
                Options.Create(new BlobStorageOptions()),
                Options.Create(tableOptions ?? new TableStorageOptions())),
            NullLogger<FormSchemasController>.Instance)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };

        return controller;
    }

    private static TranslationsController CreateTranslationsController(TableStorageOptions? tableOptions = null)
    {
        var controller = new TranslationsController(
            new TranslationService(
                new TableServiceClient("UseDevelopmentStorage=true"),
                new FileBootstrapDataProvider(),
                Options.Create(tableOptions ?? new TableStorageOptions())))
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };

        return controller;
    }

    private sealed class FakeUserQueryService : IUserQueryService
    {
        public Task<GetUserResponse?> GetUserAsync(
            string userId,
            CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public Task<MeResponse> GetCurrentUserAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult(new MeResponse(
                UserId: "a1b2c3",
                Roles: ["admin"],
                Permissions: ["tenant.select", "customization.admin"]));
        }
    }

    private sealed class ThrowingFormSchemaService : IFormSchemaService
    {
        private readonly Exception _exception;

        public ThrowingFormSchemaService(Exception exception)
        {
            _exception = exception;
        }

        public Task<FormSchemaCatalogResponse> ListAsync(CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public Task<FormSchemaResponse?> GetAsync(string formType, CancellationToken cancellationToken)
        {
            throw _exception;
        }

        public Task<FormSchemaResponse?> UpsertAsync(string formType, FormSchemaResponse request, CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public Task ImportSeedDataAsync(bool overwriteExisting, CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }
    }

}
