using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace DCL.Quality
{
    public partial class QualitySettingsAssetEditor
    {
        private LensFlareComponentSRP CreateNewLensFlare(int index)
        {
            string newName = "Custom Lens Flare " + index;
            var go = new GameObject(newName);
            LensFlareComponentSRP lensFlare = go.AddComponent<LensFlareComponentSRP>();

            // Save it as a prefab
            string parentPath = AssetDatabase.GetAssetPath(base.target);
            string lensFlarePath = Path.Combine(Path.GetDirectoryName(parentPath), newName + ".prefab");

            // Create SRPData and save it as a sub-asset
            LensFlareDataSRP data = CreateInstance<LensFlareDataSRP>();
            data.name = "Custom Lens Flare Data " + index;
            AssetDatabase.AddObjectToAsset(data, base.target);

            lensFlare.lensFlareData = data;

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(go, lensFlarePath);

            DestroyImmediate(go);

            return prefab.GetComponent<LensFlareComponentSRP>();
        }

        private void ValidateLensFlareAssetsAttached()
        {
            for (var i = 0; i < customSettings.arraySize; i++)
            {
                SerializedProperty asset = customSettings.GetArrayElementAtIndex(i)
                                                         .FindPropertyRelative(nameof(QualitySettingsAsset.QualityCustomLevel.lensFlareComponent));

                if (asset.objectReferenceValue == null)
                {
                    LensFlareComponentSRP newAsset = CreateNewLensFlare(i);
                    asset.objectReferenceValue = newAsset;
                }
            }
        }
    }
}
