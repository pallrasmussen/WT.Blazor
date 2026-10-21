using Bunit;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using WT.Blazor.Components.Pages;
using Xunit;

namespace WT.Blazor.Tests;

public class NotFoundPageTests : BunitContext
{
    [Fact]
    public void AuthenticationFailureShowsAnInformativeAccessMessage()
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Features.Set<IStatusCodeReExecuteFeature>(new AuthenticationFailureFeature());
        Services.AddSingleton<IHttpContextAccessor>(new HttpContextAccessor { HttpContext = httpContext });

        var cut = Render<NotFound>();

        Assert.Contains("Administration access requires sign-in", cut.Markup);
        Assert.Contains("cancel the sign-in prompt", cut.Markup);
        Assert.Equal("/internal", cut.Find("a[href='/internal']").GetAttribute("href"));
        Assert.Equal("/", cut.Find("a[href='/']").GetAttribute("href"));
    }

    [Fact]
    public void ErrorPageProvidesALocalizedRecoveryAction()
    {
        var cut = Render<Error>();

        Assert.Contains("Something went wrong", cut.Markup);
        Assert.Contains("We could not complete your request", cut.Markup);
        Assert.Equal("/", cut.Find("a.btn-secondary").GetAttribute("href"));
    }

    private sealed class AuthenticationFailureFeature : IStatusCodeReExecuteFeature
    {
        public string OriginalPathBase { get; set; } = "";
        public string OriginalPath { get; set; } = "/internal";
        public string? OriginalQueryString { get; set; }
        public int OriginalStatusCode => StatusCodes.Status401Unauthorized;
    }
}