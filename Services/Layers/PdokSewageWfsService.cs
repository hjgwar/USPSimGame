using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using USPSimGame.Data.Models;
using USPSimGame.Utils;

namespace USPSimGame.Services.Layers;

public class PdokSewageWfsService : IMapLayerProvider
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<PdokSewageWfsService> _logger;

    public string ProviderKey => "pdok-gwsw-sewage";

    private const string WfsBaseUrl = "https://service.pdok.nl/rioned/beheer-stedelijk-watersystemen-gwsw/wfs/v1_0";

    public PdokSewageWfsService(HttpClient httpClient, ILogger<PdokSewageWfsService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public LayerLegendInfo GetLegendInfo()
    {
        return new LayerLegendInfo
        {
            Items = new List<LayerLegendItem>
            {
                new LayerLegendItem { Label = "Sewage & Drainage Mains (BeheerLeiding)", Shape = LegendItemShape.Line, Color = "#059669", LineWidthPx = 3.5 },
                new LayerLegendItem { Label = "Inspection Manholes & Pits (BeheerPut)", Shape = LegendItemShape.Point, Color = "#059669", BorderColor = "#ffffff" },
                new LayerLegendItem { Label = "Pumping Stations & Gemalen (BeheerPomp)", Shape = LegendItemShape.Point, Color = "#ef4444", BorderColor = "#ffffff" }
            }
        };
    }

    public async Task<string?> FetchLayerDataAsync(string centerLatLong, double radiusKm = 1.0)
    {
        _logger.LogInformation("PdokSewageWfsService: Fetching urban water & sewage features via PDOK GWSW WFS for center '{Center}'", centerLatLong);

        if (!GeoCoordinateConverter.TryParseLatLong(centerLatLong, out double lat, out double lon))
        {
            _logger.LogWarning("PdokSewageWfsService: Invalid center lat/long format '{Center}'. Defaulting to Utrecht Science Park center.", centerLatLong);
            lat = 52.08640;
            lon = 5.17516;
        }

        var (rdX, rdY) = GeoCoordinateConverter.Wgs84ToRd(lat, lon);
        double radiusMeters = Math.Max(radiusKm, 1.5) * 1000;
        double minX = rdX - radiusMeters;
        double maxX = rdX + radiusMeters;
        double minY = rdY - radiusMeters;
        double maxY = rdY + radiusMeters;

        string bboxStr = string.Create(CultureInfo.InvariantCulture, $"{minX:F0},{minY:F0},{maxX:F0},{maxY:F0},EPSG:28992");

        var featureTypes = new[]
        {
            "beheerstedelijkwater:BeheerLeiding",
            "beheerstedelijkwater:BeheerPut",
            "beheerstedelijkwater:BeheerPomp"
        };

        var allFeatures = new JsonArray();

        foreach (var typeName in featureTypes)
        {
            string requestUrl = $"{WfsBaseUrl}?service=WFS&version=2.0.0&request=GetFeature&typeName={typeName}&outputFormat=application/json&srsName=EPSG:4326&bbox={bboxStr}";
            try
            {
                _logger.LogInformation("PdokSewageWfsService: Querying featureType '{TypeName}' with BBOX '{Bbox}'...", typeName, bboxStr);
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
                var response = await _httpClient.GetAsync(requestUrl, cts.Token);

                if (response.IsSuccessStatusCode)
                {
                    var jsonStr = await response.Content.ReadAsStringAsync(cts.Token);
                    using var doc = JsonDocument.Parse(jsonStr);
                    if (doc.RootElement.TryGetProperty("features", out var featuresElement) && featuresElement.ValueKind == JsonValueKind.Array)
                    {
                        int count = 0;
                        foreach (var f in featuresElement.EnumerateArray())
                        {
                            var node = JsonNode.Parse(f.GetRawText());
                            if (node != null)
                            {
                                allFeatures.Add(node);
                                count++;
                            }
                        }
                        _logger.LogInformation("PdokSewageWfsService: Parsed {Count} features for '{TypeName}'.", count, typeName);
                    }
                }
                else
                {
                    _logger.LogWarning("PdokSewageWfsService: Query for '{TypeName}' returned HTTP status {Status}", typeName, response.StatusCode);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "PdokSewageWfsService: Error fetching featureType '{TypeName}'", typeName);
            }
        }

        var unifiedCollection = new JsonObject
        {
            ["type"] = "FeatureCollection",
            ["name"] = "StedelijkWaterSewageNetwork",
            ["crs"] = new JsonObject
            {
                ["type"] = "name",
                ["properties"] = new JsonObject
                {
                    ["name"] = "urn:ogc:def:crs:OGC:1.3:CRS84"
                }
            },
            ["features"] = allFeatures
        };

        string unifiedGeoJson = unifiedCollection.ToJsonString();
        _logger.LogInformation("PdokSewageWfsService: Successfully generated unified GWSW Sewage GeoJSON ({Count} features, {Length} bytes).", allFeatures.Count, unifiedGeoJson.Length);
        return unifiedGeoJson;
    }
}
