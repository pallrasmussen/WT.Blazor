using WT.Blazor.Models;
using Xunit;

namespace WT.Blazor.Tests;

public class FieldLimitTests
{
    [Theory]
    [InlineData(nameof(WaitingTimeInput.Name), FieldLimits.WaitingTimeName, WaitingTimeInputError.NameTooLong)]
    [InlineData(nameof(WaitingTimeInput.ResponsibleDepartment), FieldLimits.ResponsibleDepartment, WaitingTimeInputError.OwnerTooLong)]
    [InlineData(nameof(WaitingTimeInput.ComparisonCode), FieldLimits.ComparisonCode, WaitingTimeInputError.ComparisonCodeTooLong)]
    [InlineData(nameof(WaitingTimeInput.Note), FieldLimits.Note, WaitingTimeInputError.NoteTooLong)]
    public void EstimateTextAcceptsTheLimitAndRejectsOneMoreCharacter(string field, int maximum, WaitingTimeInputError expected)
    {
        WaitingTimeInput Input(int length)
        {
            var text = new string('x', length);
            var input = new WaitingTimeInput { Name = "MRI", Hospital = Hospital.Klaksvikar, Unit = WaitUnit.Vikur };
            return field switch
            {
                nameof(input.Name) => input with { Name = text },
                nameof(input.ResponsibleDepartment) => input with { ResponsibleDepartment = text },
                nameof(input.ComparisonCode) => input with { ComparisonCode = text },
                _ => input with { Note = text }
            };
        }
        Assert.Null(WaitingTimeValidation.Validate(Input(maximum)));
        var error = WaitingTimeValidation.Validate(Input(maximum + 1));
        Assert.NotNull(error);
        Assert.Equal(expected, error.Error);
        Assert.Equal(maximum, error.Limit);
    }

    [Fact]
    public void PublishedGuidanceAcceptsTheLimitAndRejectsOneMoreCharacter()
    {
        var text = new string('x', FieldLimits.PatientGuidance);
        var settings = new PortalSettings
        {
            PublishPatientGuidance = true,
            ReferralInstructionsFo = text,
            ReferralInstructionsEn = text,
            ContactGuidanceFo = text,
            ContactGuidanceEn = text
        };
        Assert.Null(PortalSettingsValidation.Validate(settings));
        settings.ContactGuidanceEn += "x";
        Assert.Equal(PortalSettingsError.Guidance, PortalSettingsValidation.Validate(settings));
    }
}
