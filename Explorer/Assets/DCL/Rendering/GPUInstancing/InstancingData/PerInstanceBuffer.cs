using System;
using System.Runtime.InteropServices;
using UnityEngine;

namespace DCL.Rendering.GPUInstancing.InstancingData
{
    [Serializable, StructLayout(LayoutKind.Sequential)]
    public struct PerInstanceBuffer : IEquatable<PerInstanceBuffer>
    {
        public Matrix4x4 instMatrix;
        public Vector4 instColourTint;
        public Vector2 tiling;
        public Vector2 offset;

        public PerInstanceBuffer(Matrix4x4 instMatrix, Vector2 tiling, Vector2 offset)
        {
            this.instMatrix = instMatrix;
            this.tiling = tiling;
            this.offset = offset;

            this.instColourTint = Vector4.one; // white colour
        }

        public bool Equals(PerInstanceBuffer other) =>
            tiling.Equals(other.tiling) && offset.Equals(other.offset) && instMatrix.Equals(other.instMatrix) && instColourTint.Equals(other.instColourTint);

        public override bool Equals(object obj) =>
            obj is PerInstanceBuffer other && Equals(other);

        public override int GetHashCode() =>
            HashCode.Combine(instMatrix, instColourTint, tiling, offset);
    }
}
