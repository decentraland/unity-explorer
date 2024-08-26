using ECS.Unity.GLTFContainer.Asset.Components;
using ECS.Unity.PrimitiveRenderer.Components;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;
using Utility;
using Utility.Primitives;

namespace ECS.Unity.SceneBoundsChecker
{
    public static class ConfigureSceneMaterial
    {
        internal static readonly int PLANE_CLIPPING_ID = Shader.PropertyToID("_PlaneClipping");
        internal static readonly int VERTICAL_CLIPPING_ID = Shader.PropertyToID("_VerticalClipping");

        /// <summary>
        ///     We can use this shared instance as this API is single-threaded
        /// </summary>
        private static readonly List<Material> TEMP_MATERIALS = new (3);

        /// <summary>
        ///     Enables Scene Bounds Checking
        /// </summary>
        public static void EnableSceneBounds(in GltfContainerAsset asset, in ParcelMathHelper.SceneCircumscribedPlanes sceneCircumscribedPlanes, float sceneHeight)
        {
            var vector = new Vector4(sceneCircumscribedPlanes.MinX, sceneCircumscribedPlanes.MaxX, sceneCircumscribedPlanes.MinZ, sceneCircumscribedPlanes.MaxZ);
            Vector4 verticalClipping = new Vector4(0.0f, sceneHeight, 0.0f, 0.0f);

            for (var i = 0; i < asset.Renderers.Count; i++)
            {
                Renderer renderer = asset.Renderers[i];
                renderer.SafeGetMaterials(TEMP_MATERIALS);

                for (var j = 0; j < TEMP_MATERIALS.Count; j++)
                {
                    TEMP_MATERIALS[j].SetVector(PLANE_CLIPPING_ID, vector);
                    TEMP_MATERIALS[j].SetVector(VERTICAL_CLIPPING_ID, verticalClipping);
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void EnableSceneBounds(Material material, in ParcelMathHelper.SceneCircumscribedPlanes sceneCircumscribedPlanes, float sceneHeight)
        {
            var vector = new Vector4(sceneCircumscribedPlanes.MinX, sceneCircumscribedPlanes.MaxX, sceneCircumscribedPlanes.MinZ, sceneCircumscribedPlanes.MaxZ);
            Vector4 verticalClipping = new Vector4(0.0f, sceneHeight, 0.0f, 0.0f);

            material.SetVector(PLANE_CLIPPING_ID, vector);
            material.SetVector(VERTICAL_CLIPPING_ID, verticalClipping);
        }

        internal static void SetDefaultMaterial(this ref PrimitiveMeshRendererComponent primitiveMeshRendererComponent, in ParcelMathHelper.SceneCircumscribedPlanes sceneCircumscribedPlanes, float sceneHeight)
        {
            Material dm = DefaultMaterial.Get();
            EnableSceneBounds(dm, sceneCircumscribedPlanes, sceneHeight);

            primitiveMeshRendererComponent.MeshRenderer.sharedMaterial = dm;
            primitiveMeshRendererComponent.DefaultMaterialIsUsed = true;
        }
    }
}
