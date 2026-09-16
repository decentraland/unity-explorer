using Arch.Core;
using Arch.SystemGroups;
using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.WebRequests;
using ECS.Prioritization.Components;
using ECS.StreamableLoading.Cache;
using ECS.StreamableLoading.Common.Components;
using ECS.StreamableLoading.Common.Systems;
using System.Threading;

namespace ECS.StreamableLoading.Fonts
{
    [UpdateInGroup(typeof(StreamableLoadingGroup))]
    [LogCategory(ReportCategory.FONTS)]
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
            string filePath = await DownloadAsync(intention.CommonArguments, intention.Src, ct);

            await UniTask.SwitchToMainThread(ct);

            FontFamilyAssets? assets = fontAssetFactory.Create(intention.Src, filePath);

            if (assets == null)
                throw new FontLoadException($"\"{intention.Src}\" is not a font FreeType can read");

            return new StreamableLoadingResult<FontData>(new FontData(assets));
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
    }
}
