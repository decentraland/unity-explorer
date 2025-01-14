using Arch.Core;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.AvatarRendering.Loading;
using DCL.AvatarRendering.Loading.Systems.Abstract;
using DCL.AvatarRendering.Wearables.Components;
using DCL.AvatarRendering.Wearables.Components.Intentions;
using DCL.AvatarRendering.Wearables.Helpers;
using DCL.Diagnostics;
using DCL.WebRequests;
using ECS;
using ECS.StreamableLoading.Cache;
using System.Collections.Generic;

namespace DCL.AvatarRendering.Wearables.Systems.Load
{
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [LogCategory(ReportCategory.WEARABLE)]
    public partial class LoadWearablesByParamSystem : LoadElementsByIntentionSystem<WearablesResponse, GetWearableByParamIntention, IWearable, WearableDTO>
    {
        private readonly URLSubdirectory lambdaSubdirectory;
        private readonly URLSubdirectory wearablesSubdirectory;
        private readonly IRealmData realmData;

        internal IURLBuilder urlBuilder = new URLBuilder();

        public LoadWearablesByParamSystem(
            World world, IWebRequestController webRequestController, IStreamableCache<WearablesResponse, GetWearableByParamIntention> cache,
            IRealmData realmData, URLSubdirectory lambdaSubdirectory, URLSubdirectory wearablesSubdirectory,
            IWearableStorage wearableStorage
        ) : base(world, cache, wearableStorage, webRequestController, realmData)
        {
            this.realmData = realmData;
            this.lambdaSubdirectory = lambdaSubdirectory;
            this.wearablesSubdirectory = wearablesSubdirectory;
        }

        protected override URLAddress BuildUrlFromIntention(in GetWearableByParamIntention intention)
        {
            string userID = intention.UserID;
            IReadOnlyList<(string, string)> urlEncodedParams = intention.Params;
            urlBuilder.Clear();

            if (intention.CommonArguments.URL == URLAddress.EMPTY)
            {
                urlBuilder.AppendDomainWithReplacedPath(realmData.Ipfs.LambdasBaseUrl, lambdaSubdirectory)
                          .AppendSubDirectory(URLSubdirectory.FromString(userID))
                          .AppendSubDirectory(wearablesSubdirectory);
            }
            else
            {
                // urlBuilder.AppendDomainWithReplacedPath(URLDomain.FromString(intention.CommonArguments.URL), URLSubdirectory.EMPTY);

                // ONLY FOR DEBUGGING
                urlBuilder.AppendDomainWithReplacedPath(URLDomain.FromString(intention.CommonArguments.URL), URLSubdirectory.FromString("/items"));
            }

            for (var i = 0; i < urlEncodedParams.Count; i++)
                urlBuilder.AppendParameter(urlEncodedParams[i]);

            return urlBuilder.Build();
        }

        protected override WearablesResponse AssetFromPreparedIntention(in GetWearableByParamIntention intention) =>
            new (intention.Results, intention.TotalAmount);

        protected override async UniTask<IAttachmentLambdaResponse<ILambdaResponseElement<WearableDTO>>> ParsedResponseAsync(GenericDownloadHandlerUtils.Adapter<GenericGetRequest, GenericGetArguments> adapter) =>
            await adapter.CreateFromJson<WearableDTO.LambdaResponse>(WRJsonParser.Unity);

        protected override async UniTask<IBuilderLambdaResponse> ParsedBuilderResponseAsync(GenericDownloadHandlerUtils.Adapter<GenericGetRequest, GenericGetArguments> adapter) =>
            await adapter.CreateFromJson<WearableDTO.BuilderLambdaResponse>(WRJsonParser.Newtonsoft);
    }
}
