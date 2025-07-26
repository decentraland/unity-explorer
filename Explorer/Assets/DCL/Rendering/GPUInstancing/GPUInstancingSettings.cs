using System;
using UnityEngine;
using Utility;

namespace DCL.Rendering.GPUInstancing
{
    [CreateAssetMenu(fileName = nameof(GPUInstancingSettings), menuName = "DCL/" + nameof(GPUInstancingSettings), order = 0)]
    public class GPUInstancingSettings : ScriptableObject
    {
        private const int SCENE_DIST_MIN = 20;
        private const int SCENE_DIST_MAX = 150;

        public ComputeShader FrustumCullingAndLODGenComputeShader;
        public ComputeShader IndirectBufferGenerationComputeShader;
        public ComputeShader DrawArgsInstanceCountTransferComputeShader;

        [field: Range(SCENE_DIST_MIN, SCENE_DIST_MAX)]
        [field: SerializeField]
        public int RenderDistanceInParcels { get; set; } = 70;

        public int RoadsSceneDistance() =>
            Math.Clamp(RenderDistanceInParcels, SCENE_DIST_MIN, SCENE_DIST_MAX) * ParcelMathHelper.PARCEL_SIZE;
    }
}
