using Arch.Core;
using Arch.SystemGroups;
using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.Ipfs;
using DCL.WebRequests;
using ECS.Prioritization.Components;
using ECS.StreamableLoading.AssetBundles;
using ECS.StreamableLoading.Cache;
using ECS.StreamableLoading.Common;
using ECS.StreamableLoading.Common.Components;
using ECS.StreamableLoading.Common.Systems;
using Newtonsoft.Json;
using System;
using System.Text;
using System.Threading;
using TMPro;
using Unity.Profiling;
using UnityEngine.TextCore.Text;
using AssetBundlePromise = ECS.StreamableLoading.Common.AssetPromise<ECS.StreamableLoading.AssetBundles.AssetBundleData, ECS.StreamableLoading.AssetBundles.GetAssetBundleIntention>;

namespace ECS.StreamableLoading.Fonts
{
    [UpdateInGroup(typeof(StreamableLoadingGroup))]
    [LogCategory(ReportCategory.SDK_FONTS)]
    public partial class LoadFontSystem : LoadSystemBase<FontData, GetFontIntention>
    {
        private const int MAX_CATALOG_BYTES = 1024 * 1024;

        // The names the converter gives a font bundle's two font assets (abgen builder::font).
        private const string BUNDLE_TEXT_MESH_PRO_ASSET = "tmp";
        private const string BUNDLE_UI_TOOLKIT_ASSET = "uitk";

        private static readonly ProfilerMarker READ_BUNDLED_FONT_MARKER = new ($"{nameof(LoadFontSystem)}.ReadBundledFont");

        private readonly IWebRequestController webRequestController;
        private readonly RuntimeFontAssetFactory fontAssetFactory;
        private readonly FontFileStore fileStore;
        private readonly bool tryUnlistedBundles;

        /// <param name="tryUnlistedBundles">
        ///     Local scene development with local asset bundles: the scene's manifest files[] are never read there, so
        ///     every scene font file is tried as a bundle and the ones the converter skipped fall back to the file.
        /// </param>
        internal LoadFontSystem(World world, IStreamableCache<FontData, GetFontIntention> cache, IWebRequestController webRequestController,
            RuntimeFontAssetFactory fontAssetFactory, FontFileStore fileStore, bool tryUnlistedBundles) : base(world, cache)
        {
            this.webRequestController = webRequestController;
            this.fontAssetFactory = fontAssetFactory;
            this.fileStore = fileStore;
            this.tryUnlistedBundles = tryUnlistedBundles;
        }

        protected override void DisposeAbandonedResult(FontData asset) =>
            asset.Dispose();

        protected override async UniTask<StreamableLoadingResult<FontData>> FlowInternalAsync(GetFontIntention intention, StreamableLoadingState state, IPartitionComponent partition, CancellationToken ct)
        {
            var files = new FontFileStore.Lease?[4];
            bool transferred = false;

            try
            {
                if (intention.AssetBundleHash != null && (intention.AssetBundleListed || tryUnlistedBundles))
                {
                    FontData? bundled = await TryLoadConvertedAsync(intention, partition, ct);

                    if (bundled != null)
                        return new StreamableLoadingResult<FontData>(bundled);
                }

                if (intention.Kind == FontSourceKind.FontsourceFamily)
                    await DownloadFamilyAsync(intention, files, ct);
                else
                    files[0] = await DownloadAsync(intention.CommonArguments, intention.Src, ct);

                await UniTask.SwitchToMainThread(ct);

                // Both download branches populate the required regular face before returning.
                FontFamilyAssets? assets = fontAssetFactory.Create(intention.Src, files[0]!.Path, files[1]?.Path, files[2]?.Path, files[3]?.Path);

                if (assets == null)
                    throw new FontLoadException($"\"{intention.Src}\" is not a font FreeType can read");

                var data = new FontData(assets, files);
                transferred = true;
                return new StreamableLoadingResult<FontData>(data);
            }
            finally
            {
                if (!transferred)
                {
                    await UniTask.SwitchToMainThread();
                    FontFileStore.ReleaseAfterDestructionAsync(files).Forget(e => ReportHub.LogException(e, ReportCategory.SDK_FONTS));
                }
            }
        }

        /// <summary>
        ///     Loads the font from the bundle the converter built for it: both font assets already built, their atlases
        ///     pre-filled with the common characters, and the source font inside for the rest. Null when the bundle
        ///     cannot be used, and the caller falls back to the font file.
        /// </summary>
        private async UniTask<FontData?> TryLoadConvertedAsync(GetFontIntention intention, IPartitionComponent partition, CancellationToken ct)
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
            {
                ReportHub.LogWarning(GetReportData(), $"\"{intention.Src}\": its converted font bundle did not load, the font file is loaded instead: {result.Exception?.Message}");
                return null;
            }

