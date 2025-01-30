using System;
using System.Runtime.InteropServices;
using UnityEngine;

namespace DCL.Roads.GPUInstancing.Playground
{
    [Serializable]
    public class GPUInstancedMesh
    {
        public MeshRenderingData meshRenderingData;
        public PerInstanceBuffer[] PerInstancesData;
    }

    [Serializable, StructLayout(LayoutKind.Sequential)]
    public struct PerInstanceBuffer : IEquatable<PerInstanceBuffer>
    {
        public Matrix4x4 instMatrix;
        public Vector3 instColourTint;

        public PerInstanceBuffer(Matrix4x4 instMatrix)
        {
            this.instMatrix = instMatrix;
            this.instColourTint = Vector3.one;
        }

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
    public class GPUInstancedLOD
    {
        public float ScreenRelativeTransitionHeight;
        public MeshRenderingData[] Meshes;
    }

    [Serializable]
    public class LODGroupData
    {
        public LODGroup LODGroup;
        public Transform Transform;

        [Space]
        public float ObjectSize;
        public Bounds LODBounds;

        [Space]
        public GPUInstancedLOD[] LODs;

        public void UpdateGroupBounds()
        {
            var isInitialized = false;

            foreach (GPUInstancedLOD mid in LODs)
            foreach (MeshRenderingData data in mid.Meshes)
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
