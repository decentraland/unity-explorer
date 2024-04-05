using System.Collections.Generic;
using CommunicationData.URLHelpers;
using DCL.AvatarRendering.AvatarShape.ComputeShader;
using DCL.AvatarRendering.AvatarShape.Rendering.TextureArray;
using DCL.Diagnostics;
using DCL.Optimization.Pools;
using ECS.SceneLifeCycle.SceneDefinition;
using SceneRunner.Scene;
using System.Linq;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;

namespace DCL.LOD
{
    public static class LODUtils
    {
        public static readonly URLDomain LOD_WEB_URL = URLDomain.FromString("https://ab-cdn-decentraland-org-contentbucket-4e8caab.s3.amazonaws.com/LOD/");

        public static readonly URLSubdirectory LOD_EMBEDDED_SUBDIRECTORIES = URLSubdirectory.FromString("lods");

        public static readonly SceneAssetBundleManifest[] LOD_MANIFESTS =
            Enumerable.Range(0, 4).Select(level => new SceneAssetBundleManifest(URLDomain.FromString($"{LOD_WEB_URL}{level}/"))).ToArray();

        private static readonly ListObjectPool<TextureArraySlot?> TEXTURE_ARRAY_SLOTS = new (listInstanceDefaultCapacity: 10, defaultCapacity: 20);
        public static string LOD_SHADER = "DCL/Scene_TexArray";

        public static TextureArraySlot?[] ApplyTextureArrayToLOD(SceneDefinitionComponent sceneDefinitionComponent, GameObject instantiatedLOD, TextureArrayContainer lodTextureArrayContainer)
        {
            var newSlots = TEXTURE_ARRAY_SLOTS.Get();
            using (PoolExtensions.Scope<List<Renderer>> pooledList = instantiatedLOD.GetComponentsInChildrenIntoPooledList<Renderer>(true))
            {
                for (int i = 0; i < pooledList.Value.Count; i++)
                {
                    for (int j = 0; j < pooledList.Value[i].materials.Length; j++)
                    {
                        if (pooledList.Value[i].materials[j].mainTexture != null)
                        {
                            if (pooledList.Value[i].materials[j].mainTexture.width != pooledList.Value[i].materials[j].mainTexture.height)
                            {
                                ReportHub.LogWarning(ReportCategory.LOD, $"Trying to apply a non square resolution in {sceneDefinitionComponent.Definition.id} {sceneDefinitionComponent.Definition.metadata.scene.DecodedBase}");
                                continue;
                            }

                            if (pooledList.Value[i].materials[j].shader.name != LOD_SHADER)
                            {
                                ReportHub.LogWarning(ReportCategory.LOD, $"One material does not have the correct shader in {sceneDefinitionComponent.Definition.id} {sceneDefinitionComponent.Definition.metadata.scene.DecodedBase}. " +
                                                                         $"It has {pooledList.Value[i].materials[j].shader} while it should be {LOD_SHADER}. Please check the AB Converter");
                                continue;
                            }

                            newSlots.AddRange(lodTextureArrayContainer.SetTexturesFromOriginalMaterial(pooledList.Value[i].materials[j], pooledList.Value[i].materials[j]));
                            pooledList.Value[i].materials[j].mainTexture = null;
                        }
                    }
                }
            }

            return newSlots.ToArray();
        }

        private static void ApplyTransparency(Material duplicatedMaterial, bool setDefaultTransparency)
        {
            duplicatedMaterial.EnableKeyword("_ALPHAPREMULTIPLY_ON");
            duplicatedMaterial.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");

            duplicatedMaterial.SetFloat("_Surface",  1);
            duplicatedMaterial.SetFloat("_BlendMode", 0);
            duplicatedMaterial.SetFloat("_AlphaCutoffEnable", 0);
            duplicatedMaterial.SetFloat("_SrcBlend", 1f);
            duplicatedMaterial.SetFloat("_DstBlend", 10f);
            duplicatedMaterial.SetFloat("_AlphaSrcBlend", 1f);
            duplicatedMaterial.SetFloat("_AlphaDstBlend", 10f);
            duplicatedMaterial.SetFloat("_ZTestDepthEqualForOpaque", 4f);
            duplicatedMaterial.renderQueue = (int)RenderQueue.Transparent;

            duplicatedMaterial.color = new Color(duplicatedMaterial.color.r, duplicatedMaterial.color.g, duplicatedMaterial.color.b, setDefaultTransparency ? 0.8f : duplicatedMaterial.color.a);
        }
    }
}
