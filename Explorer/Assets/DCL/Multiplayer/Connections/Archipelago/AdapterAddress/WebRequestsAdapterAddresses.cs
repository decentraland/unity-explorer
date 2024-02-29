using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.WebRequests;
using System;
using System.Threading;

namespace DCL.Multiplayer.Connections.Archipelago.AdapterAddress
{
    public class WebRequestsAdapterAddresses : IAdapterAddresses
    {
        private readonly IWebRequestController webRequestController;

        public WebRequestsAdapterAddresses() : this(IWebRequestController.DEFAULT) { }

        public WebRequestsAdapterAddresses(IWebRequestController webRequestController)
        {
            this.webRequestController = webRequestController;
        }

        public async UniTask<string> AdapterUrlAsync(string aboutUrl, CancellationToken token)
        {
            try
            {
                GenericGetRequest result = await webRequestController.GetAsync(
                    new CommonArguments(URLAddress.FromString(aboutUrl)),
                    token,
                    ReportCategory.ARCHIPELAGO_REQUEST
                );

                FullBody? parsed = await result.CreateFromJson<FullBody>(WRJsonParser.Unity);
                return parsed.AdapterUrl();
            }
            catch (Exception e) { throw new Exception("Error getting adapter url", e); }
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
