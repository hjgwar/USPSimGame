using System.Globalization;
using USPSimGame.Data.Models;
using USPSimGame.Utils;

namespace USPSimGame.Services.Layers;

public class BuildingService : IBuildingService, IMapLayerProvider
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<BuildingService> _logger;

    public string ProviderKey => "pdok-3dbag-buildings";

    public BuildingService(HttpClient httpClient, ILogger<BuildingService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public Task<string?> FetchLayerDataAsync(string centerLatLong, double radiusKm = 1.0)
    {
        return GetBuildingFootprintsGeoJsonAsync(centerLatLong, radiusKm);
    }

    public LayerLegendInfo GetLegendInfo()
    {
        return new LayerLegendInfo
        {
            Items = new List<LayerLegendItem>
            {
                new LayerLegendItem { Label = "Low Kiosks & Sheds (≤ 4m)", Shape = LegendItemShape.PolygonSwatch, Color = "rgba(226, 232, 240, 0.8)", BorderColor = "#cbd5e1" },
                new LayerLegendItem { Label = "Standard Buildings (4m - 34m)", Shape = LegendItemShape.PolygonSwatch, Color = "rgba(238, 242, 252, 0.94)", BorderColor = "#64748b" },
                new LayerLegendItem { Label = "High-Rise Towers (≥ 35m)", Shape = LegendItemShape.PolygonSwatch, Color = "#ffffff", BorderColor = "#334155" }
            }
        };
    }

    public async Task<string?> GetBuildingFootprintsGeoJsonAsync(string centerLatLong, double radiusKm = 1.0)
    {
        _logger.LogInformation("BuildingService: Starting 3D BAG fetch for center '{CenterLatLong}', radius {RadiusKm}km", centerLatLong, radiusKm);

        if (!GeoCoordinateConverter.TryParseLatLong(centerLatLong, out double lat, out double lon))
        {
            _logger.LogWarning("BuildingService: Failed to parse coordinates from '{CenterLatLong}'", centerLatLong);
            return null;
        }

        _logger.LogInformation("BuildingService: Parsed Center -> Latitude: {Lat}, Longitude: {Lon}", lat, lon);

        var (rdX, rdY) = GeoCoordinateConverter.Wgs84ToRd(lat, lon);
        _logger.LogInformation("BuildingService: Converted RD Coordinates -> X: {RdX}, Y: {RdY}", rdX, rdY);

        double radiusMeters = radiusKm * 1000.0;
        double minX = Math.Round(rdX - radiusMeters);
        double maxX = Math.Round(rdX + radiusMeters);
        double minY = Math.Round(rdY - radiusMeters);
        double maxY = Math.Round(rdY + radiusMeters);

        string wfsUrl = $"https://data.3dbag.nl/api/BAG3D/wfs?request=GetFeature&service=WFS&version=2.0.0&typename=BAG3D:lod13&outputFormat=json&count=2500&srsName=EPSG:4326&bbox={minX.ToString(CultureInfo.InvariantCulture)},{minY.ToString(CultureInfo.InvariantCulture)},{maxX.ToString(CultureInfo.InvariantCulture)},{maxY.ToString(CultureInfo.InvariantCulture)}";

        _logger.LogInformation("BuildingService: Requesting 3D BAG WFS URL: {WfsUrl}", wfsUrl);

        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
            var response = await _httpClient.GetAsync(wfsUrl, cts.Token);
            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync(cts.Token);
                _logger.LogInformation("BuildingService: Received successful 3D BAG response ({Length} bytes).", content.Length);
                return content;
            }
            else
            {
                _logger.LogWarning("BuildingService: 3D BAG WFS returned HTTP status code {StatusCode}", response.StatusCode);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "BuildingService: Exception while fetching 3D building footprints from 3D BAG.");
        }

        return null;
    }
}
