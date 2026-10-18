using System.Globalization;

namespace WT.Blazor.Localization;
using System.Resources;

public static class AppText
{
    private static bool English => CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "en";

    private static readonly ResourceManager Resources = new("WT.Blazor.Localization.AppText", typeof(AppText).Assembly);

    public static string Get(string key) => Resources.GetString(key, CultureInfo.CurrentUICulture) ?? key;
    public static string Get(AppTextKey key) => Get(key.ToString());
    public static string Format(AppTextKey key, params object[] arguments) => string.Format(CultureInfo.CurrentUICulture, Get(key), arguments);
}

public enum AppTextKey
{
    Cancel,
    Current,
    Date,
    ExportedAt,
    ExportRecords,
    HistoryExportFileName,
    HistoryExportPdfFileName,
    Optional,
    Of,
    Page,
    Reason,
    RemoveFromPublic,
    RemoveWaitingTime,
    TrendExportFileName,
    TrendHeading,
    TrendHistory,
    Wait,
    WaitingTimeConflict,
    WaitingTimeNotFound
}