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

        public PerInstanceBuffer(Matrix4x4 instMatrix)
        {
            this.instMatrix = instMatrix;
            this.instColourTint = Vector4.one; // white colour
        }

        public bool Equals(PerInstanceBuffer other) =>
            instMatrix.Equals(other.instMatrix) && instColourTint.Equals(other.instColourTint);

        public override bool Equals(object obj) =>
            obj is PerInstanceBuffer other && Equals(other);

        public override int GetHashCode() =>
            HashCode.Combine(instMatrix, instColourTint);
    }
}
