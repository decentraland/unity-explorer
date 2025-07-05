using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.Utilities.Extensions;
using DCL.WebRequests;
using System;
using System.Collections.Generic;
using System.Threading;

namespace Global.Dynamic.RealmUrl.Names
{
    public class RealmNamesMap : IRealmNamesMap
    {
        private readonly IWebRequestController webRequestController;
        private readonly Dictionary<string, string> cachedUrlToNameDictionary = new ();
        private IReadOnlyList<NodeDTO>? cachedNodes;

        public RealmNamesMap(IWebRequestController webRequestController)
        {
            this.webRequestController = webRequestController;
        }

        public async UniTask<Uri> UrlFromNameAsync(string name, CancellationToken token)
        {
            IReadOnlyList<NodeDTO> nodes = await NodesAsync(token);

            foreach (NodeDTO nodeDTO in nodes)
            {
                string nodeName = await NameOfNodeAsync(nodeDTO, token);

                if (nodeName == name)
                    return new Uri(nodeDTO.BaseUrl);
            }

            throw new Exception($"Node with name '{name}' not found");
        }

        private static readonly Uri SERVERS_URI = new ("https://peer.decentraland.org/lambdas/contracts/servers");

        private async UniTask<IReadOnlyList<NodeDTO>> NodesAsync(CancellationToken token)
        {
            if (cachedNodes == null)
            {
                CommonArguments arguments = SERVERS_URI;

                cachedNodes = await webRequestController
                                   .GetAsync(arguments, ReportCategory.GENERIC_WEB_REQUEST)
                                   .CreateFromJsonAsync<List<NodeDTO>>(WRJsonParser.Newtonsoft, token);
            }

            return cachedNodes;
        }

        private async UniTask<string> NameOfNodeAsync(NodeDTO nodeDTO, CancellationToken ct)
        {
            if (cachedUrlToNameDictionary.TryGetValue(nodeDTO.BaseUrl, out string? name) == false)
            {
                CommonArguments arguments = new Uri($"{nodeDTO.baseUrl}/about");

                var about = await webRequestController
                                 .GetAsync(arguments, ReportCategory.GENERIC_WEB_REQUEST)
                                 .CreateFromJsonAsync<NodeAboutDTO>(WRJsonParser.Newtonsoft, ct);

                cachedUrlToNameDictionary[nodeDTO.BaseUrl] = name = about.RealmName();
            }

            return name!;
        }

        [Serializable]
        private class NodeDTO
        {
            public string? baseUrl;

            public string BaseUrl => baseUrl.EnsureNotNull("Node's baseUrl is null");
        }

        [Serializable]
        private class NodeAboutDTO
        {
            public Configurations? configurations;

            public string RealmName() =>
                configurations.EnsureNotNull("Node's configurations is null").realmName.EnsureNotNull("Node's realmName is null");
        }

        [Serializable]
        private class Configurations
        {
            public string? realmName;
        }
    }
}
