using UnityEngine;
using UnityEngine.Serialization;
using Utility;

namespace DCL.Rendering.GPUInstancing
{
    [CreateAssetMenu(fileName = nameof(GPUInstancingSettings), menuName = "DCL/" + nameof(GPUInstancingSettings), order = 0)]
    public class GPUInstancingSettings : ScriptableObject
    {
        private const float SCENE_DIST_MIN = 300f;
        private const float ENV_DIST_MIN = 1000f;
        private const float ENV_DIST_MAX = 7000f;

        public ComputeShader FrustumCullingAndLODGenComputeShader;
        public ComputeShader IndirectBufferGenerationComputeShader;
        public ComputeShader DrawArgsInstanceCountTransferComputeShader;

        public float RenderDistScaleFactor = 1f;

        public float RoadsSceneDistance(float envDistance)
        {
            float normalizedEnvDistance = Mathf.InverseLerp(ENV_DIST_MIN, ENV_DIST_MAX, envDistance);
            float result = SCENE_DIST_MIN + (Mathf.Lerp(0, ParcelMathJobifiedHelper.RADIUS_HARD_LIMIT - SCENE_DIST_MIN, normalizedEnvDistance) * RenderDistScaleFactor);

            return result * ParcelMathHelper.PARCEL_SIZE;
        }
    }
}
