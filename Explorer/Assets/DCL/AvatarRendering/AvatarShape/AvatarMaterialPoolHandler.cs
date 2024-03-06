using System.Collections;
using System.Collections.Generic;
using DCL.AvatarRendering.AvatarShape.Rendering.TextureArray;
using DCL.Optimization.Pools;
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
        private Dictionary<string, PoolMaterialSetup> materialDictionary;

        public AvatarMaterialPoolHandler(List<Material> materials, int defaultMaterialCapacity)
        {
            materialDictionary = new Dictionary<string, PoolMaterialSetup>();
            List<int> resolutionToCreate = new List<int>()
            {
                256, 512
            };
            foreach (var material in materials)
            {
                foreach (int resolution in resolutionToCreate)
                {
                    Material? activatedMaterial = ActivateMaterial(material);
                    TextureArrayContainer textureArrayContainer = TextureArrayContainerFactory.Create(activatedMaterial.shader, resolution);

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

                    materialDictionary.Add($"{activatedMaterial.shader.name}_{resolution}", materialSetup);
                }
            }
        }

        public Dictionary<string, PoolMaterialSetup>.ValueCollection GetAllMaterialsPools() =>
            materialDictionary.Values;
        
        public PoolMaterialSetup GetMaterialPool(string shaderName) =>
            materialDictionary[shaderName];
        
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

        public void Release(Material usedMaterial)
        {
            var tex = usedMaterial.GetTexture(TextureArrayConstants.MAINTEX_ORIGINAL_TEXTURE_ID) as Texture2D;
            int resolution = tex != null ? tex.width : TextureArrayConstants.MAIN_TEXTURE_RESOLUTION;
            
            materialDictionary[$"{usedMaterial.shader.name}_{resolution}"].Pool.Release(usedMaterial);

        }
    }
}