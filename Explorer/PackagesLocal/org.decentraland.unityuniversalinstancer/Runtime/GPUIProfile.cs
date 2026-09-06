// ScriptableObject describing per-prototype rendering parameters. DCL only touches
// minMaxDistance and SetParameterBufferData (REQ-031). The script GUID is pinned in
// the .meta file (1ec256bd8e4c92f48b9b9221cc99020f) so existing .asset files
// continue to deserialise (REQ-032).
//
// The full field list authored in the .asset files (per
// docs/gpui-requirements.md §2.2) is declared here so every authored value
// binds to a real member and YAML round-trips cleanly without dropping data.

using UnityEngine;

namespace GPUInstancerPro
{
    public class GPUIProfile : ScriptableObject
    {
        // LandscapeData.cs:38 — the only field DCL writes to.
        public Vector2 minMaxDistance;

        // The rest of the authored .asset shape. ProfileSnapshot resolves every
        // culling, shadow, LOD, cross-fade and occlusion field into the values
        // TreeRendererService and CullAndLODJob draw with; enablePerObjectMotionVectors
        // and isDefaultProfile only round-trip.
        public int isShadowCasting = 1;
        public int isDistanceCulling = 1;
        public int isFrustumCulling = 1;
        public int isOcclusionCulling = 1;
        public int isShadowFrustumCulling;
        public int isShadowOcclusionCulling;
        public int isShadowDistanceCulling = 1;
        public int isLODCrossFade = 1;
        public int isAnimateCrossFade = 1;
        public float minCullingDistance;
        public float minShadowCullingDistance = 20f;
        public float frustumOffset = 0.1f;
        public float occlusionOffset = 0.0001f;
        public float occlusionOffsetSizeMultiplier;
        public float shadowFrustumOffset = 10f;
        public float shadowOcclusionOffset = 0.0001f;
        public float shadowOcclusionOffsetSizeMultiplier = 0.5f;
        public int occlusionAccuracy = 3;
        public Vector3 boundsOffset;
        public float lodBiasAdjustment = 1f;
        public float customShadowDistance;
        public int[] shadowLODMap = { 0, 1, 2, 3, 4, 5, 6, 7 };
        public float lodCrossFadeTransitionWidth = 0.1f;
        public float lodCrossFadeAnimateSpeed = 4f;
        public int maximumLODLevel;
        public int enablePerObjectMotionVectors;
        public int isDefaultProfile;

        /// <summary>
        /// LandscapeData.cs:39 — flushes the cached fields onto every renderer
        /// bound to this profile. Caller-explicit to match GPUI semantics (REQ-013):
        /// mutating <see cref="minMaxDistance"/> alone does NOT propagate.
        /// </summary>
        public void SetParameterBufferData()
        {
            GPUICoreAPI.NotifyProfileFlushed(this);
        }
    }
}
