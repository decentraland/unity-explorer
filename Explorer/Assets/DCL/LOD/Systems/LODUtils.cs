using System.Collections.Generic;
using CommunicationData.URLHelpers;
using DCL.AvatarRendering.AvatarShape.ComputeShader;
using DCL.AvatarRendering.AvatarShape.Rendering.Avatar;
using DCL.Diagnostics;
using DCL.Optimization.Pools;
using ECS.SceneLifeCycle.SceneDefinition;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;

namespace DCL.LOD
{
    public static class LODUtils
    {
        public static readonly URLDomain LOD_WEB_URL = URLDomain.FromString("https://ab-cdn-decentraland-org-contentbucket-4e8caab.s3.amazonaws.com/LOD/");

        public static readonly URLSubdirectory[] LOD_EMBEDDED_SUBDIRECTORIES =
        {
            URLSubdirectory.FromString("lods/0"), URLSubdirectory.FromString("lods/1"), URLSubdirectory.FromString("lods/2"), URLSubdirectory.FromString("lods/3")
        };
        
        
        private static readonly ListObjectPool<Material> MATERIALS_LIST_POOL = new (listInstanceDefaultCapacity: 10, defaultCapacity: 20);
        private static readonly ListObjectPool<TextureArraySlot_ToDelete> TEXTURE_ARRAY_SLOTS = new (listInstanceDefaultCapacity: 10, defaultCapacity: 20);

        public static TextureArraySlot_ToDelete[] ApplyTextureArrayToLOD(SceneDefinitionComponent sceneDefinitionComponent, GameObject instantiatedLOD,
            IExtendedObjectPool<Material> materialPool, Dictionary<TextureFormat, TextureArrayContainer_ToDelete> textureArrayContainerDictionary, int lodValue)
        {
            var newSlots = TEXTURE_ARRAY_SLOTS.Get();
            using (PoolExtensions.Scope<List<Renderer>> pooledList = instantiatedLOD.GetComponentsInChildrenIntoPooledList<Renderer>(true))
            {
                for (int i = 0; i < pooledList.Value.Count; i++)
                {
                    var newMaterials =  MATERIALS_LIST_POOL.Get();
                    for (int j = 0; j < pooledList.Value[i].materials.Length; j++)
                    {
                        var newMaterial = materialPool.Get();
                        if (pooledList.Value[i].materials[j].mainTexture != null)
                        {
                            if (pooledList.Value[i].materials[j].mainTexture.width != pooledList.Value[i].materials[j].mainTexture.height)
                            {
                                ReportHub.LogWarning(ReportCategory.LOD, $"Trying to apply a non square resolution in {sceneDefinitionComponent.Definition.id}");
                                continue;
                            }

                            switch (pooledList.Value[i].materials[j].mainTexture.graphicsFormat)
                            {
                                case GraphicsFormat.RGBA_BC7_UNorm:
                                case GraphicsFormat.RGBA_BC7_SRGB:
                                    newSlots.Add(textureArrayContainerDictionary[TextureFormat.BC7].SetTexture(newMaterial, (Texture2D)pooledList.Value[i].materials[j].mainTexture, ComputeShaderConstants.TextureArrayType.ALBEDO));
                                    break;
                                case GraphicsFormat.RGBA_DXT1_UNorm:
                                case GraphicsFormat.RGBA_DXT1_SRGB:
                                    newSlots.Add(textureArrayContainerDictionary[TextureFormat.DXT1].SetTexture(newMaterial, (Texture2D)pooledList.Value[i].materials[j].mainTexture, ComputeShaderConstants.TextureArrayType.ALBEDO));
                                    break;
                                case GraphicsFormat.RGBA_DXT5_SRGB:
                                case GraphicsFormat.RGBA_DXT5_UNorm:
                                    newSlots.Add(textureArrayContainerDictionary[TextureFormat.DXT5].SetTexture(newMaterial, (Texture2D)pooledList.Value[i].materials[j].mainTexture, ComputeShaderConstants.TextureArrayType.ALBEDO));
                                    break;
                            }
                        }

                        newMaterial.DisableKeyword("_NORMALMAP");
                        newMaterial.SetColor(ComputeShaderConstants._BaseColour_ShaderID, pooledList.Value[i].materials[j].color);
                        if (pooledList.Value[i].materials[j].name.Contains("FORCED_TRANSPARENT"))
                        {
                            ApplyTransparency(newMaterial, true);
                        }

                        newMaterials.Add(newMaterial);
                    }

                    pooledList.Value[i].materials = newMaterials.ToArray();
                    MATERIALS_LIST_POOL.Release(newMaterials);
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