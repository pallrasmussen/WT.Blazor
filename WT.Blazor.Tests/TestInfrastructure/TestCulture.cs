using System.Globalization;

namespace WT.Blazor.Tests.TestInfrastructure;

internal sealed class TestCulture : IDisposable
{
    private readonly CultureInfo original = CultureInfo.CurrentUICulture;

    public TestCulture(string name) => CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(name);

    public void Dispose() => CultureInfo.CurrentUICulture = original;
}
