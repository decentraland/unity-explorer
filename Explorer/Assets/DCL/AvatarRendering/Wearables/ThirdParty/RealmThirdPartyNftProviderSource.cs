using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.WebRequests;
using ECS;
using System;
using System.Collections.Generic;
using System.Threading;

namespace DCL.AvatarRendering.Wearables.ThirdParty
{
    public class RealmThirdPartyNftProviderSource : IThirdPartyNftProviderSource
    {
        private readonly IWebRequestController webRequestController;
        private readonly IRealmData realmData;

        private ThirdPartyNftProviderDefinition[]? providers;

        public RealmThirdPartyNftProviderSource(IWebRequestController webRequestController,
            IRealmData realmData)
        {
            this.webRequestController = webRequestController;
            this.realmData = realmData;
        }

        public async UniTask<IReadOnlyList<ThirdPartyNftProviderDefinition>> GetAsync(ReportData reportData, CancellationToken ct)
        {
            if (providers != null) return providers;
            URLBuilder urlBuilder = new URLBuilder();

            URLDomain lambdasBase = realmData.Ipfs.LambdasBaseUrl;
            if (lambdasBase.IsEmpty)
                throw new InvalidOperationException("Realm lambdas base URL is not configured; cannot load third-party providers.");
            URLAddress url = urlBuilder.AppendDomain(lambdasBase)
                                              .AppendPath(URLPath.FromString("third-party-integrations"))
                                              .Build();

            GenericDownloadHandlerUtils.Adapter<GenericGetRequest, GenericGetArguments> request = webRequestController.GetAsync(new CommonArguments(url), ct, reportData);
            ThirdPartyProviderListJsonDto providersDto = await request.CreateFromJson<ThirdPartyProviderListJsonDto>(WRJsonParser.Unity);
            providers = providersDto.data;
            return providers;
        }
    }
}
