using Cysharp.Threading.Tasks;
using DCL.AssetsProvision;
using DCL.AvatarRendering.AvatarShape.Rendering.TextureArray;
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using UnityEngine.AddressableAssets;

#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.

namespace DCL.PluginSystem.Global
{
    public class DefaultTexturesContainer : DCLWorldContainer<DefaultTexturesContainer.Settings>
    {
        public TextureArrayContainerFactory TextureArrayContainerFactory { get; private set; }

        [Serializable]
        public class Settings : IDCLPluginSettings
        {
            [field: SerializeField]
            public AssetReferenceTexture DefaultMain256 { get; private set; }

            [field: SerializeField]
            public AssetReferenceTexture DefaultMain512 { get; private set; }

            [field: SerializeField]
            public AssetReferenceTexture DefaultNormal256 { get; private set; }

            [field: SerializeField]
            public AssetReferenceTexture DefaultNormal512 { get; private set; }

            [field: SerializeField]
            public AssetReferenceTexture DefaultEmmisive256 { get; private set; }

            [field: SerializeField]
            public AssetReferenceTexture DefaultEmmisive512 { get; private set; }

            [field: SerializeField]
            public AssetReferenceTexture DefaultMask256 { get; private set; }

            [field: SerializeField]
            public AssetReferenceTexture DefaultMask512 { get; private set; }
        }

        public static async UniTask<(DefaultTexturesContainer?, bool)> CreateAsync(
            IPluginSettingsContainer settingsContainer,
            IAssetsProvisioner assetsProvisioner,
            CancellationToken ct)
        {
            var container = new DefaultTexturesContainer();
            return await container.InitializeContainerAsync<DefaultTexturesContainer, Settings>(settingsContainer, ct, async texturesContainer =>
            {
                var defaultTextures = new Dictionary<TextureArrayKey, Texture>(10);

                var settings = texturesContainer.settings;

                var mainTex256 = (await assetsProvisioner.ProvideMainAssetAsync(settings.DefaultMain256, ct: ct)).Value;
                var mainTex512 = (await assetsProvisioner.ProvideMainAssetAsync(settings.DefaultMain512, ct: ct)).Value;

                defaultTextures.Add(new TextureArrayKey(TextureArrayConstants.MAINTEX_ARR_TEX_SHADER, 256), mainTex256);
                defaultTextures.Add(new TextureArrayKey(TextureArrayConstants.MAINTEX_ARR_TEX_SHADER, 512) , mainTex512);
                defaultTextures.Add(new TextureArrayKey(TextureArrayConstants.NORMAL_MAP_TEX_ARR, 256), (await assetsProvisioner.ProvideMainAssetAsync(settings.DefaultNormal256, ct: ct)).Value);
                defaultTextures.Add(new TextureArrayKey(TextureArrayConstants.NORMAL_MAP_TEX_ARR, 512), (await assetsProvisioner.ProvideMainAssetAsync(settings.DefaultNormal512, ct: ct)).Value);
                defaultTextures.Add(new TextureArrayKey(TextureArrayConstants.EMISSIVE_MAP_TEX_ARR, 256), (await assetsProvisioner.ProvideMainAssetAsync(settings.DefaultEmmisive256, ct: ct)).Value);
                defaultTextures.Add(new TextureArrayKey(TextureArrayConstants.EMISSIVE_MAP_TEX_ARR, 512), (await assetsProvisioner.ProvideMainAssetAsync(settings.DefaultEmmisive512, ct: ct)).Value);
                defaultTextures.Add(new TextureArrayKey(TextureArrayConstants.MASK_ARR_TEX_SHADER_ID, 256), (await assetsProvisioner.ProvideMainAssetAsync(settings.DefaultMask256, ct: ct)).Value);
                defaultTextures.Add(new TextureArrayKey(TextureArrayConstants.MASK_ARR_TEX_SHADER_ID, 512), (await assetsProvisioner.ProvideMainAssetAsync(settings.DefaultMask512, ct: ct)).Value);

                // Compatibility for PBR shader
                defaultTextures.Add(new TextureArrayKey(TextureArrayConstants.BASE_MAP_TEX_ARR, 256), mainTex256);
                defaultTextures.Add(new TextureArrayKey(TextureArrayConstants.BASE_MAP_TEX_ARR, 512) , mainTex512);

                texturesContainer.TextureArrayContainerFactory = new TextureArrayContainerFactory(defaultTextures);
            });
        }
    }
}
