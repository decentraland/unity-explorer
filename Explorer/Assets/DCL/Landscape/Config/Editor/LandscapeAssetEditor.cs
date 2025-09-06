using UnityEditor;
using UnityEngine;
using UnityEngine.Pool;

namespace DCL.Landscape.Config.Editor
{
    [CustomEditor(typeof(LandscapeAsset)), CanEditMultipleObjects]
    public sealed class LandscapeAssetEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();

            if (GUILayout.Button("Create or Update Collider"))
                foreach (Object landscapeAsset in targets)
                    CreateOrUpdateCollider((LandscapeAsset)landscapeAsset);
        }

        private static void CreateOrUpdateCollider(LandscapeAsset landscapeAsset)
        {
            GameObject instance = Object.Instantiate(landscapeAsset.asset);

            instance.name = landscapeAsset.Collider != null
                ? landscapeAsset.Collider.name
                : landscapeAsset.asset.name;

            bool hasColliders = false;

            using (ListPool<Component>.Get(out var components))
            {
                instance.GetComponentsInChildren(true, components);

                foreach (var component in components)
                {
                    if (component is Transform)
                        continue;

                    if (component is Collider)
                        hasColliders = true;
                    else
                        Object.DestroyImmediate(component);
                }

                if (hasColliders)
                {
                    foreach (var component in components)
                    {
                        if (component != null && component is Transform
                                              && component.GetComponentInChildren<Collider>() == null)
                            Object.DestroyImmediate(component.gameObject);
                    }

                    string colliderAssetPath = landscapeAsset.Collider != null
                        ? AssetDatabase.GetAssetPath(landscapeAsset.Collider)
                        : $"Assets/{instance.name}.prefab";

                    landscapeAsset.Collider = PrefabUtility.SaveAsPrefabAsset(instance,
                        colliderAssetPath);
                }
                else
                    landscapeAsset.Collider = null;
            }

            Object.DestroyImmediate(instance);

            EditorUtility.SetDirty(landscapeAsset);
            AssetDatabase.SaveAssetIfDirty(landscapeAsset);
        }
    }
}
