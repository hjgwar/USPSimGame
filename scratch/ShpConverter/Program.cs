using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using NetTopologySuite.Geometries;
using NetTopologySuite.IO;

namespace ShpConverter;

class Program
{
    record ConvertJob(string ZipPath, string ShpName, string OutputPath, string IdPrefix, string GmlPrefix);

    static void Main(string[] args)
    {
        string projectRoot = @"C:\Users\harald.warmelink\Documents\USPSimGame";
        string downloadsDir = @"C:\Users\harald.warmelink\Downloads";
        string scratchDir = Path.Combine(projectRoot, "scratch");

        var jobs = new List<ConvertJob>
        {
            new ConvertJob(
                Path.Combine(downloadsDir, "Laagspanningsverbindingen.zip"),
                "Laagspanningsverbindingen.shp",
                Path.Combine(projectRoot, "Data", "Layers", "stedin-laagspanning.json"),
                "stedin_ls",
                "Stedin_Laagspanning"
            ),
            new ConvertJob(
                Path.Combine(downloadsDir, "MiddenLaagspanningsstations.zip"),
                "MiddenLaagspanningsstations.shp",
                Path.Combine(projectRoot, "Data", "Layers", "stedin-middenlaagspanningsstations.json"),
                "stedin_mls",
                "Stedin_Middenlaagspanningsstation"
            ),
            new ConvertJob(
                Path.Combine(downloadsDir, "Hoogspanningsverbindingen.zip"),
                "Hoogspanningsverbindingen.shp",
                Path.Combine(projectRoot, "Data", "Layers", "stedin-hoogspanningsverbindingen.json"),
                "stedin_hs",
                "Stedin_Hoogspanning"
            )
        };

        foreach (var job in jobs)
        {
            Console.WriteLine($"==================================================");
            Console.WriteLine($"Processing Job: {job.ShpName}");
            Console.WriteLine($"Zip Path: {job.ZipPath}");
            Console.WriteLine($"Output Path: {job.OutputPath}");

            if (!File.Exists(job.ZipPath))
            {
                Console.WriteLine($"WARNING: Zip file not found at {job.ZipPath}. Skipping job.");
                continue;
            }

            string extractDir = Path.Combine(scratchDir, "shp_temp_" + Path.GetFileNameWithoutExtension(job.ShpName));
            if (Directory.Exists(extractDir)) Directory.Delete(extractDir, true);
            Directory.CreateDirectory(extractDir);

            try
            {
                Console.WriteLine($"Extracting {job.ZipPath} to {extractDir}...");
                System.IO.Compression.ZipFile.ExtractToDirectory(job.ZipPath, extractDir, true);

                string shpPath = Path.Combine(extractDir, job.ShpName);
                if (!File.Exists(shpPath))
                {
                    // Search recursively in extractDir if nested
                    var foundFiles = Directory.GetFiles(extractDir, job.ShpName, SearchOption.AllDirectories);
                    if (foundFiles.Length > 0) shpPath = foundFiles[0];
                }

                if (!File.Exists(shpPath))
                {
                    Console.WriteLine($"ERROR: Shapefile {job.ShpName} not found in zip extract!");
                    continue;
                }

                ConvertShapeFileToGeoJson(shpPath, job.OutputPath, job.IdPrefix, job.GmlPrefix);
            }
            finally
            {
                if (Directory.Exists(extractDir))
                {
                    try { Directory.Delete(extractDir, true); } catch { }
                }
            }
        }

        Console.WriteLine("\nAll conversion jobs completed!");
    }

    private static void ConvertShapeFileToGeoJson(string shpPath, string outputPath, string idPrefix, string gmlPrefix)
    {
        // Target: Utrecht Science Park Center RD (X = 140880, Y = 455430), Radius 1.5 km
        double minX = 139380;
        double maxX = 142380;
        double minY = 453930;
        double maxY = 456930;

        int totalRead = 0;
        int matched = 0;

        string? outDir = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrEmpty(outDir) && !Directory.Exists(outDir))
        {
            Directory.CreateDirectory(outDir);
        }

        using (var writer = new StreamWriter(outputPath, false, Encoding.UTF8))
        {
            writer.WriteLine("{");
            writer.WriteLine("  \"type\": \"FeatureCollection\",");
            writer.WriteLine("  \"crs\": { \"type\": \"name\", \"properties\": { \"name\": \"EPSG:4326\" } },");
            writer.WriteLine("  \"features\": [");

            bool first = true;

            using (var reader = new ShapefileDataReader(shpPath, new GeometryFactory()))
            {
                var header = reader.DbaseHeader;
                Console.WriteLine($"ShapeFile Record Count: {header.NumRecords}");

                while (reader.Read())
                {
                    totalRead++;
                    if (totalRead % 500000 == 0)
                    {
                        Console.WriteLine($"Processed {totalRead} / {header.NumRecords} records (Matched: {matched})...");
                    }

                    var geom = reader.Geometry;
                    if (geom == null) continue;

                    var env = geom.EnvelopeInternal;
                    if (env.MaxX < minX || env.MinX > maxX || env.MaxY < minY || env.MinY > maxY)
                    {
                        continue; // Skip features outside Utrecht Science Park area
                    }

                    matched++;

                    if (!first)
                    {
                        writer.WriteLine(",");
                    }
                    first = false;

                    // Read DBF properties
                    var propsSb = new StringBuilder();
                    propsSb.Append("{");
                    propsSb.Append($"\"GmlID\":\"{gmlPrefix}.{matched}\"");
                    for (int i = 0; i < reader.FieldCount; i++)
                    {
                        string fname = reader.GetName(i);
                        object val = reader.GetValue(i);
                        if (val != null && !(val is DBNull))
                        {
                            string strVal = val.ToString() ?? "";
                            propsSb.Append($",\"{EscapeJson(fname)}\":\"{EscapeJson(strVal)}\"");
                        }
                    }
                    propsSb.Append("}");

                    string geomJson = GeometryToGeoJsonString(geom);

                    writer.Write($"    {{\"type\":\"Feature\",\"id\":\"{idPrefix}_{matched}\",\"geometry\":{geomJson},\"properties\":{propsSb.ToString()}}}");
                }
            }

            writer.WriteLine();
            writer.WriteLine("  ]");
            writer.WriteLine("}");
        }

