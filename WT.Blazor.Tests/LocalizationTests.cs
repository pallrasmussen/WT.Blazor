using System.Globalization;
using WT.Blazor.Localization;
using Xunit;

namespace WT.Blazor.Tests;

public class LocalizationTests
{
    [Fact]
    public void SettingsAuditUsesEnglishResourcesWhenEnglishIsSelected()
    {
        var originalCulture = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("en");

            Assert.Equal("Recent changes", AppText.Get("SettingsAudit"));
            Assert.Equal("Language", AppText.Get("Language"));
            Assert.Equal("Previous page", AppText.Get("PreviousPage"));
            Assert.Equal("Menu: History", string.Format(AppText.Get("NavigationMenuForPage"), "History"));
            Assert.Equal("Return to administration", AppText.Get("ReturnToAdministration"));
        }
        finally
        {
            CultureInfo.CurrentUICulture = originalCulture;
        }
    }

    [Fact]
    public void FaroeseTechnicalAndAdministrationTermsUseApprovedText()
    {
        var originalCulture = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("fo");

            Assert.Equal("Mál", AppText.Get("Language"));
            Assert.Equal("Request ID", AppText.Get("HistoryRequestId"));
            Assert.Equal("Broyt bíðitíð", AppText.Get("EditWaitingTimes"));
            Assert.Equal("Stovna bíðitíð", AppText.Get("AddWaitingTime"));
            Assert.Equal("Lat aftur valmynd", AppText.Get("CloseNavigationMenu"));
            Assert.Equal("Vís skipanastøðu", AppText.Get("ViewSystemStatus"));
        }
        finally
        {
            CultureInfo.CurrentUICulture = originalCulture;
        }
    }
}