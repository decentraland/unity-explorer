using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.Multiplayer.Connections.DecentralandUrls;
using DCL.WebRequests;
using ECS;
using System.Collections.Generic;
using System.Threading;
using UnityEngine.Pool;

namespace DCL.AvatarRendering.Wearables.ThirdParty
{
    public class RealmThirdPartyNftProviderSource : IThirdPartyNftProviderSource
    {
        private readonly IWebRequestController webRequestController;
        private readonly IDecentralandUrlsSource urlsSource;

        private ThirdPartyNftProviderDefinition[]? providers;

        public RealmThirdPartyNftProviderSource(IWebRequestController webRequestController,
            IDecentralandUrlsSource urlsSource)
        {
            this.webRequestController = webRequestController;
            this.urlsSource = urlsSource;
        }

        public async UniTask<IReadOnlyList<ThirdPartyNftProviderDefinition>> GetAsync(ReportData reportData, CancellationToken ct)
        {
            if (providers != null) return providers;

            using PooledObject<URLBuilder> _ = urlsSource.BuildFromDomain(DecentralandUrl.Lambdas, out URLBuilder urlBuilder);

            URLAddress url = urlBuilder.AppendPath(URLPath.FromString("third-party-integrations"))
                                       .Build();

            GenericDownloadHandlerUtils.Adapter<GenericGetRequest, GenericGetArguments> request = webRequestController.GetAsync(new CommonArguments(url), ct, reportData);
            ThirdPartyProviderListJsonDto providersDto = await request.CreateFromJson<ThirdPartyProviderListJsonDto>(WRJsonParser.Unity);
            providers = providersDto.data;
            return providers;
        }
    }
}
