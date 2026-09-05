using UnityEditor;
using UnityEngine;

namespace Editor
{
    public class AssetDatabaseTools
    {
        [MenuItem("Decentraland/Asset Database/Force Reserialize ALL Assets")]
        static void ForceReserializeAssets()
        {
            bool proceed = EditorUtility.DisplayDialog(
                "Warning",
                "This operation will force Unity to reserialize all assets in your project. " +
                "This might take a long time for large projects and cannot be canceled once started. " +
                "\n\nDo you want to continue?",
                "Yes, Proceed",
                "Cancel"
            );

            if (proceed)
            {
                Debug.Log("Starting asset reserialization...");
                AssetDatabase.ForceReserializeAssets();
                Debug.Log("Asset reserialization completed.");
            }
            else
            {
                Debug.Log("Asset reserialization canceled by user.");
            }
        }
    }
}
