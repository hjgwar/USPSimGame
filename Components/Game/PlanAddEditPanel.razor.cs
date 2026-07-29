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
    public string? PropertiesJson { get; set; }
}

public partial class PlanAddEditPanel : ComponentBase
{
    [Inject]
    public IPlanService PlanService { get; set; } = default!;

    [Inject]
    public IPlanApprovalEvaluationService EvaluationService { get; set; } = default!;

    [Inject]
    public IMapLayerService MapLayerService { get; set; } = default!;

    [Inject]
    public USPSimGame.Services.Costing.ICostCalculationService CostCalculationService { get; set; } = default!;

    [Inject]
    public IJSRuntime JSRuntime { get; set; } = default!;

    [Inject]
    public USPSimGame.Services.PlayerSessionState PlayerSessionState { get; set; } = default!;

    [Parameter, EditorRequired]
    public int GameSessionId { get; set; }

    [Parameter, EditorRequired]
    public int TeamId { get; set; }

    [Parameter]
    public int CurrentPlayerSessionId { get; set; }

    [Parameter]
    public int StartYear { get; set; } = 2026;

    [Parameter]
    public Plan? PlanToEdit { get; set; }

    [Parameter]
    public EventCallback<Plan> OnSaved { get; set; }

    [Parameter]
    public EventCallback OnCancel { get; set; }

    protected string PlanName { get; set; } = string.Empty;
    protected string Description { get; set; } = string.Empty;

    private int _selectedMonth = 1;
    private int _selectedYear = 2026;

    protected int SelectedMonth
    {
        get => _selectedMonth;
        set
        {
            _selectedMonth = value;
            ValidateSelectedTargetDate();
        }
    }

    protected int SelectedYear
    {
        get => _selectedYear;
        set
        {
            _selectedYear = value;
            ValidateSelectedTargetDate();
        }
    }

    protected int CalculatedConstructionMonths { get; set; } = 0;
    protected int MinimumAllowedCompletionMonth { get; set; } = 0;
    protected string? ConstructionTimeWarningMessage { get; set; }
    protected bool IsConstructionTimeValid { get; set; } = true;

    protected List<GameSessionPlannableLayer> SessionPlannableLayers { get; set; } = new();
    protected int SelectedPlannableLayerId { get; set; } = 0;
    private int _previousSelectedPlannableLayerId = 0;

    protected List<DraftFeatureItem> DraftFeatures { get; set; } = new();

    protected bool IsLoading { get; set; } = true;
    protected bool IsSaving { get; set; } = false;
    protected string? ErrorMessage { get; set; }

    private bool _needsInitialDrawingActivation = false;

    protected bool IsEditMode => PlanToEdit != null;

    protected override async Task OnInitializedAsync()
    {
        SelectedYear = StartYear;

        if (PlanToEdit != null)
        {
            PlanName = PlanToEdit.Name;
            Description = PlanToEdit.Description ?? string.Empty;

            int totalMonths = (StartYear * 12) + PlanToEdit.StartMonth;
            SelectedYear = totalMonths / 12;
            SelectedMonth = (totalMonths % 12) + 1;
        }

        await LoadPlannableLayersAsync();
    }

    private async Task LoadPlannableLayersAsync()
    {
        IsLoading = true;
        try
        {
            SessionPlannableLayers = await MapLayerService.GetSessionPlannableLayersAsync(GameSessionId);

            if (PlanToEdit != null && PlanToEdit.Features.Any())
            {
                foreach (var feat in PlanToEdit.Features)
                {
                    var layer = SessionPlannableLayers.FirstOrDefault(l => l.Id == feat.GameSessionPlannableLayerId);
                    if (layer != null && !string.IsNullOrWhiteSpace(feat.GeoJsonGeometry))
                    {
                        DraftFeatures.Add(new DraftFeatureItem
                        {
                            GameSessionPlannableLayerId = layer.Id,
                            Layer = layer,
                            GeoJsonGeometry = feat.GeoJsonGeometry,
                            PropertiesJson = feat.PropertiesJson
                        });
                    }
                }

                if (DraftFeatures.Any())
                {
                    SelectedPlannableLayerId = DraftFeatures.First().GameSessionPlannableLayerId;
                }
            }
            else
            {
                SelectedPlannableLayerId = 0;
            }

            _needsInitialDrawingActivation = true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[PlanAddEditPanel] Error loading plannable layers: {ex.Message}");
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

            if (PlanToEdit != null && DraftFeatures.Any())
            {
                foreach (var item in DraftFeatures)
                {
                    if (item.Layer?.PlannableLayerDefinition != null && !string.IsNullOrWhiteSpace(item.GeoJsonGeometry))
                    {
                        var def = item.Layer.PlannableLayerDefinition;
                        await JSRuntime.InvokeVoidAsync("uspsim2d5.startDrawing", def.GeometryType.ToString(), def.DefaultColor ?? "#3b82f6", "rgba(59, 130, 246, 0.25)", item.GameSessionPlannableLayerId.ToString());
                        await JSRuntime.InvokeVoidAsync("uspsim2d5.loadDraftFeatureGeometry", item.GameSessionPlannableLayerId.ToString(), item.GeoJsonGeometry);
                    }
                }

                if (SelectedPlannableLayerId > 0)
                {
                    await ActivateSelectedLayerDrawingAsync();
                }
            }
        }
    }

