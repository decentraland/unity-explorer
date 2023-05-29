using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using DCL.ECSComponents;
using ECS.Abstract;
using ECS.Unity.Materials.Components;
using ECS.Unity.Materials.Components.Defaults;
using ECS.Unity.Textures.Components;
using ECS.Unity.Textures.Components.Defaults;
using SceneRunner.Scene;

namespace ECS.Unity.Materials.Systems
{
    /// <summary>
    ///     Places a loading intention that can be consumed by other systems in the pipeline.
    ///     Does not provide support for Video Textures
    /// </summary>
    [UpdateInGroup(typeof(MaterialLoadingGroup))]
    public partial class StartMaterialsLoadingSystem : BaseUnityLoopSystem
    {
        private readonly IMaterialsCache materialsCache;
        private readonly ISceneContentProvider sceneContentProvider;

        public StartMaterialsLoadingSystem(World world, IMaterialsCache materialsCache, ISceneContentProvider sceneContentProvider) : base(world)
        {
            this.materialsCache = materialsCache;
            this.sceneContentProvider = sceneContentProvider;
        }

        protected override void Update(float t)
        {
            InvalidateMaterialComponentQuery(World);
            CreateMaterialComponentQuery(World);
        }

        [Query]
        private void InvalidateMaterialComponent(ref PBMaterial material, ref MaterialComponent materialComponent)
        {
            if (!material.IsDirty)
                return;

            material.IsDirty = false;

            MaterialData materialData = CreateMaterialData(ref material);

            if (MaterialDataEqualityComparer.INSTANCE.Equals(materialComponent.Data, materialData))
                return;

            ReleaseMaterial.Execute(World, ref materialComponent, materialsCache);

            materialComponent.Data = materialData;
        }

        [Query]
        [All(typeof(PBMaterial))]
        [None(typeof(MaterialComponent))]
        private void CreateMaterialComponent(in Entity entity, ref PBMaterial material)
        {
            World.Add(entity, new MaterialComponent(CreateMaterialData(ref material)));
        }

        private MaterialData CreateMaterialData(ref PBMaterial material)
        {
            // TODO Video Textures

            TextureComponent? albedoTexture = (material.Pbr?.Texture ?? material.Unlit?.Texture).CreateTextureComponent(sceneContentProvider);

            if (material.Pbr != null)
            {
                TextureComponent? alphaTexture = material.Pbr.AlphaTexture.CreateTextureComponent(sceneContentProvider);
                TextureComponent? emissiveTexture = material.Pbr.EmissiveTexture.CreateTextureComponent(sceneContentProvider);
                TextureComponent? bumpTexture = material.Pbr.BumpTexture.CreateTextureComponent(sceneContentProvider);

                return CreatePBRMaterialData(material, albedoTexture, alphaTexture, emissiveTexture, bumpTexture);
            }

            return CreateBasicMaterialData(material, albedoTexture);
        }

        private static MaterialData CreatePBRMaterialData(
            in PBMaterial pbMaterial,
            in TextureComponent? albedoTexture,
            in TextureComponent? alphaTexture,
            in TextureComponent? emissiveTexture,
            in TextureComponent? bumpTexture)
        {
            var materialData = MaterialData.CreatePBRMaterial(
                albedoTexture,
                alphaTexture,
                emissiveTexture,
                bumpTexture,
                pbMaterial.GetAlphaTest(),
                pbMaterial.GetCastShadows(),
                pbMaterial.GetAlbedoColor(),
                pbMaterial.GetEmissiveColor(),
                pbMaterial.GetReflectiveColor(),
                pbMaterial.GetTransparencyMode(),
                pbMaterial.GetMetallic(),
                pbMaterial.GetRoughness(),
                pbMaterial.GetGlossiness(),
                pbMaterial.GetSpecularIntensity(),
                pbMaterial.GetEmissiveIntensity(),
                pbMaterial.GetDirectIntensity());

            return materialData;
        }

        private static MaterialData CreateBasicMaterialData(in PBMaterial pbMaterial, in TextureComponent? albedoTexture) =>
            MaterialData.CreateBasicMaterial(albedoTexture, pbMaterial.GetAlphaTest(), pbMaterial.GetDiffuseColor(), pbMaterial.GetCastShadows());
    }
}
