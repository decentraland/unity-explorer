using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.Multiplayer.Connections.DecentralandUrls;
using DCL.WebRequests;
using System.Collections.Generic;
using System.Threading;

namespace DCL.AvatarRendering.Wearables.ThirdParty
{
    public class DecentralandHttpThirdPartyNftProviderSource : IThirdPartyNftProviderSource
    {
        private readonly IWebRequestController webRequestController;
        private readonly IDecentralandUrlsSource decentralandUrls;

        private List<ThirdPartyNftProviderDefinition>? providers;

        public DecentralandHttpThirdPartyNftProviderSource(IWebRequestController webRequestController,
            IDecentralandUrlsSource decentralandUrls)
        {
            this.webRequestController = webRequestController;
            this.decentralandUrls = decentralandUrls;
        }

        public async UniTask<IReadOnlyList<ThirdPartyNftProviderDefinition>> GetAsync(CancellationToken ct)
        {
            if (providers != null) return providers;
            var url = URLAddress.FromString(decentralandUrls.Url(DecentralandUrl.ThirdPartyNftProviders));
            var request = webRequestController.GetAsync(new CommonArguments(url), ct);
            ThirdPartyProviderListJsonDto providersDto = await request.CreateFromJson<ThirdPartyProviderListJsonDto>(WRJsonParser.Unity);
            providers = new List<ThirdPartyNftProviderDefinition>(providersDto.thirdPartyProviders);
            return providers;
        }
    }
}
