using GPUInstancerPro;
using UnityEditor;
using UnityEngine;

namespace Editor
{
    // Every GPUIProfile used by the landscape's trees/rocks/detail decorations has occlusion
    // culling enabled - a GPU depth-buffer heuristic tuned for normal ground-level gameplay
    // camera angles. Our map-capture camera looks straight down from high altitude, an angle
    // this system was never tuned/tested for; it's the leading suspect for objects vanishing
    // ("deloading") at capture distance/angle even though they're clearly unoccluded from above.
    // Disable occlusion culling and widen the distance-culling range for every profile so nothing
    // in frame gets incorrectly hidden during capture builds.
    public static class FixGPUIOcclusionCulling
    {
        public static void Fix()
        {
            string[] guids = AssetDatabase.FindAssets("t:GPUIProfile");
            int changed = 0;

            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var profile = AssetDatabase.LoadAssetAtPath<GPUIProfile>(path);

                if (profile == null)
                    continue;

                bool dirty = false;

                if (profile.isOcclusionCulling)
                {
                    profile.isOcclusionCulling = false;
                    dirty = true;
                }

                if (profile.isShadowOcclusionCulling)
                {
                    profile.isShadowOcclusionCulling = false;
                    dirty = true;
                }

                if (profile.minMaxDistance.y < 5000f)
                {
                    profile.minMaxDistance = new Vector2(profile.minMaxDistance.x, 5000f);
                    dirty = true;
                }

                if (dirty)
                {
                    EditorUtility.SetDirty(profile);
                    changed++;
                    Debug.Log($"[FixGPUIOcclusionCulling] Patched {path}");
                }
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"[FixGPUIOcclusionCulling] Done. Found {guids.Length} profiles, patched {changed}.");
        }
    }
}
