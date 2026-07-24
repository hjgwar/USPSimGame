using System.Globalization;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using OpenLayers.Blazor;
using USPSimGame.Components.Game;
using USPSimGame.Data.Entities;
using USPSimGame.Services;
using USPSimGame.Services.Layers;
using USPSimGame.Services.Plans;

namespace USPSimGame.Components.Pages;

public partial class Game : ComponentBase
{
    [Inject]
    public PlayerSessionState PlayerSessionState { get; set; } = default!;

    [Inject]
    public IMapLayerService MapLayerService { get; set; } = default!;

    [Inject]
    public IPlanService PlanService { get; set; } = default!;

    [Inject]
    public IJSRuntime JSRuntime { get; set; } = default!;

    [Inject]
    public NavigationManager Navigation { get; set; } = default!;

    [Inject]
    public ILogger<Game> Logger { get; set; } = default!;

    protected OpenStreetMap? map;
    protected PlansControlPanel? plansControlPanel;

    protected bool ShowNewPlanPanel { get; set; } = false;
    protected Plan? ActivePlan { get; set; }

    protected Coordinate MapCenter
    {
        get
        {
            var session = PlayerSessionState.CurrentGameSession;
            if (session != null && USPSimGame.Utils.GeoCoordinateConverter.TryParseLatLong(session.CenterLatLong, out double lat, out double lon))
            {
                return new Coordinate(lon, lat);
            }

            return new Coordinate(5.17516, 52.08640);
        }
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            Logger.LogInformation("Game.razor: OnAfterRenderAsync (firstRender=true)");

            if (!PlayerSessionState.IsConnected)
            {
                Logger.LogWarning("Game.razor: PlayerSessionState not connected, navigating to home.");
                Navigation.NavigateTo("/");
                return;
            }

            var session = PlayerSessionState.CurrentGameSession;
            Logger.LogInformation("Game.razor: CurrentGameSession Name='{Name}', CenterLatLong='{Center}'", session?.Name, session?.CenterLatLong);

            if (session != null)
            {
                try
                {
                    Logger.LogInformation("Game.razor: Loading session map layers for Session #{Id}...", session.Id);
                    var layers = await MapLayerService.GetSessionLayersAsync(session.Id);

                    // Fallback: If any active layer has unpopulated cache for session, trigger pre-fetch on demand
                    if ((!layers.Any() || layers.Any(l => l.IsEnabled && string.IsNullOrEmpty(l.CachedDataContent))) && !string.IsNullOrWhiteSpace(session.CenterLatLong))
                    {
                        Logger.LogInformation("Game.razor: Unpopulated layer cache detected for Session {Id}. Triggering pre-fetch on demand...", session.Id);
                        await MapLayerService.PreFetchAndSaveSessionLayersAsync(session.Id, session.CenterLatLong);
                        layers = await MapLayerService.GetSessionLayersAsync(session.Id);
                    }

                    // Trigger browser HTTP stream download for each active session layer
                    foreach (var layer in layers)
                    {
                        if (layer.IsEnabled && !string.IsNullOrEmpty(layer.CachedDataContent))
                        {
                            Logger.LogInformation("Game.razor: Triggering browser HTTP layer stream for '{Key}' in Session #{Id}...", layer.LayerDefinition.Key, session.Id);
                            await JSRuntime.InvokeVoidAsync("uspsim2d5.loadSessionLayer", session.Id, layer.LayerDefinition.Key);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Logger.LogError(ex, "Game.razor: Error triggering session layer HTTP streams.");
                }
            }
        }
    }

    protected void OpenNewPlanPanel()
    {
        ActivePlan = null;
        ShowNewPlanPanel = true;
        _ = ClearPlanHighlightAsync();
    }

    protected async Task CloseNewPlanPanelAsync()
    {
        ShowNewPlanPanel = false;
        await StopDrawingAsync();
    }

    protected async Task HandlePlanSavedAsync()
    {
        ShowNewPlanPanel = false;
        await StopDrawingAsync();
        if (plansControlPanel != null)
        {
            await plansControlPanel.RefreshPlansAsync();
        }
    }

    protected async Task HandlePlanSelectedAsync(Plan? plan)
    {
        ShowNewPlanPanel = false;
        await StopDrawingAsync();
        ActivePlan = plan;

        if (ActivePlan == null)
        {
            await ClearPlanHighlightAsync();
        }
        else
        {
            await HighlightPlanFeaturesAsync(ActivePlan);
        }
    }

    protected async Task ClosePlanViewAsync()
    {
        ActivePlan = null;
        await ClearPlanHighlightAsync();
    }

    private async Task HighlightPlanFeaturesAsync(Plan plan)
    {
        try
        {
            var featurePayloads = plan.Features
                .Where(f => !string.IsNullOrEmpty(f.GeoJsonGeometry))
                .Select(f => new
                {
                    geoJson = f.GeoJsonGeometry,
                    color = f.GameSessionPlannableLayer?.PlannableLayerDefinition?.DefaultColor ?? "#10b981"
                })
                .ToList();

            if (featurePayloads.Any())
            {
                await JSRuntime.InvokeVoidAsync("uspsim2d5.renderPlanFeatures", featurePayloads, "#10b981");
            }
            else
            {
                await ClearPlanHighlightAsync();
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Game.razor: Error rendering plan feature highlight on map.");
        }
    }

    private async Task ClearPlanHighlightAsync()
    {
        try
        {
            await JSRuntime.InvokeVoidAsync("uspsim2d5.clearPlanHighlight");
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Game.razor: Error clearing plan highlight.");
        }
    }

    private async Task StopDrawingAsync()
    {
        try
        {
            await JSRuntime.InvokeVoidAsync("uspsim2d5.stopDrawing");
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Game.razor: Error stopping drawing mode.");
        }
    }
}