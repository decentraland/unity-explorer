using System;
using System.Linq;
using UnityEngine;

namespace DCL.Roads.Playground
{
    /// <summary>
    ///     Pair of mesh and render parameters (material, shadows,..) used in one GPU instancing draw call for several instances of such pair
    /// </summary>
    [Serializable]
    public struct GPUInstancedRenderer : IEquatable<GPUInstancedRenderer>
    {
        public readonly Mesh Mesh;
        public readonly RenderParams[] RenderParamsArray; // array for submeshes

        public GPUInstancedRenderer(Mesh mesh, RenderParams[] renderParamsArray)
        {
            Mesh = mesh;
            RenderParamsArray = renderParamsArray;
        }

        public bool Equals(GPUInstancedRenderer other) =>
            Equals(Mesh, other.Mesh) &&
            RenderParamsArray != null &&
            other.RenderParamsArray != null &&
            RenderParamsArray.SequenceEqual(other.RenderParamsArray);

        public override bool Equals(object obj) =>
            obj is GPUInstancedRenderer other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                var hash = 17;
                hash = (hash * 23) + (Mesh != null ? Mesh.GetHashCode() : 0);

                if (RenderParamsArray == null) return hash;

                foreach (var param in RenderParamsArray)
                    hash = (hash * 23) + param.GetHashCode();

                return hash;
            }
        }
    }
}
