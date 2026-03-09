using System;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using React_Receiver.Application.FormSchemas;
using React_Receiver.Application.Translations;
using React_Receiver.Application.Users;
using React_Receiver.Controllers;
using React_Receiver.Models;
using React_Receiver.Tests.TestDoubles;
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
        var sections = Assert.IsType<FormSectionResponse[]>(response.Sections);
        Assert.False(string.IsNullOrWhiteSpace(response.FormName));
        Assert.NotEmpty(sections);
        Assert.Equal("\"hvac-v1\"", controller.Response.Headers.ETag.ToString());
    }

    [Fact]
    public async Task UpsertFormSchema_ReturnsOkForKnownFormType()
    {
        var controller = CreateFormSchemasController();

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
        Assert.Equal("/form-schemas/hvac", controller.Response.Headers.ContentLocation.ToString());
        Assert.False(string.IsNullOrWhiteSpace(controller.Response.Headers.ETag.ToString()));
        Assert.False(string.IsNullOrWhiteSpace(controller.Response.Headers["X-Form-Schema-Version"].ToString()));
    }

    [Fact]
    public async Task UpsertFormSchema_ReturnsCreatedForUnknownFormType()
    {
        var controller = CreateFormSchemasController();

        var schema = new FormSchemaResponse(
            FormName: "Custom",
            Sections: []);

        var result = await controller.UpsertFormSchema(new FormSchemaRouteRequest { FormType = "custom-form" }, schema);

        var created = Assert.IsType<CreatedAtActionResult>(result.Result);
        Assert.Equal(nameof(FormSchemasController.GetFormSchema), created.ActionName);
        Assert.Equal("custom-form", created.RouteValues?["formType"]);
        var response = Assert.IsType<FormSchemaResponse>(created.Value);
        Assert.Equal("Custom", response.FormName);
        Assert.False(string.IsNullOrWhiteSpace(controller.Response.Headers.ETag.ToString()));
        Assert.False(string.IsNullOrWhiteSpace(controller.Response.Headers["X-Form-Schema-Version"].ToString()));
    }

    [Fact]
    public async Task GetTranslations_ReturnsTranslationForLanguage()
    {
        var controller = CreateTranslationsController();

        var result = await controller.GetTranslations("es");

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<TranslationsResponse>(ok.Value);
        Assert.Equal("Espanol", response.LanguageName);
        var app = Assert.IsType<AppTranslations>(response.App);
        Assert.Equal("QControl", app.Brand);
        Assert.Equal("\"es-v1\"", controller.Response.Headers.ETag.ToString());
    }

    [Fact]
    public async Task GetTranslations_ReturnsNotFoundForUnknownLanguage()
    {
        var controller = CreateTranslationsController();

        var result = await controller.GetTranslations("fr");

        Assert.IsType<NotFoundResult>(result.Result);
    }

    [Fact]
    public async Task UpsertTranslations_ReturnsOkForKnownLanguage()
    {
        var controller = CreateTranslationsController();

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

        var result = await controller.UpsertTranslations("en", payload);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<TranslationsResponse>(ok.Value);
        var app = Assert.IsType<AppTranslations>(response.App);
        Assert.Equal("Updated Title", app.Title);
        Assert.Equal("/translations/en", controller.Response.Headers.ContentLocation.ToString());
        Assert.Equal("\"en-v2\"", controller.Response.Headers.ETag.ToString());
    }

    [Fact]
    public async Task UpsertTranslations_ReturnsCreatedForUnknownLanguage()
    {
        var controller = CreateTranslationsController();

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

        var result = await controller.UpsertTranslations("fr", payload);

        var created = Assert.IsType<CreatedAtActionResult>(result.Result);
        Assert.Equal(nameof(TranslationsController.GetTranslations), created.ActionName);
        Assert.Equal("fr", created.RouteValues?["language"]);
        var response = Assert.IsType<TranslationsResponse>(created.Value);
        Assert.Equal("French", response.LanguageName);
        Assert.Equal("\"fr-v1\"", controller.Response.Headers.ETag.ToString());
    }

    private static UsersController CreateUsersController()
    {
        var controller = new UsersController(new TestSender((request, _) =>
        {
            return request switch
            {
                GetCurrentUserQuery => Task.FromResult<object?>(new MeResponse(
                    UserId: "a1b2c3",
                    Roles: ["admin"],
                    Permissions: ["tenant.select", "customization.admin"])),
                GetUserQuery => throw new NotSupportedException(),
                _ => throw new NotSupportedException()
            };
        }), NullLogger<UsersController>.Instance)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };

        return controller;
    }

    private static FormSchemasController CreateFormSchemasController()
    {
        var controller = new FormSchemasController(new TestSender((request, _) =>
        {
            return request switch
            {
                ListFormSchemasQuery => Task.FromResult<object?>(new FormSchemaCatalogResponse(
                    [
                        new FormSchemaCatalogItemResponse("hvac", "2026-01-01", "\"hvac-v1\"")
                    ])),
                GetFormSchemaQuery getQuery when string.Equals(getQuery.FormType, "hvac", StringComparison.OrdinalIgnoreCase)
                    => Task.FromResult<object?>(new ResourceEnvelope<FormSchemaResponse>(
                        new FormSchemaResponse(
                            FormName: "HVAC Inspection",
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
                            ]),
                        "\"hvac-v1\"",
                        "2026-01-01")),
                GetFormSchemaQuery => Task.FromResult<object?>(null),
                UpsertFormSchemaCommand upsert when string.Equals(upsert.FormType, "custom-form", StringComparison.OrdinalIgnoreCase)
                    => Task.FromResult<object?>(new UpsertResult<FormSchemaResponse>(
                        upsert.Request,
                        true,
                        "2026-03-06",
                        "\"custom-form-v1\"")),
                UpsertFormSchemaCommand upsert
                    => Task.FromResult<object?>(new UpsertResult<FormSchemaResponse>(
                        upsert.Request,
                        false,
                        "2026-03-06",
                        "\"hvac-v2\"")),
                _ => throw new NotSupportedException()
            };
        }), NullLogger<FormSchemasController>.Instance)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };

        return controller;
    }

    private static TranslationsController CreateTranslationsController()
    {
        var controller = new TranslationsController(new TestSender((request, _) =>
        {
            return request switch
            {
                GetTranslationsQuery getQuery when string.Equals(getQuery.Language, "es", StringComparison.OrdinalIgnoreCase)
                    => Task.FromResult<object?>(new ResourceEnvelope<TranslationsResponse>(
                        new TranslationsResponse
                        {
                            LanguageName = "Espanol",
                            App = new AppTranslations
                            {
                                Brand = "QControl"
                            }
                        },
                        "\"es-v1\"")),
                GetTranslationsQuery => Task.FromResult<object?>(null),
                UpsertTranslationsCommand upsert when string.Equals(upsert.Language, "fr", StringComparison.OrdinalIgnoreCase)
                    => Task.FromResult<object?>(new UpsertResult<TranslationsResponse>(upsert.Request, true, ETag: "\"fr-v1\"")),
                UpsertTranslationsCommand upsert
                    => Task.FromResult<object?>(new UpsertResult<TranslationsResponse>(upsert.Request, false, ETag: "\"en-v2\"")),
                _ => throw new NotSupportedException()
            };
        }), NullLogger<TranslationsController>.Instance)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };

        return controller;
    }
}
