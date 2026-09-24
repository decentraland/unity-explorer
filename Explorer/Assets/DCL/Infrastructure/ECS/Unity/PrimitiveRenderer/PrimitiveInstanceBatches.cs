using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace ECS.Unity.PrimitiveRenderer
{
    /// <summary>
    ///     Per-frame aggregation of primitive instances that share a mesh and a material, drawn with
    ///     <see cref="Graphics.RenderMeshInstanced{T}(in RenderParams, Mesh, int, List{T}, int, int)" />.
    ///     Batches outlive the frame so their instance lists are reused; <see cref="Clear" /> empties them before a new collection.
    /// </summary>
    public class PrimitiveInstanceBatches
    {
        /// <summary>
        ///     Upper bound of instances a single instanced draw accepts.
        /// </summary>
        internal const int MAX_INSTANCES_PER_DRAW = 1023;

        // Pooled renderer game objects are created on the default layer and nothing reassigns them
        private const int DEFAULT_LAYER = 0;

        private readonly Dictionary<(Mesh mesh, Material material), Batch> batches = new ();
        private readonly List<Batch> activeBatches = new ();

        public int BatchCount => activeBatches.Count;

        public void Clear()
        {
            for (var i = 0; i < activeBatches.Count; i++)
                activeBatches[i].Matrices.Clear();

            activeBatches.Clear();
        }

        public void Add(Mesh mesh, Material material, ShadowCastingMode shadowCastingMode, in Matrix4x4 localToWorld)
        {
            if (!batches.TryGetValue((mesh, material), out Batch batch))
            {
                batch = new Batch(mesh, material);
                batches.Add((mesh, material), batch);
            }

            Bounds instanceBounds = TransformBounds(batch.LocalBounds, in localToWorld);

            if (batch.Matrices.Count == 0)
            {
                // The shader compiles instanced variants for every material; the flag only opts the material in
                if (!material.enableInstancing)
                    material.enableInstancing = true;

                batch.ShadowCastingMode = shadowCastingMode;
                batch.WorldBounds = instanceBounds;
                activeBatches.Add(batch);
            }
            else
                batch.WorldBounds.Encapsulate(instanceBounds);

            batch.Matrices.Add(localToWorld);
        }

        public void Render()
        {
            for (var i = 0; i < activeBatches.Count; i++)
            {
                Batch batch = activeBatches[i];

                var renderParams = new RenderParams(batch.Material)
                {
                    layer = DEFAULT_LAYER,
                    shadowCastingMode = batch.ShadowCastingMode,
                    receiveShadows = true,
                    renderingLayerMask = RenderingLayerMask.defaultRenderingLayerMask,
                    worldBounds = batch.WorldBounds,
                };

                for (var start = 0; start < batch.Matrices.Count; start += MAX_INSTANCES_PER_DRAW)
                {
                    int count = Mathf.Min(MAX_INSTANCES_PER_DRAW, batch.Matrices.Count - start);
                    Graphics.RenderMeshInstanced(renderParams, batch.Mesh, 0, batch.Matrices, count, start);
                }
            }
        }

        internal int InstanceCount(int batchIndex) =>
            activeBatches[batchIndex].Matrices.Count;

        internal Bounds WorldBounds(int batchIndex) =>
            activeBatches[batchIndex].WorldBounds;

        private static Bounds TransformBounds(in Bounds local, in Matrix4x4 m)
        {
            Vector3 center = m.MultiplyPoint3x4(local.center);
            Vector3 e = local.extents;

            var extents = new Vector3(
                (Mathf.Abs(m.m00) * e.x) + (Mathf.Abs(m.m01) * e.y) + (Mathf.Abs(m.m02) * e.z),
                (Mathf.Abs(m.m10) * e.x) + (Mathf.Abs(m.m11) * e.y) + (Mathf.Abs(m.m12) * e.z),
                (Mathf.Abs(m.m20) * e.x) + (Mathf.Abs(m.m21) * e.y) + (Mathf.Abs(m.m22) * e.z));

            return new Bounds(center, extents * 2f);
        }

        private class Batch
        {
            public readonly Mesh Mesh;
            public readonly Material Material;
            public readonly Bounds LocalBounds;
            public readonly List<Matrix4x4> Matrices = new ();

            public ShadowCastingMode ShadowCastingMode;
            public Bounds WorldBounds;

            public Batch(Mesh mesh, Material material)
            {
                Mesh = mesh;
                Material = material;
                LocalBounds = mesh.bounds;
            }
        }
    }
}