        FileInfo fi = new FileInfo(outputPath);
        Console.WriteLine($"SUCCESS: Matched {matched} features for USP area! Saved to {outputPath} ({fi.Length / 1024} KB).");
    }

    private static string EscapeJson(string s)
    {
        return s.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", " ").Replace("\r", " ");
    }

    private static string GeometryToGeoJsonString(Geometry geom)
    {
        string geomType = geom.GeometryType;
        StringBuilder sb = new StringBuilder();

        if (geomType == "Point")
        {
            var (lat, lon) = RdToWgs84(geom.Coordinate.X, geom.Coordinate.Y);
            return $"{{\"type\":\"Point\",\"coordinates\":[{Math.Round(lon, 6).ToString(CultureInfo.InvariantCulture)},{Math.Round(lat, 6).ToString(CultureInfo.InvariantCulture)}]}}";
        }
        else if (geomType == "MultiPoint")
        {
            sb.Append("{\"type\":\"MultiPoint\",\"coordinates\":[");
            for (int i = 0; i < geom.NumGeometries; i++)
            {
                if (i > 0) sb.Append(",");
                var c = geom.GetGeometryN(i).Coordinate;
                var (lat, lon) = RdToWgs84(c.X, c.Y);
                sb.Append($"[{Math.Round(lon, 6).ToString(CultureInfo.InvariantCulture)},{Math.Round(lat, 6).ToString(CultureInfo.InvariantCulture)}]");
            }
            sb.Append("]}");
            return sb.ToString();
        }
        else if (geomType == "LineString" || geomType == "MultiLineString")
        {
            sb.Append("{\"type\":\"MultiLineString\",\"coordinates\":[");
            for (int g = 0; g < geom.NumGeometries; g++)
            {
                if (g > 0) sb.Append(",");
                sb.Append("[");
                var subGeom = geom.GetGeometryN(g);
                for (int i = 0; i < subGeom.Coordinates.Length; i++)
                {
                    if (i > 0) sb.Append(",");
                    var c = subGeom.Coordinates[i];
                    var (lat, lon) = USPSimGame.Utils.GeoCoordinateConverter.RdToWgs84(c.X, c.Y);
                    sb.Append($"[{Math.Round(lon, 6).ToString(CultureInfo.InvariantCulture)},{Math.Round(lat, 6).ToString(CultureInfo.InvariantCulture)}]");
                }
                sb.Append("]");
            }
            sb.Append("]}");
            return sb.ToString();
        }
        else if (geomType == "Polygon" || geomType == "MultiPolygon")
        {
            sb.Append("{\"type\":\"MultiPolygon\",\"coordinates\":[");
            for (int g = 0; g < geom.NumGeometries; g++)
            {
                if (g > 0) sb.Append(",");
                sb.Append("[");
                if (geom.GetGeometryN(g) is Polygon poly)
                {
                    sb.Append("[");
                    for (int i = 0; i < poly.ExteriorRing.Coordinates.Length; i++)
                    {
                        if (i > 0) sb.Append(",");
                        var c = poly.ExteriorRing.Coordinates[i];
                        var (lat, lon) = USPSimGame.Utils.GeoCoordinateConverter.RdToWgs84(c.X, c.Y);
                        sb.Append($"[{Math.Round(lon, 6).ToString(CultureInfo.InvariantCulture)},{Math.Round(lat, 6).ToString(CultureInfo.InvariantCulture)}]");
                    }
                    sb.Append("]");
                    for (int r = 0; r < poly.NumInteriorRings; r++)
                    {
                        sb.Append(",[");
                        var ring = poly.GetInteriorRingN(r);
                        for (int i = 0; i < ring.Coordinates.Length; i++)
                        {
                            if (i > 0) sb.Append(",");
                            var c = ring.Coordinates[i];
                            var (lat, lon) = USPSimGame.Utils.GeoCoordinateConverter.RdToWgs84(c.X, c.Y);
                            sb.Append($"[{Math.Round(lon, 6).ToString(CultureInfo.InvariantCulture)},{Math.Round(lat, 6).ToString(CultureInfo.InvariantCulture)}]");
                        }
                        sb.Append("]");
                    }
                }
                sb.Append("]");
            }
            sb.Append("]}");
            return sb.ToString();
        }

        sb.Append("{\"type\":\"LineString\",\"coordinates\":[");
        for (int i = 0; i < geom.Coordinates.Length; i++)
        {
            if (i > 0) sb.Append(",");
            var c = geom.Coordinates[i];
            var (lat, lon) = RdToWgs84(c.X, c.Y);
            sb.Append($"[{Math.Round(lon, 6).ToString(CultureInfo.InvariantCulture)},{Math.Round(lat, 6).ToString(CultureInfo.InvariantCulture)}]");
        }
        sb.Append("]}");
        return sb.ToString();
    }

}
}