            using ProfilerMarker.AutoScope _ = READ_BUNDLED_FONT_MARKER.Auto();

            if (!bundle.TryGetAsset(out TMP_FontAsset textMeshPro, BUNDLE_TEXT_MESH_PRO_ASSET)
                || !bundle.TryGetAsset(out FontAsset uiToolkit, BUNDLE_UI_TOOLKIT_ASSET))
            {
                bundle.Dereference();
                ReportHub.LogWarning(GetReportData(), $"\"{intention.Src}\": its converted font bundle holds no font assets, the font file is loaded instead");
                return null;
            }

            return new FontData(fontAssetFactory.AdoptBundled(intention.Src, textMeshPro, uiToolkit), bundle);
        }

        private async UniTask DownloadFamilyAsync(GetFontIntention intention, FontFileStore.Lease?[] files, CancellationToken ct)
        {
            byte[] catalogBytes = await DownloadBytesAsync(intention.CommonArguments, MAX_CATALOG_BYTES, ct);
            FontsourceFamilyRecord record = JsonConvert.DeserializeObject<FontsourceFamilyRecord>(Encoding.UTF8.GetString(catalogBytes))
                                           ?? throw new FontLoadException("Fontsource returned an empty family record");

            if (!FontsourceCatalog.TryGetVariantUrl(record, FontVariant.Regular, out string regularUrl))
                throw new FontLoadException($"The font family \"{intention.Src}\" has no regular face on Fontsource");

            files[0] = await DownloadAsync(intention.CommonArguments.WithURL(URLAddress.FromString(regularUrl)), intention.Src, ct);

            // Settle all writes before the owner can release their leases on cancellation.
            var (bold, italic, boldItalic) = await UniTask.WhenAll(
                DownloadVariantIfPresentAsync(record, FontVariant.Bold, intention.CommonArguments, intention.Src, ct).SuppressCancellationThrow(),
                DownloadVariantIfPresentAsync(record, FontVariant.Italic, intention.CommonArguments, intention.Src, ct).SuppressCancellationThrow(),
                DownloadVariantIfPresentAsync(record, FontVariant.BoldItalic, intention.CommonArguments, intention.Src, ct).SuppressCancellationThrow());

            files[1] = bold.Result;
            files[2] = italic.Result;
            files[3] = boldItalic.Result;
            ct.ThrowIfCancellationRequested();
        }

        private async UniTask<FontFileStore.Lease> DownloadAsync(CommonArguments arguments, string src, CancellationToken ct)
        {
            byte[] bytes = await DownloadBytesAsync(arguments, FontFileStore.MAX_FILE_BYTES, ct);

            if (!FontFileStore.LooksLikeTrueTypeFont(bytes))
                throw new FontLoadException($"\"{src}\": {arguments.URL} is not a supported TrueType font file");

            return await fileStore.StoreAsync(bytes, ct);
        }

        private async UniTask<byte[]> DownloadBytesAsync(CommonArguments arguments, int maxBytes, CancellationToken ct)
        {
            await UniTask.SwitchToMainThread(ct);
            using var handler = new FontDownloadHandler(maxBytes);

            try
            {
                // Each attempt needs a fresh handler: disposing the request also disposes its handler.
                byte[] bytes = await webRequestController.SendAsync<GenericGetRequest, GenericGetArguments, GenericDownloadHandlerUtils.GetDataCopyOp<GenericGetRequest>, byte[]>(
                    new CommonArguments(arguments.URL, RetryPolicy.NONE, arguments.Timeout), default(GenericGetArguments),
                    new GenericDownloadHandlerUtils.GetDataCopyOp<GenericGetRequest>(), ct, GetReportData(), downloadHandler: handler);

                if (handler.LimitExceeded)
                    throw new FontLoadException($"{arguments.URL} exceeds the download limit of {maxBytes} bytes");

                return bytes;
            }
            catch (UnityWebRequestException) when (handler.LimitExceeded)
            {
                throw new FontLoadException($"{arguments.URL} exceeds the download limit of {maxBytes} bytes");
            }
        }

        private async UniTask<FontFileStore.Lease?> DownloadVariantIfPresentAsync(FontsourceFamilyRecord record, FontVariant variant, CommonLoadingArguments commonArguments, string src, CancellationToken ct)
        {
            try
            {
                if (!FontsourceCatalog.TryGetVariantUrl(record, variant, out string url))
                    return null;

                return await DownloadAsync(commonArguments.WithURL(URLAddress.FromString(url)), src, ct);
            }
            catch (Exception e) when (e is not OperationCanceledException)
            {
                ReportHub.LogWarning(GetReportData(), $"\"{src}\": the {variant} face failed to load, the regular face stands in: {e.Message}");
                return null;
            }
        }
    }
}
