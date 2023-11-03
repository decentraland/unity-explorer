using ECS.Unity.GLTFContainer.Asset.Components;
using System.Collections.Generic;
using UnityEngine;
using Utility;

namespace ECS.Unity.GLTFContainer.Systems
{
    internal static class ConfigureGltfMaterials
    {
        internal static readonly int PLANE_CLIPPING_ID = Shader.PropertyToID("_PlaneClipping");

        /// <summary>
        ///     We can use this shared instance as this API is single-threaded
        /// </summary>
        private static readonly List<Material> TEMP_MATERIALS = new (3);

        /// <summary>
        ///     Enables Scene Bounds Checking
        /// </summary>
        internal static void EnableSceneBounds(in GltfContainerAsset asset, in ParcelMathHelper.SceneCircumscribedPlanes sceneCircumscribedPlanes)
        {
            var vector = new Vector4(sceneCircumscribedPlanes.MinX, sceneCircumscribedPlanes.MaxX, sceneCircumscribedPlanes.MinZ, sceneCircumscribedPlanes.MaxZ);

            for (var i = 0; i < asset.Renderers.Count; i++)
            {
                Renderer renderer = asset.Renderers[i];
                renderer.GetMaterials(TEMP_MATERIALS);

                for (var j = 0; j < TEMP_MATERIALS.Count; j++)
                    TEMP_MATERIALS[j].SetVector(PLANE_CLIPPING_ID, vector);
            }
        }
    }
}