    private void EnsureSelectedLayerInDraftFeatures()
    {
        if (SelectedPlannableLayerId > 0)
        {
            var existing = DraftFeatures.FirstOrDefault(f => f.GameSessionPlannableLayerId == SelectedPlannableLayerId);
            if (existing == null)
            {
                var layer = SessionPlannableLayers.FirstOrDefault(l => l.Id == SelectedPlannableLayerId);
                if (layer != null)
                {
                    DraftFeatures.Add(new DraftFeatureItem
                    {
                        GameSessionPlannableLayerId = SelectedPlannableLayerId,
                        Layer = layer,
                        GeoJsonGeometry = null
                    });
                }
            }
        }
    }

    protected bool _isCatalogModalOpen = false;
    protected HashSet<int> _includedLayerIdsSet => DraftFeatures.Select(f => f.GameSessionPlannableLayerId).ToHashSet();
    protected USPSimGame.Services.Costing.PlanCostEstimate CurrentPlanCost { get; set; }

    protected void OpenCatalogModal()
    {
        _isCatalogModalOpen = true;
    }

    protected async Task OnCatalogLayerSelectedAsync(int layerId)
    {
        SelectedPlannableLayerId = layerId;
        await OnPlannableLayerChangedAsync();
    }

    protected double GetFeatureInvestmentPoints(DraftFeatureItem item)
    {
        if (item.Layer?.PlannableLayerDefinition == null || string.IsNullOrWhiteSpace(item.GeoJsonGeometry)) return 0;
        var est = CostCalculationService.CalculateFeatureCost(item.Layer.PlannableLayerDefinition, item.GeoJsonGeometry);
        return est.TotalInvestmentPoints;
    }

    protected async Task RecalculatePlanCostAsync()
    {
        await SyncCurrentLayerGeoJsonAsync();
        var featurePairs = DraftFeatures
            .Where(f => f.Layer?.PlannableLayerDefinition != null && !string.IsNullOrWhiteSpace(f.GeoJsonGeometry))
            .Select(f => (f.Layer.PlannableLayerDefinition, f.GeoJsonGeometry));

        CurrentPlanCost = CostCalculationService.CalculateDraftPlanCost(featurePairs);
    }

    protected async Task OnPlannableLayerChangedAsync()
    {
        await SyncCurrentLayerGeoJsonAsync();
        EnsureSelectedLayerInDraftFeatures();
        await ActivateSelectedLayerDrawingAsync();
        await RecalculatePlanCostAsync();
    }

    protected async Task SelectDraftFeatureAsync(int gameSessionPlannableLayerId)
    {
        if (SelectedPlannableLayerId != gameSessionPlannableLayerId)
        {
            await SyncCurrentLayerGeoJsonAsync();
            SelectedPlannableLayerId = gameSessionPlannableLayerId;
            EnsureSelectedLayerInDraftFeatures();
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
            Console.WriteLine($"[PlanAddEditPanel] Error removing draft layer in JS: {ex.Message}");
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
            else
            {
                SelectedPlannableLayerId = 0;
            }
        }
        StateHasChanged();
    }

    private async Task SyncCurrentLayerGeoJsonAsync()
    {
        try
        {
            foreach (var item in DraftFeatures.ToList())
            {
                string? geoJson = await JSRuntime.InvokeAsync<string?>("uspsim2d5.getDrawnGeoJsonForLayer", item.GameSessionPlannableLayerId.ToString());
                if (!string.IsNullOrWhiteSpace(geoJson))
                {
                    item.GeoJsonGeometry = geoJson;
                }
            }

            if (_previousSelectedPlannableLayerId > 0 && !DraftFeatures.Any(f => f.GameSessionPlannableLayerId == _previousSelectedPlannableLayerId))
            {
                string? prevGeoJson = await JSRuntime.InvokeAsync<string?>("uspsim2d5.getDrawnGeoJsonForLayer", _previousSelectedPlannableLayerId.ToString());
                if (!string.IsNullOrWhiteSpace(prevGeoJson))
                {
                    var layer = SessionPlannableLayers.FirstOrDefault(l => l.Id == _previousSelectedPlannableLayerId);
                    if (layer != null)
                    {
                        DraftFeatures.Add(new DraftFeatureItem
                        {
                            GameSessionPlannableLayerId = _previousSelectedPlannableLayerId,
                            Layer = layer,
                            GeoJsonGeometry = prevGeoJson
                        });
                    }
                }
            }

            await ReevaluateRealtimeSpatialConditionsAsync();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[PlanAddEditPanel] Error syncing GeoJSON: {ex.Message}");
        }
    }

