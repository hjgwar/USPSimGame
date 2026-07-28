using System.Text.Json;

namespace USPSimGame.Utils;

public static class GeoJsonSpatialUtils
{
    public struct Point2D
    {
        public double X { get; set; }
        public double Y { get; set; }
        public Point2D(double x, double y) { X = x; Y = y; }
    }

    public static List<List<Point2D>> ExtractRings(string? geoJson)
    {
        var result = new List<List<Point2D>>();
        if (string.IsNullOrWhiteSpace(geoJson)) return result;

        try
        {
            using var doc = JsonDocument.Parse(geoJson);
            var root = doc.RootElement;
            ProcessElement(root, result);
        }
        catch { }

        return result;
    }

    private static void ProcessElement(JsonElement element, List<List<Point2D>> result)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            if (element.TryGetProperty("type", out var typeProp))
            {
                var type = typeProp.GetString();
                if (type == "FeatureCollection" && element.TryGetProperty("features", out var feats))
                {
                    foreach (var f in feats.EnumerateArray()) ProcessElement(f, result);
                }
                else if (type == "Feature" && element.TryGetProperty("geometry", out var geom))
                {
                    ProcessElement(geom, result);
                }
                else if ((type == "Polygon" || type == "MultiPolygon" || type == "LineString" || type == "MultiLineString" || type == "Point" || type == "MultiPoint") &&
                         element.TryGetProperty("coordinates", out var coords))
                {
                    ExtractCoordsFromGeometry(type, coords, result);
                }
            }
        }
    }

    private static void ExtractCoordsFromGeometry(string geomType, JsonElement coords, List<List<Point2D>> result)
    {
        if (geomType == "Point")
        {
            var pt = ParsePoint(coords);
            if (pt.HasValue)
            {
                // Create small micro-buffer square ring around point for spatial checks
                double d = 0.0001; // ~10m
                result.Add(new List<Point2D>
                {
                    new Point2D(pt.Value.X - d, pt.Value.Y - d),
                    new Point2D(pt.Value.X + d, pt.Value.Y - d),
                    new Point2D(pt.Value.X + d, pt.Value.Y + d),
                    new Point2D(pt.Value.X - d, pt.Value.Y + d),
                    new Point2D(pt.Value.X - d, pt.Value.Y - d)
                });
            }
        }
        else if (geomType == "MultiPoint")
        {
            var pts = ParsePointList(coords);
            double d = 0.0001;
            foreach (var pt in pts)
            {
                result.Add(new List<Point2D>
                {
                    new Point2D(pt.X - d, pt.Y - d),
                    new Point2D(pt.X + d, pt.Y - d),
                    new Point2D(pt.X + d, pt.Y + d),
                    new Point2D(pt.X - d, pt.Y + d),
                    new Point2D(pt.X - d, pt.Y - d)
                });
            }
        }
        else if (geomType == "LineString")
        {
            var line = ParsePointList(coords);
            if (line.Count > 0)
            {
                result.Add(line);
            }
        }
        else if (geomType == "MultiLineString")
        {
            foreach (var lineElement in coords.EnumerateArray())
            {
                var line = ParsePointList(lineElement);
                if (line.Count > 0) result.Add(line);
            }
        }
        else if (geomType == "Polygon")
        {
            foreach (var ringElement in coords.EnumerateArray())
            {
                var ring = ParsePointList(ringElement);
                if (ring.Count > 0) result.Add(ring);
            }
        }
        else if (geomType == "MultiPolygon")
        {
            foreach (var polyElement in coords.EnumerateArray())
            {
                foreach (var ringElement in polyElement.EnumerateArray())
                {
                    var ring = ParsePointList(ringElement);
                    if (ring.Count > 0) result.Add(ring);
                }
            }
        }
    }

    private static Point2D? ParsePoint(JsonElement el)
    {
        if (el.ValueKind == JsonValueKind.Array && el.GetArrayLength() >= 2)
        {
            double x = el[0].GetDouble();
            double y = el[1].GetDouble();
            return new Point2D(x, y);
        }
        return null;
    }

    private static List<Point2D> ParsePointList(JsonElement el)
    {
        var list = new List<Point2D>();
        if (el.ValueKind == JsonValueKind.Array)
        {
            foreach (var ptEl in el.EnumerateArray())
            {
                var pt = ParsePoint(ptEl);
                if (pt.HasValue) list.Add(pt.Value);
            }
        }
        return list;
    }

    public static bool DoRingsIntersect(List<List<Point2D>> ringsA, List<List<Point2D>> ringsB)
    {
        if (ringsA.Count == 0 || ringsB.Count == 0) return false;

        // 1. Ray-casting test: Any point of A in B, or any point of B in A
        foreach (var rA in ringsA)
        {
            foreach (var ptA in rA)
            {
                foreach (var rB in ringsB)
                {
                    if (IsPointInRing(ptA, rB)) return true;
                }
            }
        }

        foreach (var rB in ringsB)
        {
            foreach (var ptB in rB)
            {
                foreach (var rA in ringsA)
                {
                    if (IsPointInRing(ptB, rA)) return true;
                }
            }
        }

        // 2. Line segment cross test
        foreach (var rA in ringsA)
        {
            if (rA.Count < 2) continue;
            for (int i = 0; i < rA.Count - 1; i++)
            {
                var p1 = rA[i];
                var p2 = rA[i + 1];
                foreach (var rB in ringsB)
                {
                    if (rB.Count < 2) continue;
                    for (int j = 0; j < rB.Count - 1; j++)
                    {
                        var p3 = rB[j];
                        var p4 = rB[j + 1];
                        if (SegmentsCross(p1, p2, p3, p4)) return true;
                    }
                }
            }
        }

        return false;
    }

    public static bool IsPointInRing(Point2D pt, List<Point2D> ring)
    {
        if (ring.Count < 3) return false;
        double x = pt.X, y = pt.Y;
        bool inside = false;

        for (int i = 0, j = ring.Count - 1; i < ring.Count; j = i++)
        {
            double xi = ring[i].X, yi = ring[i].Y;
            double xj = ring[j].X, yj = ring[j].Y;

            bool intersect = ((yi > y) != (yj > y)) &&
                (x < (xj - xi) * (y - yi) / (yj - yi + 1e-10) + xi);
            if (intersect) inside = !inside;
        }

        return inside;
    }

    private static bool SegmentsCross(Point2D p1, Point2D p2, Point2D p3, Point2D p4)
    {
        bool Ccw(Point2D a, Point2D b, Point2D c) =>
            (c.Y - a.Y) * (b.X - a.X) > (b.Y - a.Y) * (c.X - a.X);

        return (Ccw(p1, p3, p4) != Ccw(p2, p3, p4)) && (Ccw(p1, p2, p3) != Ccw(p1, p2, p4));
    }
}
