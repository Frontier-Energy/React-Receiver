using System.ComponentModel.DataAnnotations;
using React_Receiver.Models;
using Xunit;

namespace React_Receiver.Tests;

public sealed class RequestContractValidationTests
{
    [Fact]
    public void LoginRequestCommand_RequiresValidEmail()
    {
        var model = new LoginRequestCommand("not-an-email");

        var results = Validate(model);

        Assert.Contains(results, item => item.MemberNames.Contains(nameof(LoginRequestCommand.Email)));
    }

    [Fact]
    public void RegisterRequestModel_RequiresAllFields()
    {
        var model = new RegisterRequestModel(null, "A", null);

        var results = Validate(model);

        Assert.Contains(results, item => item.MemberNames.Contains(nameof(RegisterRequestModel.Email)));
        Assert.Contains(results, item => item.MemberNames.Contains(nameof(RegisterRequestModel.LastName)));
    }

    [Fact]
    public void GetInspectionFileRequest_RequiresSessionIdAndFileName()
    {
        var model = new GetInspectionFileRequest();

        var results = Validate(model);

        Assert.Contains(results, item => item.MemberNames.Contains(nameof(GetInspectionFileRequest.SessionId)));
        Assert.Contains(results, item => item.MemberNames.Contains(nameof(GetInspectionFileRequest.FileName)));
    }

    [Fact]
    public void TenantBootstrapResponse_RequiresNestedUiDefaults()
    {
        var model = new TenantBootstrapResponse(
            TenantId: "tenant-1",
            DisplayName: "Tenant",
            UiDefaults: new UiDefaults(
                Theme: null,
                Font: "Tahoma",
                Language: null,
                ShowLeftFlyout: true,
                ShowRightFlyout: true,
                ShowInspectionStatsButton: false),
            EnabledForms: ["hvac"],
            LoginRequired: true);

        var results = Validate(model.UiDefaults!);

        Assert.Contains(results, item => item.MemberNames.Contains(nameof(UiDefaults.Theme)));
        Assert.Contains(results, item => item.MemberNames.Contains(nameof(UiDefaults.Language)));
    }

    [Fact]
    public void TranslationsResponse_RequiresLanguageNameAndApp()
    {
        var model = new TranslationsResponse();

        var results = Validate(model);

        Assert.Contains(results, item => item.MemberNames.Contains(nameof(TranslationsResponse.LanguageName)));
        Assert.Contains(results, item => item.MemberNames.Contains(nameof(TranslationsResponse.App)));
    }

    private static IReadOnlyList<ValidationResult> Validate(object model)
    {
        var results = new List<ValidationResult>();
        var context = new ValidationContext(model);
        Validator.TryValidateObject(model, context, results, validateAllProperties: true);
        return results;
    }
}
