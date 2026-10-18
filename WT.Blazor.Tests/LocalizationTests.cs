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
        }
        finally
        {
            CultureInfo.CurrentUICulture = originalCulture;
        }
    }
}