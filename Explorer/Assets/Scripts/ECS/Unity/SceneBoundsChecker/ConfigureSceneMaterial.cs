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

        /// <summary>
        ///     We can use this shared instance as this API is single-threaded
        /// </summary>
        private static readonly List<Material> TEMP_MATERIALS = new (3);
        
        /// <summary>
        ///     Enables Scene Bounds Checking
        /// </summary>
        public static void EnableSceneBounds(in GltfContainerAsset asset, in ParcelMathHelper.SceneCircumscribedPlanes sceneCircumscribedPlanes)
        {
            var vector = new Vector4(sceneCircumscribedPlanes.MinX, sceneCircumscribedPlanes.MaxX, sceneCircumscribedPlanes.MinZ, sceneCircumscribedPlanes.MaxZ);

            for (var i = 0; i < asset.Renderers.Count; i++)
            {
                Renderer renderer = asset.Renderers[i];
                renderer.SafeGetMaterials(TEMP_MATERIALS);

                for (var j = 0; j < TEMP_MATERIALS.Count; j++)
                    TEMP_MATERIALS[j].SetVector(PLANE_CLIPPING_ID, vector);
            }
        }

        /// <summary>
        ///     Enables Scene Bounds Checking for GameObjects
        /// </summary>
        public static void EnableSceneBounds(GameObject asset, in
            ParcelMathHelper.SceneCircumscribedPlanes sceneCircumscribedPlanes)
        {
            var vector = new Vector4(sceneCircumscribedPlanes.MinX, sceneCircumscribedPlanes.MaxX,
                sceneCircumscribedPlanes.MinZ, sceneCircumscribedPlanes.MaxZ);

            var componentsInChildren = asset.GetComponentsInChildren<MeshRenderer>();
            for (var i = 0; i < componentsInChildren.Length; i++)
            {
                componentsInChildren[i].SafeGetMaterials(TEMP_MATERIALS);

                for (var j = 0; j < TEMP_MATERIALS.Count; j++)
                    TEMP_MATERIALS[j].SetVector(PLANE_CLIPPING_ID, vector);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void EnableSceneBounds(Material material, in ParcelMathHelper.SceneCircumscribedPlanes sceneCircumscribedPlanes)
        {
            var vector = new Vector4(sceneCircumscribedPlanes.MinX, sceneCircumscribedPlanes.MaxX, sceneCircumscribedPlanes.MinZ, sceneCircumscribedPlanes.MaxZ);
            material.SetVector(PLANE_CLIPPING_ID, vector);
        }

        internal static void SetDefaultMaterial(this ref PrimitiveMeshRendererComponent primitiveMeshRendererComponent, in ParcelMathHelper.SceneCircumscribedPlanes sceneCircumscribedPlanes)
        {
            Material dm = DefaultMaterial.Get();
            EnableSceneBounds(dm, sceneCircumscribedPlanes);

            primitiveMeshRendererComponent.MeshRenderer.sharedMaterial = dm;
            primitiveMeshRendererComponent.DefaultMaterialIsUsed = true;
        }
    }
}
