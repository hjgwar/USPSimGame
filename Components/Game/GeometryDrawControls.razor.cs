using Microsoft.AspNetCore.Components;

namespace USPSimGame.Components.Game;

public partial class GeometryDrawControls : ComponentBase
{
    [Parameter]
    public string LayerName { get; set; } = "Geometry";

    [Parameter]
    public string GeometryType { get; set; } = "Polygon";

    [Parameter]
    public string Category { get; set; } = "Drawing";

    [Parameter]
    public string Color { get; set; } = "#3b82f6";

    [Parameter]
    public string Icon { get; set; } = "bi-hexagon-fill";

    [Parameter]
    public bool IsActive { get; set; } = true;

    [Parameter]
    public EventCallback OnUndoPoint { get; set; }

    [Parameter]
    public EventCallback OnRedoPoint { get; set; }

    [Parameter]
    public EventCallback OnDeletePoint { get; set; }

    protected async Task UndoAsync()
    {
        await OnUndoPoint.InvokeAsync();
    }

    protected async Task RedoAsync()
    {
        await OnRedoPoint.InvokeAsync();
    }

    protected async Task DeletePointAsync()
    {
        await OnDeletePoint.InvokeAsync();
    }
}
