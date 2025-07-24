using System;
using System.Collections.Generic;
using UnityEngine;

namespace DCL.Rendering.GPUInstancing.InstancingData
{
    [Serializable]
    public struct LODGroupData : IEquatable<LODGroupData>
    {
        private const int MAX_LODS_LEVEL = 8;
        private const float DITHER_OVERLAP_FACTOR = 0.20f;

        private readonly float[] lodsScreenSpaceSizes;
        private readonly float objectSize;

        public Matrix4x4 LODSizesMatrix;
        public Bounds Bounds;

        public int LODCount => lodsScreenSpaceSizes?.Length ?? 1;

        public LODGroupData(Bounds sharedMeshBounds)
        {
            Bounds = new Bounds();
            Bounds.Encapsulate(sharedMeshBounds);
            objectSize = Mathf.Max(Bounds.size.x, Bounds.size.y, Bounds.size.z);
            lodsScreenSpaceSizes = new[] { 0.0f }; // Single LOD with maximum visibility
            LODSizesMatrix = new Matrix4x4();
            BuildLODMatrix(1);
        }

        public LODGroupData(LODGroup lodGroupBehaviour, LOD[] lods, IReadOnlyList<CombinedLodsRenderer> combinedLodsRenderers)
        {
            Bounds = combinedLodsRenderers[0].CombinedMesh.bounds;

            for (var i = 1; i < combinedLodsRenderers.Count; i++)
                Bounds.Encapsulate(combinedLodsRenderers[i].CombinedMesh.bounds);

            objectSize = lodGroupBehaviour.size;

            lodsScreenSpaceSizes = new float[lods.Length];
            for (var i = 0; i < lods.Length && i < MAX_LODS_LEVEL; i++)
                lodsScreenSpaceSizes[i] = lods[i].screenRelativeTransitionHeight;

            LODSizesMatrix = new Matrix4x4();
            BuildLODMatrix(lods.Length);
        }

        private void BuildLODMatrix(int lodsLength)
        {
            var rowEnd = 0;
            var col = 0;

            for (var i = 0; i < lodsLength && i < MAX_LODS_LEVEL; i++)
            {
                float endValue = lodsScreenSpaceSizes[i];
                float startValue;

                if (i == 0)
                    startValue = 1f;
                else
                {
                    float prevEnd = lodsScreenSpaceSizes[i - 1];
                    float difference = prevEnd - endValue;
                    float overlap = difference * DITHER_OVERLAP_FACTOR;

                    startValue = prevEnd + overlap;
                }

                // Write [startValue, endValue] into LODSizesMatrix.
                //    The pattern:
                //      - row0 & row1 for 'start'
                //      - row2 & row3 for 'end'
                //    i < 4 => row0 & row2, i >= 4 => row1 & row3
                int rowStart = i < 4 ? 0 : 1;
                rowEnd = rowStart + 2; // 2 or 3
                col = i % 4;

                LODSizesMatrix[rowStart, col] = startValue;
                LODSizesMatrix[rowEnd, col] = endValue;
            }

            LODSizesMatrix[rowEnd, col] = 0; // zero for the end of last LOD
        }

        public override int GetHashCode()
        {
            var hashCode = new HashCode();
            hashCode.Add(LODCount);
            hashCode.Add(objectSize);
            hashCode.Add(Bounds);
            hashCode.Add(LODSizesMatrix);
            return hashCode.ToHashCode();
        }

        public bool Equals(LODGroupData other)
        {
            const float EPS = 0.001f;

            if (LODCount != other.LODCount) return false;
            if (Math.Abs(objectSize - other.objectSize) > EPS) return false;
            if (!Bounds.Equals(other.Bounds)) return false;
            if (!LODSizesMatrix.Equals(other.LODSizesMatrix)) return false;

            return true;
        }
    }
}
