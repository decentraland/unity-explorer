using System;
using GLTFast;
using UnityEngine;

namespace Loading
{
    /// <summary>
    /// Geometry and material counts of one loaded entity, measured as the glTF describes it rather than
    /// as the scene ends up, so the renderer's own material and texture handling never changes the
    /// numbers an app validates against.
    /// </summary>
    public readonly struct ModelMetrics
    {
        private const string COLLIDER_NAME_PART = "collider";

        public readonly int Triangles;
        public readonly int Materials;
        public readonly int Textures;
        public readonly int Meshes;

        public ModelMetrics(int triangles, int materials, int textures, int meshes)
        {
            Triangles = triangles;
            Materials = materials;
            Textures = textures;
            Meshes = meshes;
        }

        /// <summary>
        /// Counts the renderers instantiated under <paramref name="root"/>, one mesh per sub-mesh as
        /// glTF counts primitives, skipping nodes named as colliders. Materials and textures are read
        /// from the glTF document, so a material the importer could not build still counts.
        /// </summary>
        public static ModelMetrics Measure(GameObject root, GltfImport importer)
        {
            var triangles = 0;
            var meshes = 0;

            foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                if (renderer.name.Contains(COLLIDER_NAME_PART, StringComparison.OrdinalIgnoreCase)) continue;

                Mesh mesh;

                if (renderer is SkinnedMeshRenderer skinned)
                    mesh = skinned.sharedMesh;
                else if (renderer.TryGetComponent<MeshFilter>(out var filter))
                    mesh = filter.sharedMesh;
                else
                    continue;

                if (mesh == null) continue;

                for (var i = 0; i < mesh.subMeshCount; i++)
                {
                    triangles += (int)(mesh.GetIndexCount(i) / 3);
                    meshes++;
                }
            }

            var source = importer.GetSourceRoot();

            return new ModelMetrics(triangles, source?.materials?.Length ?? 0, source?.textures?.Length ?? 0,
                meshes);
        }

        public static ModelMetrics operator +(ModelMetrics a, ModelMetrics b) =>
            new(a.Triangles + b.Triangles, a.Materials + b.Materials, a.Textures + b.Textures,
                a.Meshes + b.Meshes);
    }
}
