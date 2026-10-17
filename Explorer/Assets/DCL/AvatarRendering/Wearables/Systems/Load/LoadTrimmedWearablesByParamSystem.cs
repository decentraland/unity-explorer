using Arch.Core;
using Arch.SystemGroups;
using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.AvatarRendering.Loading;
using DCL.AvatarRendering.Loading.Systems.Abstract;
using DCL.AvatarRendering.Wearables.Components;
using DCL.AvatarRendering.Wearables.Components.Intentions;
using DCL.AvatarRendering.Wearables.Helpers;
using DCL.Diagnostics;
using DCL.Multiplayer.Connections.DecentralandUrls;
using DCL.WebRequests;
using ECS;
using ECS.Groups;
using ECS.StreamableLoading.Cache;

namespace DCL.AvatarRendering.Wearables.Systems.Load
{
    [UpdateInGroup(typeof(LoadGlobalSystemGroup))]
    [LogCategory(ReportCategory.WEARABLE)]
    public partial class LoadTrimmedWearablesByParamSystem : LoadTrimmedElementsByIntentionSystem<TrimmedWearablesResponse, GetTrimmedWearableByParamIntention, ITrimmedWearable, TrimmedWearableDTO, IWearable, WearableDTO>
    {
        public LoadTrimmedWearablesByParamSystem(
            World world,
            IWebRequestController webRequestController,
            IStreamableCache<TrimmedWearablesResponse, GetTrimmedWearableByParamIntention> cache,
            IRealmData realmData,
            URLSubdirectory wearablesSubdirectory,
            IDecentralandUrlsSource urlsSource,
            IWearableStorage wearableStorage,
            ITrimmedWearableStorage trimmedWearableStorage,
            string? builderContentURL = null
        ) : base(world, cache, trimmedWearableStorage, wearableStorage, realmData, wearablesSubdirectory,
            webRequestController,"wearable", urlsSource, builderContentURL: builderContentURL)
        {
        }

        protected override TrimmedWearablesResponse AssetFromPreparedIntention(in GetTrimmedWearableByParamIntention intention) =>
            new (intention.Results, intention.TotalAmount);

        protected override async UniTask<IAttachmentLambdaResponse<ILambdaResponseElement<TrimmedWearableDTO>>> ParseResponseAsync(GenericDownloadHandlerUtils.Adapter<GenericGetRequest, GenericGetArguments> adapter) =>
            await adapter.CreateFromJson<TrimmedWearableDTO.LambdaResponse>(WRJsonParser.Newtonsoft);

        protected override async UniTask<IBuilderLambdaResponse<IBuilderLambdaResponseElement<WearableDTO>>> ParseBuilderResponseAsync(GenericDownloadHandlerUtils.Adapter<GenericGetRequest, GenericGetArguments> adapter) =>
            await adapter.CreateFromJson<BuilderWearableDTO.BuilderLambdaResponse>(WRJsonParser.Newtonsoft);
    }
}
