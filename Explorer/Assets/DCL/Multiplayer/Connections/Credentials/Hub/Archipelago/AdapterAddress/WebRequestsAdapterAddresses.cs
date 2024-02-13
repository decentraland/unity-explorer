using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.Web3.Identities;
using DCL.WebRequests;
using DCL.WebRequests.Analytics;
using System;
using System.Threading;

namespace DCL.Multiplayer.Connections.Credentials.Hub.Archipelago.AdapterAddress
{
    public class WebRequestsAdapterAddresses : IAdapterAddresses
    {
        private readonly IWebRequestController webRequestController;

        public WebRequestsAdapterAddresses() : this(
            new WebRequestController(
                new WebRequestsAnalyticsContainer(),
                new PlayerPrefsIdentityProvider(
                    new PlayerPrefsIdentityProvider.DecentralandIdentityWithNethereumAccountJsonSerializer()
                )
            )
        ) { }

        public WebRequestsAdapterAddresses(IWebRequestController webRequestController)
        {
            this.webRequestController = webRequestController;
        }

        public async UniTask<string> AdapterUrlAsync(string aboutUrl, CancellationToken token)
        {
            try
            {
                var result = await webRequestController.GetAsync(
                    new CommonArguments(URLAddress.FromString(aboutUrl)),
                    token,
                    ReportCategory.ARCHIPELAGO_REQUEST
                );

                var parsed = await result.CreateFromJson<FullBody>(WRJsonParser.Unity);
                return parsed.AdapterUrl();
            }
            catch (Exception e)
            {
                throw new Exception("Error getting adapter url", e);
            }
        }

        [Serializable]
        public class FullBody
        {
            public Comms? comms;

            public string AdapterUrl() =>
                comms?.adapter ?? throw new Exception("Adapter is not presented in the response body");
        }

        [Serializable]
        public class Comms
        {
            public string? adapter;
        }
    }
}
