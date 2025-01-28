using DCL.Roads.Playground;
using System;
using System.Linq;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.Rendering;

namespace DCL.Roads.GPUInstancing.Playground
{
    [Serializable]
    public class GPUInstancedMesh
    {
        public MeshInstanceData meshInstanceData;
        public PerInstanceBuffer[] PerInstancesData;
    }

    [Serializable, StructLayout(LayoutKind.Sequential)]
    public struct PerInstanceBuffer : IEquatable<PerInstanceBuffer>
    {
        public Matrix4x4 instMatrix;
        public Vector3 instColourTint;

        public bool Equals(PerInstanceBuffer other) =>
            instMatrix.Equals(other.instMatrix) && instColourTint.Equals(other.instColourTint);

        public override bool Equals(object obj) =>
            obj is PerInstanceBuffer other && Equals(other);

        public override int GetHashCode() =>
            HashCode.Combine(instMatrix, instColourTint);

        // private const float EPSILON = 0.0001f;

        // public bool Equals(Matrix4x4 a, Matrix4x4 b) =>
        //     Mathf.Abs(a.m00 - b.m00) < EPSILON &&
        //     Mathf.Abs(a.m01 - b.m01) < EPSILON &&
        //     Mathf.Abs(a.m02 - b.m02) < EPSILON &&
        //     Mathf.Abs(a.m03 - b.m03) < EPSILON &&
        //     Mathf.Abs(a.m10 - b.m10) < EPSILON &&
        //     Mathf.Abs(a.m11 - b.m11) < EPSILON &&
        //     Mathf.Abs(a.m12 - b.m12) < EPSILON &&
        //     Mathf.Abs(a.m13 - b.m13) < EPSILON &&
        //     Mathf.Abs(a.m20 - b.m20) < EPSILON &&
        //     Mathf.Abs(a.m21 - b.m21) < EPSILON &&
        //     Mathf.Abs(a.m22 - b.m22) < EPSILON &&
        //     Mathf.Abs(a.m23 - b.m23) < EPSILON &&
        //     Mathf.Abs(a.m30 - b.m30) < EPSILON &&
        //     Mathf.Abs(a.m31 - b.m31) < EPSILON &&
        //     Mathf.Abs(a.m32 - b.m32) < EPSILON &&
        //     Mathf.Abs(a.m33 - b.m33) < EPSILON;

        // public int GetHashCode(Matrix4x4 matrix)
        // {
        //     unchecked
        //     {
        //         var hash = 17;
        //         hash = (hash * 23) + (int)(matrix.m03 / EPSILON);
        //         hash = (hash * 23) + (int)(matrix.m13 / EPSILON);
        //         hash = (hash * 23) + (int)(matrix.m23 / EPSILON);
        //         return hash;
        //     }
        // }
    }

    [Serializable]
    public class MeshInstanceData : IEquatable<MeshInstanceData>
    {
        // PerInstance data
        public Transform Transform;
        public Matrix4x4 LocalToRootMatrix;

        // Shared data
        public MeshRenderer Renderer;
        public Mesh SharedMesh;

        public bool ReceiveShadows;
        public ShadowCastingMode ShadowCastingMode;

        public Material[] SharedMaterials;

        // RenderParams are not Serializable, so that is why we save collected raw data and transition to RenderParams at runtime
        public GPUInstancedRenderer ToGPUInstancedRenderer() =>
            new (SharedMesh, SharedMaterials.Select(mat => new RenderParams(mat)
            {
                receiveShadows = ReceiveShadows,
                shadowCastingMode = ShadowCastingMode,
                // ?? worldBounds = new Bounds(center: Vector3.zero, size: Vector3.one * 999999f), ?? what value ??
            }).ToArray());

        // Equals when MeshFilter and MeshRenderer settings are same, but Transform could be different
        public bool Equals(MeshInstanceData other) =>
            other != null &&
            Equals(SharedMesh, other.SharedMesh) && // Mesh
            ReceiveShadows == other.ReceiveShadows && ShadowCastingMode == other.ShadowCastingMode && // Shadows
            SharedMaterials != null && other.SharedMaterials != null && SharedMaterials.SequenceEqual(other.SharedMaterials); // Materials

        public override bool Equals(object obj) =>
            obj is MeshInstanceData other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                var hash = 17;
                hash = (hash * 23) + (SharedMesh != null ? SharedMesh.GetHashCode() : 0);
                hash = (hash * 23) + ReceiveShadows.GetHashCode();
                hash = (hash * 23) + ShadowCastingMode.GetHashCode();

                if (SharedMaterials == null) return hash;

                foreach (var material in SharedMaterials)
                    hash = (hash * 23) + (material != null ? material.GetHashCode() : 0);

                return hash;
            }
        }


    }

    [Serializable]
    public class GPUInstancedLOD
    {
        public float ScreenRelativeTransitionHeight;
        public MeshInstanceData[] Meshes;
    }

    [Serializable]
    public class LODGroupData
    {
        public LODGroup LODGroup;
        public Transform Transform;

        public float ObjectSize;
        public Bounds LODBounds;

        [Space]
        public GPUInstancedLOD[] LODs;

        public void UpdateGroupBounds()
        {
            var isInitialized = false;

            foreach (GPUInstancedLOD mid in LODs)
            foreach (MeshInstanceData data in mid.Meshes)
            {
                if (!isInitialized)
                {
                    LODBounds = data.SharedMesh.bounds;
                    isInitialized = true;
                }
                else LODBounds.Encapsulate(data.SharedMesh.bounds);
            }
        }
    }
}
