using Cysharp.Threading.Tasks;
using DCL.AssetsProvision;
using DCL.AvatarRendering.AvatarShape.ComputeShader;
using DCL.AvatarRendering.AvatarShape.Rendering.TextureArray;
using DCL.Rendering.DCL_Toon;
using Global.AppArgs;
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

            // Default metallic-gloss mask. Should be WHITE (metallic in .b = 1): a wearable that is
            // metallic by factor (no map) has no mask packed, so it falls back to this default and
            // reads as fully metallic — the _IsStylizedMetallic gate keeps non-metal wearables off.
            [field: SerializeField]
            public AssetReferenceTexture DefaultMetallic256 { get; private set; }

            [field: SerializeField]
            public AssetReferenceTexture DefaultMetallic512 { get; private set; }

            [field: SerializeField]
            public AssetReferenceTexture DefaultMouthBrowMask256 { get; private set; }

            [field: SerializeField]
            public AssetReferenceTexture DefaultMouthBrowMask512 { get; private set; }

            [field: SerializeField]
            public AssetReferenceTexture DefaultEyesMask256 { get; private set; }

            [field: SerializeField]
            public AssetReferenceTexture DefaultEyesMask512 { get; private set; }

            // Shared stylized-metallic matcap library, referenced directly by GUID to the package
            // MatcapPresets.asset (precedent: Avatar_Toon.mat -> package DCL_Toon.shader). Small fixed
            // set, so a direct reference is simpler than Addressables.
            [field: SerializeField]
            public MatcapPresets MatcapPresets { get; private set; }

            // Preset name applied to metallic materials whose wearable JSON has no (or an unknown) matcap.
            [field: SerializeField]
            public string DefaultMatcapName { get; private set; }
        }

        public static async UniTask<(DefaultTexturesContainer?, bool)> CreateAsync(
            IPluginSettingsContainer settingsContainer,
            IAssetsProvisioner assetsProvisioner,
            IAppArgs appArgs,
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
                defaultTextures.Add(new TextureArrayKey(TextureArrayConstants.METALLIC_GLOSS_MAP_ARR_TEX_SHADER_ID, 256), (await assetsProvisioner.ProvideMainAssetAsync(settings.DefaultMetallic256, ct: ct)).Value);
                defaultTextures.Add(new TextureArrayKey(TextureArrayConstants.METALLIC_GLOSS_MAP_ARR_TEX_SHADER_ID, 512), (await assetsProvisioner.ProvideMainAssetAsync(settings.DefaultMetallic512, ct: ct)).Value);
                defaultTextures.Add(new TextureArrayKey(TextureArrayConstants.MASK_ARR_TEX_SHADER_ID, 256, 0), (await assetsProvisioner.ProvideMainAssetAsync(settings.DefaultMouthBrowMask256, ct: ct)).Value);
                defaultTextures.Add(new TextureArrayKey(TextureArrayConstants.MASK_ARR_TEX_SHADER_ID, 512, 0), (await assetsProvisioner.ProvideMainAssetAsync(settings.DefaultMouthBrowMask512, ct: ct)).Value);
                defaultTextures.Add(new TextureArrayKey(TextureArrayConstants.MASK_ARR_TEX_SHADER_ID, 256, 1), (await assetsProvisioner.ProvideMainAssetAsync(settings.DefaultEyesMask256, ct: ct)).Value);
                defaultTextures.Add(new TextureArrayKey(TextureArrayConstants.MASK_ARR_TEX_SHADER_ID, 512, 1), (await assetsProvisioner.ProvideMainAssetAsync(settings.DefaultEyesMask512, ct: ct)).Value);

                // Compatibility for PBR shader
                defaultTextures.Add(new TextureArrayKey(TextureArrayConstants.BASE_MAP_TEX_ARR, 256), mainTex256);
                defaultTextures.Add(new TextureArrayKey(TextureArrayConstants.BASE_MAP_TEX_ARR, 512) , mainTex512);

                texturesContainer.TextureArrayContainerFactory = new TextureArrayContainerFactory(defaultTextures, enableRawGltfWearables: appArgs.HasFlag(AppArgsFlags.SELF_PREVIEW_BUILDER_COLLECTIONS));

                BuildMatcapLibrary(settings);
            });
        }

        // Builds one shared matcap Texture2DArray from the MatcapPresets SO and installs it (plus the
        // name->slice map and per-slice tint/blur) on AvatarMaterialConfiguration. All presets are
        // authored uniformly (256², same format + mips) so a straight Graphics.CopyTexture per slice
        // works. No presets => leaves Matcap null and metallic materials fall back to unlit (safe).
        private static void BuildMatcapLibrary(Settings settings)
        {
            MatcapPresets presets = settings.MatcapPresets;
            if (presets == null || presets.Count == 0) return;

            Texture2D first = presets[0].texture;
            if (first == null) return;

            var array = new Texture2DArray(first.width, first.height, presets.Count, first.format, first.mipmapCount > 1, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                anisoLevel = 1,
            };

            var nameToSlice = new Dictionary<string, int>(presets.Count);
            var tints = new Color[presets.Count];
            var blurs = new float[presets.Count];

            for (var i = 0; i < presets.Count; i++)
            {
                MatcapPresets.Preset p = presets[i];
                if (p.texture != null)
                    Graphics.CopyTexture(p.texture, 0, array, i);
                if (!string.IsNullOrEmpty(p.name))
                    nameToSlice[p.name] = i;
                tints[i] = p.tint;
                blurs[i] = p.blur;
            }

            array.Apply(false, true);

            int defaultSlice = presets.TryGetIndex(settings.DefaultMatcapName, out int di) ? di : 0;
            AvatarMaterialConfiguration.Matcap = new AvatarMaterialConfiguration.MatcapLibrary(array, nameToSlice, tints, blurs, defaultSlice);
        }
    }
}
