using Microsoft.AspNetCore.Components;
using USPSimGame.Data.Entities;

namespace USPSimGame.Components.Game;

public partial class PlanViewPanel : ComponentBase
{
    [Parameter, EditorRequired]
    public Plan Plan { get; set; } = default!;

    [Parameter]
    public int StartYear { get; set; } = 2026;

    [Parameter]
    public EventCallback OnClose { get; set; }

    protected async Task CloseAsync()
    {
        await OnClose.InvokeAsync();
    }

    protected string FormatMonthYear(int startMonth)
    {
        int totalMonths = (StartYear * 12) + startMonth;
        int year = totalMonths / 12;
        int month = (totalMonths % 12) + 1;
        DateTime dt = new DateTime(year, month, 1);
        return $"{dt:MMMM yyyy} (Month {startMonth})";
    }

    protected string GetStateBadgeClass(PlanState state)
    {
        return state switch
        {
            PlanState.Draft => "bg-secondary-subtle text-secondary border-secondary-subtle",
            PlanState.Requested => "bg-info-subtle text-info border-info-subtle",
            PlanState.Approved => "bg-primary-subtle text-primary border-primary-subtle",
            PlanState.Implemented => "bg-success-subtle text-success border-success-subtle",
            PlanState.Archived => "bg-dark-subtle text-muted border-dark-subtle",
            _ => "bg-light text-dark"
        };
    }
}
