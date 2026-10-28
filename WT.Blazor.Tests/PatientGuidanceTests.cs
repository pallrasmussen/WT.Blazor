using Bunit;
using WT.Blazor.Components.Shared;
using WT.Blazor.Models;
using WT.Blazor.Tests.TestInfrastructure;
using Xunit;

namespace WT.Blazor.Tests;

public class PatientGuidanceTests : BunitContext
{
    [Fact]
    public void ExpiredEstimatesCannotBeUsedInPublicHospitalComparisons()
    {
        var now = new DateTime(2026, 10, 28);
        var first = new WaitingTime { Id = 1, Name = "MRI", Hospital = Hospital.Klaksvikar, ComparisonCode = "MRI", Note = "Adults", ReferralEndpoint = ReferralEndpoint.Investigation, VerifiedAt = now, WaitValue = 6, WaitUpperValue = 10, Unit = WaitUnit.Vikur };
        var second = new WaitingTime { Id = 2, Name = "MRI", Hospital = Hospital.Suduroyar, ComparisonCode = "MRI", Note = "Adults", ReferralEndpoint = ReferralEndpoint.Investigation, VerifiedAt = now.AddMonths(-12), WaitValue = 42, Unit = WaitUnit.Dagar };
        var cut = Render<HospitalComparisons>(parameters => parameters.Add(p => p.Items, new[] { first, second }).Add(p => p.Settings, new PortalSettings()).Add(p => p.Now, now));
        Assert.Empty(cut.FindAll(".comparison-group"));
        second.VerifiedAt = now;
        cut.Render(parameters => parameters.Add(p => p.Items, new[] { first, second }).Add(p => p.Settings, new PortalSettings()).Add(p => p.Now, now));
        Assert.Single(cut.FindAll(".comparison-group"));
        Assert.Contains("42–70", cut.Markup);
    }

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
