using Bunit;
using WT.Blazor.Components.Shared;
using WT.Blazor.Models;
using WT.Blazor.Tests.TestInfrastructure;
using Xunit;

namespace WT.Blazor.Tests;

public class PatientGuidanceTests : BunitContext
{
    [Theory]
    [InlineData("en", "EN referral", "EN contact")]
    [InlineData("fo", "FO referral", "FO contact")]
    public void OnlyPublishedGuidanceForTheSelectedLanguageIsShown(string culture, string referral, string contact)
    {
        using var selected = new TestCulture(culture);
        var settings = new PortalSettings
        {
            ReferralInstructionsEn = "EN referral",
            ContactGuidanceEn = "EN contact",
            ReferralInstructionsFo = "FO referral",
            ContactGuidanceFo = "FO contact"
        };
        var cut = Render<PatientNextSteps>(parameters => parameters.Add(p => p.Settings, settings));
        Assert.Empty(cut.FindAll(".approved-guidance"));
        settings.PublishPatientGuidance = true;
        cut.Render(parameters => parameters.Add(p => p.Settings, settings));
        Assert.Equal(new[] { referral, contact }, cut.FindAll(".approved-guidance").Select(p => p.TextContent));
        settings.ContactGuidanceEn = "<script>alert('bad')</script>";
        cut.Render(parameters => parameters.Add(p => p.Settings, settings));
        Assert.Empty(cut.FindAll("script"));
    }
}
