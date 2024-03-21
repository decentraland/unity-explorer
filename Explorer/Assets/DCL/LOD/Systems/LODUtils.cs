using System.Collections.Generic;
using DCL.AvatarRendering.AvatarShape.ComputeShader;
using DCL.AvatarRendering.AvatarShape.Rendering.Avatar;
using DCL.Diagnostics;
using DCL.Optimization.Pools;
using ECS.SceneLifeCycle.SceneDefinition;
using UnityEngine;
using UnityEngine.Experimental.Rendering;

namespace DCL.LOD
{
    public static class LODUtils
    {
        private static readonly ListObjectPool<Material> MATERIALS_LIST_POOL = new (listInstanceDefaultCapacity: 10, defaultCapacity: 20);
        private static readonly ListObjectPool<TextureArraySlot> TEXTURE_ARRAY_SLOTS = new (listInstanceDefaultCapacity: 10, defaultCapacity: 20);


        public static TextureArraySlot[] ApplyTextureArrayToLOD(SceneDefinitionComponent sceneDefinitionComponent, GameObject instantiatedLOD,
            IExtendedObjectPool<Material> materialPool, Dictionary<TextureFormat, TextureArrayContainer> textureArrayContainerDictionary, int lodValue)
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
                            newMaterial.EnableKeyword("_ALPHATEST_ON");
                            newMaterial.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                        }

                        newMaterials.Add(newMaterial);
                    }

                    pooledList.Value[i].materials = newMaterials.ToArray();
                    MATERIALS_LIST_POOL.Release(newMaterials);
                }
            }

            return newSlots.ToArray();
        }
    }
}