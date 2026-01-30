using ECS.Unity.GLTFContainer.Asset.Components;
using ECS.Unity.PrimitiveRenderer.Components;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;
using UnityEngine.Rendering;
using Utility;
using Utility.Primitives;

namespace ECS.Unity.SceneBoundsChecker
{
    public static class ConfigureSceneMaterial
    {
        internal static readonly int PLANE_CLIPPING_ID = Shader.PropertyToID("_PlaneClipping");
        internal static readonly int VERTICAL_CLIPPING_ID = Shader.PropertyToID("_VerticalClipping");
        internal static readonly int CULL = Shader.PropertyToID("_Cull");

        private static Vector4 verticalClipping = new (-0.01f, 0.0f, 0.0f, 0.0f); // -0.01f for x to avoid z-fighting

        /// <summary>
        ///     When enabled, forces backface culling on all scene materials.
        ///     Set this from FeaturesRegistry.IsEnabled(FeatureId.FORCE_BACKFACE_CULLING).
        /// </summary>
        public static bool forceBackfaceCullingEnabled { get; set; }

        /// <summary>
        ///     We can use this shared instance as this API is single-threaded
        /// </summary>
        private static readonly List<Material> TEMP_MATERIALS = new (3);

        /// <summary>
        ///     Enables Scene Bounds Checking
        /// </summary>
        public static void EnableSceneBoundsAndForceCulling(in GltfContainerAsset asset, in ParcelMathHelper.SceneCircumscribedPlanes sceneCircumscribedPlanes, float sceneHeight)
        {
            var vector = new Vector4(sceneCircumscribedPlanes.MinX, sceneCircumscribedPlanes.MaxX, sceneCircumscribedPlanes.MinZ, sceneCircumscribedPlanes.MaxZ);
            verticalClipping.y = sceneHeight;

            for (var i = 0; i < asset.Renderers.Count; i++)
            {
                Renderer renderer = asset.Renderers[i];
                renderer.SafeGetMaterials(TEMP_MATERIALS);

                foreach (var material in TEMP_MATERIALS)
                    SetMaterialProperties(material, vector, verticalClipping);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void EnableSceneBoundsAndForceCulling(Material material, in ParcelMathHelper.SceneCircumscribedPlanes sceneCircumscribedPlanes, float sceneHeight)
        {
            var vector = new Vector4(sceneCircumscribedPlanes.MinX, sceneCircumscribedPlanes.MaxX, sceneCircumscribedPlanes.MinZ, sceneCircumscribedPlanes.MaxZ);
            verticalClipping.y = sceneHeight;

            SetMaterialProperties(material, vector, verticalClipping);
        }

        private static void SetMaterialProperties(Material material, Vector4 vector, Vector4 verticalClipping)
        {
            material.SetVector(PLANE_CLIPPING_ID, vector);
            material.SetVector(VERTICAL_CLIPPING_ID, verticalClipping);
            if (forceBackfaceCullingEnabled && material.HasProperty(CULL))
                material.SetInt(CULL, (int)CullMode.Back);
        }

        internal static void SetDefaultMaterial(this ref PrimitiveMeshRendererComponent primitiveMeshRendererComponent, in ParcelMathHelper.SceneCircumscribedPlanes sceneCircumscribedPlanes, float sceneHeight)
        {
            Material dm = DefaultMaterial.Get();
            EnableSceneBoundsAndForceCulling(dm, sceneCircumscribedPlanes, sceneHeight);

            primitiveMeshRendererComponent.MeshRenderer.sharedMaterial = dm;
            primitiveMeshRendererComponent.DefaultMaterialIsUsed = true;
        }
    }
}
