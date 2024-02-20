using System.Collections.Generic;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;

namespace DCL.Landscape.Config.Editor
{
    public static class ParcelParser
    {
        [MenuItem("Decentraland/ParseParcelFiles")]
        private static void ParseParcelFiles()
        {
            ParseAndSave();
        }

        private static void ParseAndSave()
        {
            // Load the text files
            var path = "Assets/DCL/Landscape/Data/";
            TextAsset ownedParcelsData = AssetDatabase.LoadAssetAtPath<TextAsset>($"{path}OwnedParcels.txt");
            TextAsset emptyParcelsData = AssetDatabase.LoadAssetAtPath<TextAsset>($"{path}EmptyParcels.txt");

            if (ownedParcelsData == null || emptyParcelsData == null)
            {
                Debug.LogError("Failed to load parcel data files!");
                return;
            }

            // Create a new instance of the ScriptableObject
            ParcelData parcelData = ScriptableObject.CreateInstance<ParcelData>();
            var ownedParcels = new List<int2>();
            var emptyParcels = new List<int2>();

            // Parse and save owned parcels
            string[] ownedParcelsRaw = ownedParcelsData.text.Split('\n');

            foreach (string ownedParcel in ownedParcelsRaw)
            {
                string[] coordinates = ownedParcel.Trim().Split(',');

                if (TryParse(coordinates, out int x, out int y))
                    ownedParcels.Add(new int2(x, y));
                else
                    Debug.LogWarning("Invalid line: " + ownedParcel);
            }

            // Parse and save empty parcels
            string[] emptyParcelsRaw = emptyParcelsData.text.Split('\n');

            foreach (string emptyParcel in emptyParcelsRaw)
            {
                string[] coordinates = emptyParcel.Trim().Split(',');

                if (TryParse(coordinates, out int x, out int y))
                    emptyParcels.Add(new int2(x, y));
                else
                    Debug.LogWarning("Invalid line: " + emptyParcel);
            }

            parcelData.ownedParcels = ownedParcels.ToArray();
            parcelData.emptyParcels = emptyParcels.ToArray();

            // Save the ScriptableObject asset
            AssetDatabase.CreateAsset(parcelData, path + "ParsedParcels.asset");
            AssetDatabase.SaveAssets();

            Debug.Log("Parcel data saved at: " + path);
        }

        private static bool TryParse(string[] coords, out int x, out int y)
        {
            x = 0;
            y = 0;
            return coords.Length == 2 && int.TryParse(coords[0], out x) && int.TryParse(coords[1], out y);
        }
    }
}
