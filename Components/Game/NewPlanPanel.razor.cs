using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using USPSimGame.Data.Entities;
using USPSimGame.Services.Layers;
using USPSimGame.Services.Plans;

namespace USPSimGame.Components.Game;

public class DraftFeatureItem
{
    public int GameSessionPlannableLayerId { get; set; }
    public GameSessionPlannableLayer Layer { get; set; } = default!;
    public string? GeoJsonGeometry { get; set; }
}

public partial class NewPlanPanel : ComponentBase
{
    [Inject]
    public IPlanService PlanService { get; set; } = default!;

    [Inject]
    public IMapLayerService MapLayerService { get; set; } = default!;

    [Inject]
    public IJSRuntime JSRuntime { get; set; } = default!;

    [Parameter, EditorRequired]
    public int GameSessionId { get; set; }

    [Parameter, EditorRequired]
    public int TeamId { get; set; }

    [Parameter]
    public int StartYear { get; set; } = 2026;

    [Parameter]
    public EventCallback OnSaved { get; set; }

    [Parameter]
    public EventCallback OnCancel { get; set; }

    protected string PlanName { get; set; } = string.Empty;
    protected string Description { get; set; } = string.Empty;

    protected int SelectedMonth { get; set; } = 1;
    protected int SelectedYear { get; set; } = 2026;

    protected List<GameSessionPlannableLayer> SessionPlannableLayers { get; set; } = new();
    protected int SelectedPlannableLayerId { get; set; } = 0;
    private int _previousSelectedPlannableLayerId = 0;

    protected List<DraftFeatureItem> DraftFeatures { get; set; } = new();

    protected bool IsLoading { get; set; } = true;
    protected bool IsSaving { get; set; } = false;
    protected string? ErrorMessage { get; set; }

    private bool _needsInitialDrawingActivation = false;

    protected override async Task OnInitializedAsync()
    {
        SelectedYear = StartYear;
        await LoadPlannableLayersAsync();
    }

    private async Task LoadPlannableLayersAsync()
    {
        IsLoading = true;
        try
        {
            SessionPlannableLayers = await MapLayerService.GetSessionPlannableLayersAsync(GameSessionId);
            SelectedPlannableLayerId = 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[NewPlanPanel] Error loading plannable layers: {ex.Message}");
        }
        finally
        {
            IsLoading = false;
        }
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (_needsInitialDrawingActivation)
        {
            _needsInitialDrawingActivation = false;
            await ActivateSelectedLayerDrawingAsync();
        }
    }

    protected async Task OnPlannableLayerChangedAsync()
    {
        await SyncCurrentLayerGeoJsonAsync();
        await ActivateSelectedLayerDrawingAsync();
    }

    protected async Task SelectDraftFeatureAsync(int gameSessionPlannableLayerId)
    {
        if (SelectedPlannableLayerId != gameSessionPlannableLayerId)
        {
            await SyncCurrentLayerGeoJsonAsync();
            SelectedPlannableLayerId = gameSessionPlannableLayerId;
            await ActivateSelectedLayerDrawingAsync();
        }
    }

    protected async Task RemoveDraftFeatureAsync(int gameSessionPlannableLayerId)
    {
        try
        {
            await JSRuntime.InvokeVoidAsync("uspsim2d5.removeDraftLayer", gameSessionPlannableLayerId.ToString());
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[NewPlanPanel] Error removing draft layer in JS: {ex.Message}");
        }

        DraftFeatures.RemoveAll(f => f.GameSessionPlannableLayerId == gameSessionPlannableLayerId);

        if (SelectedPlannableLayerId == gameSessionPlannableLayerId)
        {
            var remaining = SessionPlannableLayers.FirstOrDefault(l => DraftFeatures.Any(df => df.GameSessionPlannableLayerId == l.Id))
                ?? SessionPlannableLayers.FirstOrDefault();

            if (remaining != null)
            {
                SelectedPlannableLayerId = remaining.Id;
                await ActivateSelectedLayerDrawingAsync();
            }
        }
        StateHasChanged();
    }

