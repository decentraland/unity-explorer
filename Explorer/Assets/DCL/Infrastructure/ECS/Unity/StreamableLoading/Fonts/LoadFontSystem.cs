using Arch.Core;
using Arch.SystemGroups;
using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.WebRequests;
using ECS.Prioritization.Components;
using ECS.StreamableLoading.Cache;
using ECS.StreamableLoading.Common.Components;
using ECS.StreamableLoading.Common.Systems;
using System;
using System.Threading;

namespace ECS.StreamableLoading.Fonts
{
    [UpdateInGroup(typeof(StreamableLoadingGroup))]
    [LogCategory(ReportCategory.SDK_FONTS)]
    public partial class LoadFontSystem : LoadSystemBase<FontData, GetFontIntention>
    {
        private readonly IWebRequestController webRequestController;
        private readonly RuntimeFontAssetFactory fontAssetFactory;
        private readonly FontFileStore fileStore;

        internal LoadFontSystem(World world, IStreamableCache<FontData, GetFontIntention> cache, IWebRequestController webRequestController,
            RuntimeFontAssetFactory fontAssetFactory, FontFileStore fileStore) : base(world, cache)
        {
            this.webRequestController = webRequestController;
            this.fontAssetFactory = fontAssetFactory;
            this.fileStore = fileStore;
        }

        protected override void DisposeAbandonedResult(FontData asset) =>
            asset.Dispose();

        protected override async UniTask<StreamableLoadingResult<FontData>> FlowInternalAsync(GetFontIntention intention, StreamableLoadingState state, IPartitionComponent partition, CancellationToken ct)
        {
            var files = new FontFileStore.Lease?[4];
            bool transferred = false;

            try
            {
                if (intention.Kind == FontSourceKind.FontsourceFamily)
                    await DownloadFamilyAsync(intention, files, ct);
                else
                    files[0] = await DownloadAsync(intention.CommonArguments, intention.Src, ct);

                await UniTask.SwitchToMainThread(ct);

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

        private async UniTask DownloadFamilyAsync(GetFontIntention intention, FontFileStore.Lease?[] files, CancellationToken ct)
        {
            FontsourceFamilyRecord record = await webRequestController.GetAsync(intention.CommonArguments, ct, GetReportData())
                                                                      .CreateFromNewtonsoftJsonAsync<FontsourceFamilyRecord>();

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
            byte[] bytes = await webRequestController.GetAsync(arguments, ct, GetReportData()).GetDataCopyAsync();

            if (bytes.Length > FontFileStore.MAX_FILE_BYTES)
                throw new FontLoadException($"\"{src}\": {arguments.URL} is {bytes.Length} bytes, scene fonts are capped at {FontFileStore.MAX_FILE_BYTES} bytes");

            if (!FontFileStore.LooksLikeTrueTypeFont(bytes))
                throw new FontLoadException($"\"{src}\": {arguments.URL} is not a supported TrueType font file");

            return await fileStore.StoreAsync(bytes, ct);
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
