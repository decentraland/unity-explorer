using System;
using System.Collections.Generic;
using UnityEngine;

namespace DCL.Rendering.GPUInstancing.InstancingData
{
    [Serializable]
    public class LODGroupData : IEquatable<LODGroupData>
    {
        private const int MAX_LODS_LEVEL = 8;
        private const float DITHER_OVERLAP_FACTOR = 0.20f;

        private readonly float[] lodsScreenSpaceSizes;
        private readonly float objectSize;

        public Matrix4x4 LODSizesMatrix;
        public Bounds Bounds;

        public int LODCount => lodsScreenSpaceSizes.Length;

        public LODGroupData(Bounds sharedMeshBounds)
        {
            UpdateBounds(sharedMeshBounds);
            objectSize = Mathf.Max(Bounds.size.x, Bounds.size.y, Bounds.size.z);
            lodsScreenSpaceSizes = new[] { 0.0f }; // Single LOD with maximum visibility
            BuildLODMatrix(1);
        }

        public LODGroupData(LODGroup lodGroupBehaviour, LOD[] lods)
        {
            objectSize = lodGroupBehaviour.size;

            lodsScreenSpaceSizes = new float[lods.Length];

            for (var i = 0; i < lods.Length && i < MAX_LODS_LEVEL; i++)
                lodsScreenSpaceSizes[i] = lods[i].screenRelativeTransitionHeight;

            BuildLODMatrix(lods.Length);
        }

        public void UpdateBounds(IReadOnlyList<CombinedLodsRenderer> combinedLodsRenderers)
        {
            Bounds = combinedLodsRenderers[0].CombinedMesh.bounds;

            for (var i = 1; i < combinedLodsRenderers.Count; i++)
                Bounds.Encapsulate(combinedLodsRenderers[i].CombinedMesh.bounds);
        }

        public void UpdateBounds(Bounds sharedMeshBounds)
        {
            Bounds = new Bounds();
            Bounds.Encapsulate(sharedMeshBounds);
        }

        private void BuildLODMatrix(int lodsLength)
        {
            LODSizesMatrix = new Matrix4x4();

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
            hashCode.Add(objectSize);

            if (lodsScreenSpaceSizes != null)
                foreach (float size in lodsScreenSpaceSizes)
                    hashCode.Add(size);

            return hashCode.ToHashCode();
        }

        public bool Equals(LODGroupData other)
        {
            const float EPS = 0.001f;

            if (other == null) return false;
            if (ReferenceEquals(this, other)) return true;

            if (Math.Abs(objectSize - other.objectSize) > EPS) return false;

            if (lodsScreenSpaceSizes == null || other.lodsScreenSpaceSizes == null || lodsScreenSpaceSizes.Length != other.lodsScreenSpaceSizes.Length)
                return false;

            for (var i = 0; i < lodsScreenSpaceSizes.Length; i++)
                if (Math.Abs(lodsScreenSpaceSizes[i] - other.lodsScreenSpaceSizes[i]) > EPS)
                    return false;

            return true;
        }

        public override bool Equals(object obj) =>
            Equals(obj as LODGroupData);

        public static bool operator ==(LODGroupData left, LODGroupData right)
        {
            if (ReferenceEquals(left, right)) return true;
            return left is not null && left.Equals(right);
        }

        public static bool operator !=(LODGroupData left, LODGroupData right) =>
            !(left == right);
    }
}
