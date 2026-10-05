using Arch.Core;
using Arch.SystemGroups;
using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
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
        private const string CONVERTER_TEXT_MESH_PRO_ASSET_NAME = "tmp";
        private const string CONVERTER_UI_TOOLKIT_ASSET_NAME = "uitk";

        private const int MAX_ATLAS_SIZE = 4096;

        private static readonly ProfilerMarker READ_BUNDLED_FONT_MARKER = new ($"{nameof(LoadFontSystem)}.ReadBundledFont");

        private readonly RuntimeFontAssetFactory fontAssetFactory;
        private readonly bool tryUnlistedBundles;

        /// <param name="tryUnlistedBundles">
        ///     Set for local scene development with local asset bundles. The files[] of the scene manifest are not loaded there,
        ///     so every font file is tried as a bundle.
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
            if (intention.Bundle is not { } bundle)
                throw new FontLoadException($"\"{intention.Src}\": the scene has no converted asset bundles to load the font from, the built-in font stays", LogType.Log);

            if (!bundle.Listed && !tryUnlistedBundles)
                throw new FontLoadException($"\"{intention.Src}\": the scene's asset bundle manifest lists no converted font bundle for it, the built-in font stays", LogType.Log);

            return new StreamableLoadingResult<FontData>(await LoadConvertedAsync(intention, bundle, state, partition, ct));
        }

        private async UniTask<FontData> LoadConvertedAsync(GetFontIntention intention, ConvertedFontBundle converted, StreamableLoadingState state, IPartitionComponent partition, CancellationToken ct)
        {
            await UniTask.SwitchToMainThread(ct);

            var promise = AssetBundlePromise.Create(World,
                GetAssetBundleIntention.FromHash(converted.Manifest.GetCdnRequestHash(converted.Hash), converted.Manifest, parentEntityID: converted.SceneId),
                partition);

            // The bundle promise needs a slot from the same loading budget, so holding this slot while waiting for it can deadlock
            state.AcquiredBudget?.Release();

            try { promise = await promise.ToUniTaskAsync(World, cancellationToken: ct); }
            catch (OperationCanceledException)
            {
                // The bundle can resolve and take a reference before the cancellation is observed
                promise.TryDereference(World);
                promise.ForgetLoading(World);
                throw;
            }

            if (!promise.TryGetResult(World, out StreamableLoadingResult<AssetBundleData> result) || result is not { Succeeded: true, Asset: { } bundle })
                throw new FontLoadException($"\"{intention.Src}\": its converted font bundle did not load, the built-in font stays: {result.Exception?.Message}",
                    converted.Listed ? LogType.Warning : LogType.Log);

            using ProfilerMarker.AutoScope _ = READ_BUNDLED_FONT_MARKER.Auto();

            try
            {
                if (!bundle.TryGetAsset(out TMP_FontAsset textMeshPro, CONVERTER_TEXT_MESH_PRO_ASSET_NAME)
                    || !bundle.TryGetAsset(out FontAsset uiToolkit, CONVERTER_UI_TOOLKIT_ASSET_NAME))
                    throw new FontLoadException($"\"{intention.Src}\": its converted font bundle holds no font assets, the built-in font stays");

                string? defect = FindDefect(textMeshPro.atlasTextures, textMeshPro.atlasWidth, textMeshPro.atlasHeight, textMeshPro.sourceFontFile)
                                  ?? FindDefect(uiToolkit.atlasTextures, uiToolkit.atlasWidth, uiToolkit.atlasHeight, uiToolkit.sourceFontFile);

                if (defect != null)
                    throw new FontLoadException($"\"{intention.Src}\": its converted font bundle holds an unusable font ({defect}), the built-in font stays");

                return new FontData(fontAssetFactory.AdoptBundled(intention.Src, textMeshPro, uiToolkit), bundle);
            }
            catch
            {
                bundle.Dereference();
                throw;
            }
        }

        internal static string? FindDefect(Texture2D[]? atlasTextures, int atlasWidth, int atlasHeight, Font? sourceFontFile)
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
