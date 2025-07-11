using UnityEngine;
using UnityEngine.Serialization;
using Utility;

namespace DCL.Rendering.GPUInstancing
{
    [CreateAssetMenu(fileName = nameof(GPUInstancingSettings), menuName = "DCL/" + nameof(GPUInstancingSettings), order = 0)]
    public class GPUInstancingSettings : ScriptableObject
    {
        public const float SCENE_DIST_MIN = 20f;
        public const float SCENE_DIST_MAX = 300f;

        public ComputeShader FrustumCullingAndLODGenComputeShader;
        public ComputeShader IndirectBufferGenerationComputeShader;
        public ComputeShader DrawArgsInstanceCountTransferComputeShader;

        [Range(SCENE_DIST_MIN, SCENE_DIST_MAX)]
        public float RenderDistanceInParcels = 70f;

        public float RoadsSceneDistance() =>
            Mathf.Clamp(RenderDistanceInParcels, SCENE_DIST_MIN, SCENE_DIST_MAX) * ParcelMathHelper.PARCEL_SIZE;
    }
}
