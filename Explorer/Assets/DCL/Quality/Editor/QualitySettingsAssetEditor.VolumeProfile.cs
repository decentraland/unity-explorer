using UnityEditor;
using UnityEngine.Rendering;

namespace DCL.Quality
{
    public partial class QualitySettingsAssetEditor
    {
        private static VolumeProfile CreateNewVolumeProfile(int index)
        {
            VolumeProfile profile = CreateInstance<VolumeProfile>();

            //profile.hideFlags = HideFlags.HideInInspector;
            profile.name = $"Custom Volume Profile {index}";

            return profile;
        }

        private void ValidateVolumeProfileAttached()
        {
            for (var i = 0; i < customSettings.arraySize; i++)
            {
                SerializedProperty asset = customSettings.GetArrayElementAtIndex(i)
                                                         .FindPropertyRelative(nameof(QualitySettingsAsset.QualityCustomLevel.volumeProfile));

                if (asset.objectReferenceValue == null)
                {
                    // Create a profile as a subasset
                    VolumeProfile newProfile = CreateNewVolumeProfile(i);

                    // Store this new effect as a subasset so we can reference it safely afterwards
                    AssetDatabase.AddObjectToAsset(newProfile, base.target);

                    asset.objectReferenceValue = newProfile;
                }
            }
        }
    }
}
