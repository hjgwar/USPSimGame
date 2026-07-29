using Microsoft.AspNetCore.Components;
using USPSimGame.Data.Entities;

namespace USPSimGame.Components.Game;

public partial class PlannableLayerCatalogModal : ComponentBase
{
    [Parameter] public bool IsOpen { get; set; }
    [Parameter] public EventCallback<bool> IsOpenChanged { get; set; }
    [Parameter] public IEnumerable<GameSessionPlannableLayer>? AvailableLayers { get; set; }
    [Parameter] public HashSet<int> IncludedLayerIds { get; set; } = new();
    [Parameter] public int SelectedLayerId { get; set; }
    [Parameter] public EventCallback<int> OnLayerSelected { get; set; }

    protected async Task Close()
    {
        IsOpen = false;
        await IsOpenChanged.InvokeAsync(false);
    }

    protected async Task SelectLayer(int layerId)
    {
        await Close();
        await OnLayerSelected.InvokeAsync(layerId);
    }

    protected string GetGeometryTypeDisplay(PlannableGeometryType type)
    {
        return type switch
        {
            PlannableGeometryType.Point => "Point",
            PlannableGeometryType.Line => "Line",
            PlannableGeometryType.Polygon => "Polygon",
            _ => type.ToString()
        };
    }

    protected string GetUnitSuffix(PlannableGeometryType type)
    {
        return type switch
        {
            PlannableGeometryType.Point => "/ point",
            PlannableGeometryType.Line => "/ 50m",
            PlannableGeometryType.Polygon => "/ m²",
            _ => ""
        };
    }

    protected MarkupString GetExpenseDisplay(PlannableLayerDefinition def)
    {
        int durationYears = def.DefaultExpenseDurationMonths > 0 ? def.DefaultExpenseDurationMonths / 12 : 10;
        string unitSuffix = GetUnitSuffix(def.GeometryType);

        if (def.BaseMonthlyExpensePoints > 0 && def.MonthlyExpensePointsPerUnit > 0)
        {
            return new MarkupString($"{def.BaseMonthlyExpensePoints} + {def.MonthlyExpensePointsPerUnit} pts / month {unitSuffix}<br/>(for {durationYears} yrs)");
        }
        else if (def.BaseMonthlyExpensePoints > 0)
        {
            return new MarkupString($"{def.BaseMonthlyExpensePoints} pts / month {unitSuffix}<br/>(for {durationYears} yrs)");
        }
        else
        {
            return new MarkupString($"{def.MonthlyExpensePointsPerUnit} pts / month {unitSuffix}<br/>(for {durationYears} yrs)");
        }
    }
}
