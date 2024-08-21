using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.WebRequests;
using ECS;
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

        public async UniTask<IReadOnlyList<ThirdPartyNftProviderDefinition>> GetAsync(CancellationToken ct)
        {
            if (providers != null) return providers;
            URLBuilder urlBuilder = new URLBuilder();

            URLAddress url = urlBuilder.AppendDomain(realmData.Ipfs.LambdasBaseUrl)
                                              .AppendPath(URLPath.FromString("third-party-integrations"))
                                              .Build();

            var request = webRequestController.GetAsync(new CommonArguments(url), ct);
            ThirdPartyProviderListJsonDto providersDto = await request.CreateFromJson<ThirdPartyProviderListJsonDto>(WRJsonParser.Unity);
            providers = providersDto.data;
            return providers;
        }
    }
}
