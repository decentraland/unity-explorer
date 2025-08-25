// TerrainMetadataHelper.cs
// Separate file to completely isolate from Unity serialization

using System.Collections.Generic;

// Use a simple dictionary-based approach to avoid any Unity type conflicts
public static class TerrainMetadataHelper
{
    public static Dictionary<string, object> CreateMetadata(
        float originalMin, float originalMax, float originalAverage, float originalVariance,
        float compressionRatio, float optimizationStrength, float heightScale,
        float frequency, int octaves, int textureSizeX, int textureSizeY) =>
        new()
        {
            { "originalMin", originalMin },
            { "originalMax", originalMax },
            { "originalAverage", originalAverage },
            { "originalVariance", originalVariance },
            { "compressionRatio", compressionRatio },
            { "optimizationStrength", optimizationStrength },
            { "heightScale", heightScale },
            { "frequency", frequency },
            { "octaves", octaves },
            { "textureSizeX", textureSizeX },
            { "textureSizeY", textureSizeY },
        };

    public static float DenormalizeHeight(Dictionary<string, object> metadata, float textureValue)
    {
        var originalMin = (float)metadata["originalMin"];
        var originalMax = (float)metadata["originalMax"];
        return originalMin + (textureValue * (originalMax - originalMin));
    }

    public static float NormalizeHeight(Dictionary<string, object> metadata, float terrainValue)
    {
        var originalMin = (float)metadata["originalMin"];
        var originalMax = (float)metadata["originalMax"];
        return (terrainValue - originalMin) / (originalMax - originalMin);
    }

    public static string MetadataToJson(Dictionary<string, object> metadata)
    {
        var json = "{";
        var first = true;

        foreach (KeyValuePair<string, object> kvp in metadata)
        {
            if (!first) json += ",";
            json += $"\"{kvp.Key}\":{kvp.Value}";
            first = false;
        }

        json += "}";
        return json;
    }

    public static Dictionary<string, object> JsonToMetadata(string json)
    {
        var metadata = new Dictionary<string, object>();

        json = json.Trim('{', '}');
        string[] pairs = json.Split(',');

        foreach (string pair in pairs)
        {
            string[] keyValue = pair.Split(':');
            if (keyValue.Length != 2) continue;

            string key = keyValue[0].Trim().Trim('"');
            string value = keyValue[1].Trim();

            // Parse based on expected types
            if (key == "octaves" || key == "textureSizeX" || key == "textureSizeY")
            {
                if (int.TryParse(value, out int intValue))
                    metadata[key] = intValue;
            }
            else
            {
                if (float.TryParse(value, out float floatValue))
                    metadata[key] = floatValue;
            }
        }

        return metadata;
    }

    // Helper getters
    public static float GetOriginalMin(Dictionary<string, object> metadata) =>
        (float)metadata["originalMin"];

    public static float GetOriginalMax(Dictionary<string, object> metadata) =>
        (float)metadata["originalMax"];

    public static float GetOriginalAverage(Dictionary<string, object> metadata) =>
        (float)metadata["originalAverage"];

    public static float GetCompressionRatio(Dictionary<string, object> metadata) =>
        (float)metadata["compressionRatio"];

    public static float GetOptimizationStrength(Dictionary<string, object> metadata) =>
        (float)metadata["optimizationStrength"];
}
