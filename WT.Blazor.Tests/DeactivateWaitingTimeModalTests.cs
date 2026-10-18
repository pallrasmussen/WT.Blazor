using Bunit;
using Microsoft.AspNetCore.Components;
using WT.Blazor.Components.Shared;
using WT.Blazor.Models;
using Xunit;

namespace WT.Blazor.Tests;

public class DeactivateWaitingTimeModalTests : BunitContext
{
    [Fact]
    public void ModalPropagatesReasonAndCommands()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        string? reason = null;
        var canceled = false;
        var confirmed = false;
        var item = new WaitingTime
        {
            Id = 1,
            Name = "MR-skanning",
            Hospital = Hospital.Klaksvikar,
            WaitValue = 3,
            Unit = WaitUnit.Vikur
        };

        var cut = Render<DeactivateWaitingTimeModal>(parameters => parameters
            .Add(component => component.Item, item)
            .Add(component => component.ReasonChanged, EventCallback.Factory.Create<string?>(this, value => reason = value))
            .Add(component => component.Cancel, EventCallback.Factory.Create(this, () => canceled = true))
            .Add(component => component.Confirm, EventCallback.Factory.Create(this, () => confirmed = true)));

        cut.Find("#deactivation-reason").Input("Duplicate entry");
        cut.FindAll("button")[0].Click();
        cut.FindAll("button")[1].Click();

        Assert.Equal("Duplicate entry", reason);
        Assert.True(canceled);
        Assert.True(confirmed);
    }
}