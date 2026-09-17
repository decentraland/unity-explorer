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

        protected override async UniTask<StreamableLoadingResult<FontData>> FlowInternalAsync(GetFontIntention intention, StreamableLoadingState state, IPartitionComponent partition, CancellationToken ct)
        {
            string regularPath;
            string? boldPath = null;
            string? italicPath = null;
            string? boldItalicPath = null;

            if (intention.Kind == FontSourceKind.FontsourceFamily)
                (regularPath, boldPath, italicPath, boldItalicPath) = await DownloadFamilyAsync(intention, ct);
            else
                regularPath = await DownloadAsync(intention.CommonArguments, intention.Src, ct);

            await UniTask.SwitchToMainThread(ct);

            FontFamilyAssets? assets = fontAssetFactory.Create(intention.Src, regularPath, boldPath, italicPath, boldItalicPath);

            if (assets == null)
                throw new FontLoadException($"\"{intention.Src}\" is not a font FreeType can read");

            return new StreamableLoadingResult<FontData>(new FontData(assets));
        }

        private async UniTask<(string regularPath, string? boldPath, string? italicPath, string? boldItalicPath)> DownloadFamilyAsync(GetFontIntention intention, CancellationToken ct)
        {
            FontsourceFamilyRecord record = await webRequestController.GetAsync(intention.CommonArguments, ct, GetReportData())
                                                                      .CreateFromNewtonsoftJsonAsync<FontsourceFamilyRecord>();

            if (!FontsourceCatalog.TryGetVariantUrl(record, FontVariant.Regular, out string regularUrl))
                throw new FontLoadException($"The font family \"{intention.Src}\" has no regular face on Fontsource");

            string regularPath = await DownloadAsync(intention.CommonArguments.WithURL(URLAddress.FromString(regularUrl)), intention.Src, ct);

            (string? boldPath, string? italicPath, string? boldItalicPath) = await UniTask.WhenAll(
                DownloadVariantIfPresentAsync(record, FontVariant.Bold, intention.CommonArguments, intention.Src, ct),
                DownloadVariantIfPresentAsync(record, FontVariant.Italic, intention.CommonArguments, intention.Src, ct),
                DownloadVariantIfPresentAsync(record, FontVariant.BoldItalic, intention.CommonArguments, intention.Src, ct));

            return (regularPath, boldPath, italicPath, boldItalicPath);
        }

        private async UniTask<string> DownloadAsync(CommonArguments arguments, string src, CancellationToken ct)
        {
            byte[] bytes = await webRequestController.GetAsync(arguments, ct, GetReportData()).GetDataCopyAsync();

            if (bytes.Length > FontFileStore.MAX_FILE_BYTES)
                throw new FontLoadException($"\"{src}\": {arguments.URL} is {bytes.Length} bytes, scene fonts are capped at {FontFileStore.MAX_FILE_BYTES} bytes");

            if (!FontFileStore.LooksLikeFontFile(bytes))
                throw new FontLoadException($"\"{src}\": {arguments.URL} is not a TrueType/OpenType font file");

            return await fileStore.StoreAsync(bytes, ct);
        }

        private async UniTask<string?> DownloadVariantIfPresentAsync(FontsourceFamilyRecord record, FontVariant variant, CommonLoadingArguments commonArguments, string src, CancellationToken ct)
        {
            if (!FontsourceCatalog.TryGetVariantUrl(record, variant, out string url))
                return null;

            try { return await DownloadAsync(commonArguments.WithURL(URLAddress.FromString(url)), src, ct); }
            catch (Exception e) when (e is not OperationCanceledException)
            {
                ReportHub.LogWarning(GetReportData(), $"\"{src}\": the {variant} face failed to load, the regular face stands in: {e.Message}");
                return null;
            }
        }
    }
}
