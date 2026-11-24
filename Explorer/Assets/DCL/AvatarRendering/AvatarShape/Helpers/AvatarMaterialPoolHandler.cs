using DCL.AvatarRendering.AvatarShape.Components;
using System.Collections.Generic;
using DCL.AvatarRendering.AvatarShape.Helpers;
using DCL.AvatarRendering.AvatarShape.Rendering.TextureArray;
using DCL.Diagnostics;
using DCL.Optimization.Pools;
using UnityEngine;
using Utility;

namespace DCL.AvatarRendering.AvatarShape
{
    public class AvatarMaterialPoolHandler : IAvatarMaterialPoolHandler
    {
        private static readonly IReadOnlyList<int> DEFAULT_RESOLUTIONS = new List<int>
        {
            256, 512,
        };

        private readonly Dictionary<int, PoolMaterialSetup> materialDictionary;

        public AvatarMaterialPoolHandler(List<Material> materials, int defaultMaterialCapacity, TextureArrayContainerFactory textureArrayContainerFactory)
        {
            materialDictionary = new Dictionary<int, PoolMaterialSetup>();

            foreach (Material material in materials)
            {
                var textureArrayContainer = textureArrayContainerFactory.Create(material.shader, DEFAULT_RESOLUTIONS);

                //Create the pool
                IExtendedObjectPool<Material> pool = new ExtendedObjectPool<Material>(
                    () =>
                    {
                        var mat = new Material(material);
                        return mat;
                    },
                    actionOnRelease: mat =>
                    {
                        if (material != null)
                            // reset material so it does not contain any old properties
                            mat.CopyPropertiesFromMaterial(material);
                    },
                    actionOnDestroy: UnityObjectUtils.SafeDestroy,
                    defaultCapacity: defaultMaterialCapacity);

                //Prewarm the pool
                var prewarmedMaterials = new Material[defaultMaterialCapacity];

                for (var i = 0; i < defaultMaterialCapacity; i++)
                    prewarmedMaterials[i] = pool.Get();

                for (var i = 0; i < defaultMaterialCapacity; i++)
                    pool.Release(prewarmedMaterials[i]);

                var materialSetup = new PoolMaterialSetup(pool, textureArrayContainer);

                int materialID = material.shader.name switch
                                 {
                                     TextureArrayConstants.TOON_SHADER => TextureArrayConstants.SHADERID_DCL_TOON,
                                     TextureArrayConstants.FACIAL_SHADER => TextureArrayConstants.SHADERID_DCL_FACIAL_FEATURES,
                                     _ => TextureArrayConstants.SHADERID_DCL_PBR,
                                 };

                if (!materialDictionary.TryAdd(materialID, materialSetup))
                    ReportHub.LogError(ReportCategory.AVATAR, $"Duplicate avatar material pool for shader id {materialID} ({material.shader.name})");
            }
        }

        public IReadOnlyCollection<PoolMaterialSetup> GetAllMaterialsPools() =>
            materialDictionary.Values;

        public bool TryGetMaterialPool(int shaderName, out PoolMaterialSetup setup) =>
            materialDictionary.TryGetValue(shaderName, out setup);

        public void Release(AvatarCustomSkinningComponent.MaterialSetup materialSetup)
        {
            if (!materialDictionary.TryGetValue(materialSetup.shaderId, out PoolMaterialSetup setup))
            {
                ReportHub.LogError(ReportCategory.AVATAR, $"Cannot release avatar material: no pool is registered for shader id {materialSetup.shaderId}");
                return;
            }

            setup.Pool.Release(materialSetup.usedMaterial);
            setup.TextureArrayContainer.ReleaseSlots(materialSetup.usedTextureArraySlots);
        }
    }
}