    private async Task SyncCurrentLayerGeoJsonAsync()
    {
        if (_previousSelectedPlannableLayerId > 0)
        {
            try
            {
                string? geoJson = await JSRuntime.InvokeAsync<string?>("uspsim2d5.getDrawnGeoJsonForLayer", _previousSelectedPlannableLayerId.ToString());
                var existing = DraftFeatures.FirstOrDefault(f => f.GameSessionPlannableLayerId == _previousSelectedPlannableLayerId);

                if (!string.IsNullOrWhiteSpace(geoJson))
                {
                    if (existing == null)
                    {
                        var layer = SessionPlannableLayers.FirstOrDefault(l => l.Id == _previousSelectedPlannableLayerId);
                        if (layer != null)
                        {
                            DraftFeatures.Add(new DraftFeatureItem
                            {
                                GameSessionPlannableLayerId = _previousSelectedPlannableLayerId,
                                Layer = layer,
                                GeoJsonGeometry = geoJson
                            });
                        }
                    }
                    else
                    {
                        existing.GeoJsonGeometry = geoJson;
                    }
                }
                else if (existing != null)
                {
                    DraftFeatures.Remove(existing);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[NewPlanPanel] Error syncing GeoJSON for layer {_previousSelectedPlannableLayerId}: {ex.Message}");
            }
        }
    }

    private async Task ActivateSelectedLayerDrawingAsync()
    {
        var selectedLayer = SessionPlannableLayers.FirstOrDefault(l => l.Id == SelectedPlannableLayerId);
        if (selectedLayer?.PlannableLayerDefinition != null)
        {
            var def = selectedLayer.PlannableLayerDefinition;
            string geomType = def.GeometryType.ToString();
            string color = def.DefaultColor ?? "#3b82f6";
            string layerKey = selectedLayer.Id.ToString();

            try
            {
                await JSRuntime.InvokeVoidAsync("uspsim2d5.startDrawing", geomType, color, "rgba(59, 130, 246, 0.25)", layerKey);
                _previousSelectedPlannableLayerId = SelectedPlannableLayerId;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[NewPlanPanel] Error starting drawing tool for layer {layerKey}: {ex.Message}");
            }
        }
    }

    protected async Task SavePlanAsync()
    {
        ErrorMessage = null;
        if (string.IsNullOrWhiteSpace(PlanName))
        {
            ErrorMessage = "Please enter a name for the plan.";
            return;
        }

        IsSaving = true;
        try
        {
            await SyncCurrentLayerGeoJsonAsync();

            var payloads = DraftFeatures
                .Where(f => !string.IsNullOrWhiteSpace(f.GeoJsonGeometry))
                .Select(f => new PlanFeaturePayload
                {
                    GameSessionPlannableLayerId = f.GameSessionPlannableLayerId,
                    GeoJsonGeometry = f.GeoJsonGeometry
                })
                .ToList();

            if (!payloads.Any())
            {
                ErrorMessage = "Please draw shape geometry for at least one layer before saving.";
                IsSaving = false;
                return;
            }

            int calculatedStartMonth = ((SelectedYear - StartYear) * 12) + (SelectedMonth - 1);
            if (calculatedStartMonth < 0) calculatedStartMonth = 0;

            await PlanService.CreatePlanAsync(
                GameSessionId,
                TeamId,
                PlanName.Trim(),
                Description?.Trim(),
                calculatedStartMonth,
                payloads
            );

            await JSRuntime.InvokeVoidAsync("uspsim2d5.stopDrawing");
            await OnSaved.InvokeAsync();
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Error saving plan: {ex.Message}";
        }
        finally
        {
            IsSaving = false;
        }
    }

    protected async Task CancelAsync()
    {
        try
        {
            await JSRuntime.InvokeVoidAsync("uspsim2d5.stopDrawing");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[NewPlanPanel] Error stopping drawing tool: {ex.Message}");
        }
        await OnCancel.InvokeAsync();
    }

    protected async Task UndoPointAsync()
    {
        try
        {
            await JSRuntime.InvokeVoidAsync("uspsim2d5.undoDrawPoint");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[NewPlanPanel] Error invoking undoDrawPoint: {ex.Message}");
        }
    }

    protected async Task RedoPointAsync()
    {
        try
        {
            await JSRuntime.InvokeVoidAsync("uspsim2d5.redoDrawPoint");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[NewPlanPanel] Error invoking redoDrawPoint: {ex.Message}");
        }
    }
}
