using Xunit;

namespace WT.Blazor.Tests;

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class WebApplicationFactoryCollection
{
    public const string Name = "Web application factory";
}