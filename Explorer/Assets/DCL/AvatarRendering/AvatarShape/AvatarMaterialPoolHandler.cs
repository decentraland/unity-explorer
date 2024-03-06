using System.Collections;
using System.Collections.Generic;
using DCL.AvatarRendering.AvatarShape.Rendering.TextureArray;
using DCL.Optimization.Pools;
using NUnit.Framework.Constraints;
using UnityEngine;
using Utility;

namespace DCL.AvatarRendering.AvatarShape
{

    public struct PoolMaterialSetup
    {
        public IExtendedObjectPool<Material> Pool;
        public TextureArrayContainer TextureArrayContainer;
    }
    
    public class AvatarMaterialPoolHandler
    {
        private readonly Dictionary<int, PoolMaterialSetup> materialDictionary;

        public AvatarMaterialPoolHandler(List<Material> materials, int defaultMaterialCapacity, Dictionary<string, Texture> defaultTextures)
        {
            materialDictionary = new Dictionary<int, PoolMaterialSetup>();
            List<int> resolutionToCreate = new List<int>()
            {
                256, 512
            };
            
            foreach (var material in materials)
            {
                foreach (int resolution in resolutionToCreate)
                {
                    Material? activatedMaterial = ActivateMaterial(material);
                    var textureArrayContainer = TextureArrayContainerFactory.Create(activatedMaterial.shader, resolution, defaultTextures);
                    TextureArrayContainerFactory.ARRAY_TYPES_COUNT = Mathf.Max(TextureArrayContainerFactory.ARRAY_TYPES_COUNT, textureArrayContainer.count);
                    
                    //Create the pool
                    IExtendedObjectPool<Material> pool = new ExtendedObjectPool<Material>(
                        () =>
                        {
                            var mat = new Material(activatedMaterial);
                            return mat;
                        },
                        actionOnRelease: mat =>
                        {
                            // reset material so it does not contain any old properties
                            mat.CopyPropertiesFromMaterial(activatedMaterial);
                        },
                        actionOnDestroy: UnityObjectUtils.SafeDestroy,
                        defaultCapacity: defaultMaterialCapacity);

                    //Prewarm the pool
                    for (var i = 0; i < defaultMaterialCapacity; i++)
                    {
                        Material prewarmedMaterial = pool.Get();
                        pool.Release(prewarmedMaterial);
                    }

                    PoolMaterialSetup materialSetup = new PoolMaterialSetup()
                    {
                        Pool = pool, TextureArrayContainer = textureArrayContainer
                    };

                    int materialID = activatedMaterial.shader.name switch
                    {
                        TextureArrayConstants.TOON_SHADER => TextureArrayConstants.SHADERID_DCL_TOON,
                        TextureArrayConstants.FACIAL_SHADER => TextureArrayConstants.SHADERID_DCL_FACIAL_FEATURES,
                        _ => 0
                    };

                    materialDictionary.Add(materialID * resolution, materialSetup);
                }
            }
        }

        public Dictionary<int, PoolMaterialSetup>.ValueCollection GetAllMaterialsPools()
        {
            return materialDictionary.Values;
        }

        public PoolMaterialSetup GetMaterialPool(int shaderName)
        {
            return materialDictionary[shaderName];
        }

        private Material ActivateMaterial(Material material)
        {
            var activatedMaterial = new Material(material);
            switch(material.shader.name)
            {
                case TextureArrayConstants.TOON_SHADER:
                    activatedMaterial.EnableKeyword("_DCL_TEXTURE_ARRAYS");
                    activatedMaterial.EnableKeyword("_DCL_COMPUTE_SKINNING");
                    return activatedMaterial;
                case TextureArrayConstants.FACIAL_SHADER:
                    activatedMaterial.EnableKeyword("_DCL_TEXTURE_ARRAYS");
                    activatedMaterial.EnableKeyword("_DCL_COMPUTE_SKINNING");
                    return activatedMaterial;
                default:
                    return material;
            }
        }

        public void Release(Material usedMaterial, int materialIndexInPool)
        {
            materialDictionary[materialIndexInPool].Pool.Release(usedMaterial);
        }
    }
}