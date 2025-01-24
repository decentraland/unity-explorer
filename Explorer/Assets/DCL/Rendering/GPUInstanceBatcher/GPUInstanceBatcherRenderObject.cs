using System;
using UnityEngine;

namespace DCL.Rendering.GPUInstanceBatcher
{
    [Serializable]
    public abstract class GPUInstanceBatcherRenderObject : ScriptableObject
    {
        public GameObject prefabObject;

        // Shadows
        public bool bShadowCasting = true;
        public bool useCustomShadowDistance = false;
        public float shadowDistance = 0;
        public float[] shadowLODMap = new float[] {
            0, 4, 0, 0,
            1, 5, 0, 0,
            2, 6, 0, 0,
            3, 7, 0, 0};
        public bool useOriginalShaderForShadow = false;
        public bool cullShadows = false;

        // Culling
        public float minDistance = 0;
        public float maxDistance = 500;
        public bool isFrustumCulling = true;
        public bool isOcclusionCulling = true;
        public float frustumOffset = 0.2f;
        public float minCullingDistance = 0;
        public float occlusionOffset = 0;
        public int occlusionAccuracy = 1;

        // Bounds
        public Vector3 boundsOffset;

        // LOD
        [Range(0.01f, 1.0f)]
        public float lodFadeTransitionWidth = 0.1f;
        public float lodBiasAdjustment = 1;
        public float[] lodScreenSpaceSizesMatrix = new float[] {
            0, 0, 0, 0,
            0, 0, 0, 0,
            0, 0, 0, 0,
            0, 0, 0, 0};

        public override string ToString()
        {
            if (prefabObject != null)
                return prefabObject.name;
            return name;
        }

        public virtual Texture2D GetPreviewTexture()
        {
            return null;
        }
    }
}
