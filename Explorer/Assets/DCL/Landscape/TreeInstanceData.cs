using DCL.Landscape.Config;
using Unity.Mathematics;
using UnityEngine;
using static Unity.Mathematics.math;

namespace DCL.Landscape
{
    public readonly struct TreeInstanceData
    {
        public readonly byte PrototypeIndex;
        public readonly byte PositionX;
        public readonly byte PositionZ;
        public readonly byte RotationY;
        public readonly byte ScaleXZ;
        public readonly byte ScaleY;

#if UNITY_EDITOR
        public TreeInstanceData(UnityEngine.TreeInstance instance, Vector3 position,
            int parcelSize, LandscapeAsset[] prototypes)
        {
            PrototypeIndex = (byte)instance.prototypeIndex;
            float2 fracPosition = frac(float3(position).xz / parcelSize);
            PositionX = (byte)round(fracPosition.x * 255f);
            PositionZ = (byte)round(fracPosition.y * 255f);
            RotationY = (byte)round(instance.rotation * (255f / PI2));
            prototypes[PrototypeIndex].randomization.GetScaleRange(out float2 min, out float2 max);
            ScaleXZ = (byte)round((instance.widthScale - min.x) / (max.x - min.x) * 255f);
            ScaleY = (byte)round((instance.heightScale - min.y) / (max.y - min.y) * 255f);
        }
#endif
    }
}
