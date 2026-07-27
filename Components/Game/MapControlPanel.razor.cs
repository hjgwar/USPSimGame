using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using USPSimGame.Data.Entities;
using USPSimGame.Services;
using USPSimGame.Services.Layers;

namespace USPSimGame.Components.Game;

public partial class MapControlPanel : ComponentBase
{
    [Inject]
    public IMapLayerService MapLayerService { get; set; } = default!;

    [Inject]
    public ITeamService TeamService { get; set; } = default!;

    [Inject]
    public IJSRuntime JSRuntime { get; set; } = default!;

    [Parameter, EditorRequired]
    public int GameSessionId { get; set; }

    protected List<GameSessionMapLayer> SessionLayers { get; set; } = new();
    protected List<Team> SessionTeams { get; set; } = new();
    protected Dictionary<string, bool> LayerVisibilities { get; set; } = new();
    protected List<GameSessionMapLayer> OrderedVisibleLayers { get; set; } = new();

    protected bool ShowTeamAreas { get; set; } = true;

    protected bool IsCollapsed { get; set; } = false;
    protected string ActiveTab { get; set; } = "layers";
    protected int? DraggedIndex { get; set; }

    protected int VisibleCount => LayerVisibilities.Values.Count(v => v);

    protected override async Task OnInitializedAsync()
    {
        await LoadSessionLayersAsync();
        await LoadSessionTeamsAsync();
    }

    private async Task LoadSessionTeamsAsync()
    {
        try
        {
            SessionTeams = await TeamService.GetTeamsByGameSessionAsync(GameSessionId);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[MapControlPanel] Error loading session teams: {ex.Message}");
        }
    }

    private async Task LoadSessionLayersAsync()
    {
        try
        {
            SessionLayers = await MapLayerService.GetSessionLayersAsync(GameSessionId);

            foreach (var layer in SessionLayers)
            {
                if (!LayerVisibilities.ContainsKey(layer.LayerDefinition.Key))
                {
                    LayerVisibilities[layer.LayerDefinition.Key] = true;
                }
            }

            await UpdateOrderedVisibleLayersAsync();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[MapControlPanel] Error loading session layers: {ex.Message}");
        }
    }

    protected void ToggleCollapse()
    {
        IsCollapsed = !IsCollapsed;
    }

    protected async Task ToggleTeamAreasVisibilityAsync(bool isVisible)
    {
        ShowTeamAreas = isVisible;
        try
        {
            await JSRuntime.InvokeVoidAsync("uspsim2d5.toggleTeamAreasVisibility", isVisible);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[MapControlPanel] JS Error toggling team areas visibility: {ex.Message}");
        }
    }

    protected async Task ToggleLayerVisibilityAsync(string layerKey, bool isVisible)
    {
        LayerVisibilities[layerKey] = isVisible;
        await UpdateOrderedVisibleLayersAsync();

        try
        {
            await JSRuntime.InvokeVoidAsync("uspsim2d5.setLayerVisibility", layerKey, isVisible);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[MapControlPanel] JS Error setting visibility for '{layerKey}': {ex.Message}");
        }
    }

    private async Task UpdateOrderedVisibleLayersAsync()
    {
        var currentlyVisibleKeys = LayerVisibilities.Where(kv => kv.Value).Select(kv => kv.Key).ToHashSet();

        // Keep items in OrderedVisibleLayers that are still visible
        OrderedVisibleLayers.RemoveAll(l => !currentlyVisibleKeys.Contains(l.LayerDefinition.Key));

        // Add newly enabled visible layers to the top of the order list
        foreach (var layer in SessionLayers)
        {
            if (currentlyVisibleKeys.Contains(layer.LayerDefinition.Key) && !OrderedVisibleLayers.Any(l => l.Id == layer.Id))
            {
                OrderedVisibleLayers.Insert(0, layer);
            }
        }

        await ReassignZIndicesAsync();
    }

    protected void HandleDragStart(int index)
    {
        DraggedIndex = index;
    }

    protected async Task HandleDropAsync(int targetIndex)
    {
        if (DraggedIndex.HasValue && DraggedIndex.Value != targetIndex)
        {
            var item = OrderedVisibleLayers[DraggedIndex.Value];
            OrderedVisibleLayers.RemoveAt(DraggedIndex.Value);
            OrderedVisibleLayers.Insert(targetIndex, item);
            DraggedIndex = null;

            await ReassignZIndicesAsync();
        }
    }

    protected async Task MoveLayerUpAsync(int index)
    {
        if (index > 0)
        {
            var item = OrderedVisibleLayers[index];
            OrderedVisibleLayers.RemoveAt(index);
            OrderedVisibleLayers.Insert(index - 1, item);

            await ReassignZIndicesAsync();
        }
    }

    protected async Task MoveLayerDownAsync(int index)
    {
        if (index < OrderedVisibleLayers.Count - 1)
        {
            var item = OrderedVisibleLayers[index];
            OrderedVisibleLayers.RemoveAt(index);
            OrderedVisibleLayers.Insert(index + 1, item);

            await ReassignZIndicesAsync();
        }
    }

    private async Task ReassignZIndicesAsync()
    {
        int baseZIndex = 200;
        for (int i = 0; i < OrderedVisibleLayers.Count; i++)
        {
            int assignedZIndex = baseZIndex - (i * 10);
            var key = OrderedVisibleLayers[i].LayerDefinition.Key;

            try
            {
                await JSRuntime.InvokeVoidAsync("uspsim2d5.setLayerZIndex", key, assignedZIndex);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[MapControlPanel] JS Error setting zIndex for '{key}': {ex.Message}");
            }
        }
    }
}