    protected PlanApprovalEvaluation? RealtimeEvaluation { get; set; }

    protected async Task ReevaluateRealtimeSpatialConditionsAsync()
    {
        try
        {
            var geoms = DraftFeatures
                .Where(f => !string.IsNullOrWhiteSpace(f.GeoJsonGeometry))
                .Select(f => f.GeoJsonGeometry!)
                .ToList();

            RealtimeEvaluation = await EvaluationService.EvaluatePlanGeometryAsync(GameSessionId, TeamId, geoms);

            // Calculate dynamic construction time from draft features
            var featurePairs = DraftFeatures
                .Where(f => f.Layer?.PlannableLayerDefinition != null && !string.IsNullOrWhiteSpace(f.GeoJsonGeometry))
                .Select(f => (f.Layer.PlannableLayerDefinition, f.GeoJsonGeometry));

            CalculatedConstructionMonths = CostCalculationService.CalculateDraftPlanConstructionTimeMonths(featurePairs);

            int currentMonth = PlayerSessionState.CurrentGameSession?.CurrentMonth ?? 0;
            MinimumAllowedCompletionMonth = currentMonth + CalculatedConstructionMonths;

            ValidateSelectedTargetDate();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[PlanAddEditPanel] Error in spatial evaluation: {ex.Message}");
        }
    }

    protected void ValidateSelectedTargetDate()
    {
        int selectedTargetMonth = ((SelectedYear - StartYear) * 12) + (SelectedMonth - 1);
        if (selectedTargetMonth < MinimumAllowedCompletionMonth)
        {
            IsConstructionTimeValid = false;
            int reqStartMonth = selectedTargetMonth - CalculatedConstructionMonths;
            string reqStartFormatted = USPSimGame.Utils.CommonGameUtils.FormatMonthYear(reqStartMonth, StartYear);
            string minCompFormatted = USPSimGame.Utils.CommonGameUtils.FormatMonthYear(MinimumAllowedCompletionMonth, StartYear);
            string selTargetFormatted = USPSimGame.Utils.CommonGameUtils.FormatMonthYear(selectedTargetMonth, StartYear);

            ConstructionTimeWarningMessage = $"Construction requires {CalculatedConstructionMonths} month(s). Completion by {selTargetFormatted} would require starting in {reqStartFormatted} (in the past). Earliest completion is {minCompFormatted}.";
        }
        else
        {
            IsConstructionTimeValid = true;
            ConstructionTimeWarningMessage = null;
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
                Console.WriteLine($"[PlanAddEditPanel] Error starting drawing tool for layer {layerKey}: {ex.Message}");
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
                    GeoJsonGeometry = f.GeoJsonGeometry,
                    PropertiesJson = f.PropertiesJson
                })
                .ToList();

            if (!payloads.Any())
            {
                ErrorMessage = "Please draw shape geometry for at least one layer before saving.";
                IsSaving = false;
                return;
            }

            ValidateSelectedTargetDate();
            if (!IsConstructionTimeValid)
            {
                ErrorMessage = ConstructionTimeWarningMessage ?? "The selected completion date does not provide enough lead time for construction.";
                IsSaving = false;
                return;
            }

            int calculatedStartMonth = ((SelectedYear - StartYear) * 12) + (SelectedMonth - 1);
            if (calculatedStartMonth < 0) calculatedStartMonth = 0;

            Plan savedPlan;
            if (IsEditMode && PlanToEdit != null)
            {
                savedPlan = await PlanService.UpdatePlanAsync(
                    PlanToEdit.Id,
                    PlanName.Trim(),
                    Description?.Trim(),
                    calculatedStartMonth,
                    payloads
                );
                await PlanService.UnlockPlanAsync(PlanToEdit.Id);
            }
            else
            {
                savedPlan = await PlanService.CreatePlanAsync(
                    GameSessionId,
                    TeamId,
                    PlanName.Trim(),
                    Description?.Trim(),
                    calculatedStartMonth,
                    payloads
                );
            }

            await JSRuntime.InvokeVoidAsync("uspsim2d5.stopDrawing");
            await OnSaved.InvokeAsync(savedPlan);
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
            if (IsEditMode && PlanToEdit != null)
            {
                await PlanService.UnlockPlanAsync(PlanToEdit.Id);
            }
            await JSRuntime.InvokeVoidAsync("uspsim2d5.stopDrawing");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[PlanAddEditPanel] Error stopping drawing tool: {ex.Message}");
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
            Console.WriteLine($"[PlanAddEditPanel] Error invoking undoDrawPoint: {ex.Message}");
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
            Console.WriteLine($"[PlanAddEditPanel] Error invoking redoDrawPoint: {ex.Message}");
        }
    }

    protected async Task DeletePointAsync()
    {
        try
        {
            await JSRuntime.InvokeVoidAsync("uspsim2d5.deleteSelectedVertex");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[PlanAddEditPanel] Error invoking deleteSelectedVertex: {ex.Message}");
        }
    }
}
