using Microsoft.Extensions.Options;

namespace WT.Blazor.Models;

public sealed class AdGroupsOptionsValidator : IValidateOptions<AdGroupsOptions>
{
    public ValidateOptionsResult Validate(string? name, AdGroupsOptions options)
    {
        var groups = new[] { options.Viewer, options.Contributor, options.Admin };
        if (groups.Any(string.IsNullOrWhiteSpace))
            return ValidateOptionsResult.Fail("AdGroups:Viewer, AdGroups:Contributor, and AdGroups:Admin must be configured.");

        return groups.Distinct(StringComparer.OrdinalIgnoreCase).Count() == groups.Length
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail("AdGroups:Viewer, AdGroups:Contributor, and AdGroups:Admin must be distinct.");
    }
}