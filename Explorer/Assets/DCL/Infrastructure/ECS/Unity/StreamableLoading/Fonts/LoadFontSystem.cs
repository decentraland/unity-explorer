using Arch.Core;
using Arch.SystemGroups;
using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.Ipfs;
using ECS.Prioritization.Components;
using ECS.StreamableLoading.AssetBundles;
using ECS.StreamableLoading.Cache;
using ECS.StreamableLoading.Common;
using ECS.StreamableLoading.Common.Components;
using ECS.StreamableLoading.Common.Systems;
using System;
using System.Threading;
using TMPro;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.TextCore.Text;
using AssetBundlePromise = ECS.StreamableLoading.Common.AssetPromise<ECS.StreamableLoading.AssetBundles.AssetBundleData, ECS.StreamableLoading.AssetBundles.GetAssetBundleIntention>;

namespace ECS.StreamableLoading.Fonts
{
    [UpdateInGroup(typeof(StreamableLoadingGroup))]
    [LogCategory(ReportCategory.SDK_FONTS)]
    public partial class LoadFontSystem : LoadSystemBase<FontData, GetFontIntention>
    {
        // The names the converter gives a font bundle's two font assets (abgen builder::font).
        private const string BUNDLE_TEXT_MESH_PRO_ASSET = "tmp";
        private const string BUNDLE_UI_TOOLKIT_ASSET = "uitk";

        private const int MAX_ATLAS_SIZE = 4096;

        private static readonly ProfilerMarker READ_BUNDLED_FONT_MARKER = new ($"{nameof(LoadFontSystem)}.ReadBundledFont");

        private readonly RuntimeFontAssetFactory fontAssetFactory;
        private readonly bool tryUnlistedBundles;

        /// <param name="tryUnlistedBundles">
        ///     Local scene development with local asset bundles: the scene's manifest files[] are never read there, so
        ///     every scene font file is tried as a bundle, and the fonts the converter skipped keep the built-in font.
        /// </param>
        internal LoadFontSystem(World world, IStreamableCache<FontData, GetFontIntention> cache, RuntimeFontAssetFactory fontAssetFactory,
            bool tryUnlistedBundles) : base(world, cache)
        {
            this.fontAssetFactory = fontAssetFactory;
            this.tryUnlistedBundles = tryUnlistedBundles;
        }

        protected override void DisposeAbandonedResult(FontData asset) =>
            asset.Dispose();

        protected override async UniTask<StreamableLoadingResult<FontData>> FlowInternalAsync(GetFontIntention intention, StreamableLoadingState state, IPartitionComponent partition, CancellationToken ct)
        {
            if (intention.AssetBundleHash == null)
                throw new FontLoadException($"\"{intention.Src}\": the scene has no converted asset bundles to load the font from, the built-in font stays");

            if (!intention.AssetBundleListed && !tryUnlistedBundles)
                throw new FontLoadException($"\"{intention.Src}\": the scene's asset bundle manifest lists no converted font bundle for it, the built-in font stays");

            return new StreamableLoadingResult<FontData>(await LoadConvertedAsync(intention, partition, ct));
        }

        /// <summary>
        ///     Loads the font from the bundle the converter built for it: both font assets already built, their atlases
        ///     pre-filled with the common characters, and the source font inside for the rest. Throws a
        ///     <see cref="FontLoadException" /> when the bundle cannot be used.
        /// </summary>
        private async UniTask<FontData> LoadConvertedAsync(GetFontIntention intention, IPartitionComponent partition, CancellationToken ct)
        {
            await UniTask.SwitchToMainThread(ct);

            AssetBundleManifestVersion manifest = intention.AssetBundleManifest!;
            var promise = AssetBundlePromise.Create(World,
                GetAssetBundleIntention.FromHash(manifest.GetCdnRequestHash(intention.AssetBundleHash!), manifest, parentEntityID: intention.SceneId),
                partition);

            try { promise = await promise.ToUniTaskAsync(World, cancellationToken: ct); }
            catch (OperationCanceledException)
            {
                promise.ForgetLoading(World);
                throw;
            }

            if (!promise.TryGetResult(World, out StreamableLoadingResult<AssetBundleData> result) || result is not { Succeeded: true, Asset: { } bundle })
                throw new FontLoadException($"\"{intention.Src}\": its converted font bundle did not load, the built-in font stays: {result.Exception?.Message}");

            using ProfilerMarker.AutoScope _ = READ_BUNDLED_FONT_MARKER.Auto();

            if (!bundle.TryGetAsset(out TMP_FontAsset textMeshPro, BUNDLE_TEXT_MESH_PRO_ASSET)
                || !bundle.TryGetAsset(out FontAsset uiToolkit, BUNDLE_UI_TOOLKIT_ASSET))
            {
                bundle.Dereference();
                throw new FontLoadException($"\"{intention.Src}\": its converted font bundle holds no font assets, the built-in font stays");
            }

            string? defect = FindDefect(textMeshPro.atlasTextures, textMeshPro.atlasWidth, textMeshPro.atlasHeight, textMeshPro.sourceFontFile)
                              ?? FindDefect(uiToolkit.atlasTextures, uiToolkit.atlasWidth, uiToolkit.atlasHeight, uiToolkit.sourceFontFile);

            if (defect != null)
            {
                bundle.Dereference();
                throw new FontLoadException($"\"{intention.Src}\": its converted font bundle holds an unusable font ({defect}), the built-in font stays");
            }

            return new FontData(fontAssetFactory.AdoptBundled(intention.Src, textMeshPro, uiToolkit), bundle);
        }

        private static string? FindDefect(Texture2D[]? atlasTextures, int atlasWidth, int atlasHeight, Font? sourceFontFile)
        {
            if (atlasTextures is not { Length: > 0 } || atlasTextures[0] == null)
                return "no atlas texture";

            if (atlasWidth is <= 0 or > MAX_ATLAS_SIZE || atlasHeight is <= 0 or > MAX_ATLAS_SIZE)
                return $"atlas size {atlasWidth}x{atlasHeight}";

            if (sourceFontFile == null)
                return "no source font";

            return null;
        }
    }
}
