using System.Collections.Generic;
using System.IO;
using DCL.Roads.Settings;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;

public static class RoadParser
{
    [MenuItem("Decentraland/Roads/ParseRoadsFiles")]
    private static void ParseParcelFiles()
    {
        ParseAndSave();
    }

    public static Quaternion StringToQuaternion(string sQuaternion)
    {
        // Remove the parentheses
        if (sQuaternion.StartsWith("(") && sQuaternion.EndsWith(")")) { sQuaternion = sQuaternion.Substring(1, sQuaternion.Length - 2); }

        // split the items
        string[] sArray = sQuaternion.Split(',');

        // store as a Vector3
        var result = new Quaternion(
            float.Parse(sArray[0]),
            float.Parse(sArray[1]),
            float.Parse(sArray[2]),
            float.Parse(sArray[3]));

        return result;
    }

    public static Vector2Int StringToVector2Int(string vector2Int)
    {
        // split the items
        string[] sArray = vector2Int.Split(',');

        // store as a Vector3
        var result = new Vector2Int(
            int.Parse(sArray[0]),
            int.Parse(sArray[1]));

        return result;
    }

    private static void ParseAndSave()
    {
        var path = "Assets/DCL/Roads/Data/";
        TextAsset roadsData = AssetDatabase.LoadAssetAtPath<TextAsset>($"{path}SingleParcelRoadInfo.json");

        Dictionary<string, RoadRawInfo> modelDictionary = JsonConvert.DeserializeObject<Dictionary<string, RoadRawInfo>>(roadsData.text);
        var roadsDescription = new List<RoadDescription>();

        foreach (KeyValuePair<string, RoadRawInfo> entry in modelDictionary)
        {
            RoadRawInfo roadRawInfo = entry.Value;
            roadsDescription.Add(new RoadDescription(StringToVector2Int(entry.Key), Path.GetFileNameWithoutExtension(roadRawInfo.Model), StringToQuaternion(roadRawInfo.Rotation)));
        }

        RoadSettingsAsset roadSettingsAsset = ScriptableObject.CreateInstance<RoadSettingsAsset>();
        roadSettingsAsset.RoadDescriptions = roadsDescription;

        AssetDatabase.CreateAsset(roadSettingsAsset, path + "RoadData.asset");
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"ROAD DATA SAVED SUCCESFULLY WITH {roadSettingsAsset.RoadDescriptions.Count} entries!");
    }
}

public class RoadRawInfo
{
    [JsonProperty("model")]
    public string Model { get; set; }

    [JsonProperty("position")]
    public string Position { get; set; }

    [JsonProperty("rotation")]
    public string Rotation { get; set; }

    [JsonProperty("scale")]
    public string Scale { get; set; }
}
