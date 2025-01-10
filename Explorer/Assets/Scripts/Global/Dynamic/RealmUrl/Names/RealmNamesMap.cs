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

        public async UniTask<string> UrlFromNameAsync(string name, CancellationToken token)
        {
            IReadOnlyList<NodeDTO> nodes = await NodesAsync(token);

            foreach (NodeDTO nodeDTO in nodes)
            {
                string nodeName = await NameOfNodeAsync(nodeDTO);

                if (nodeName == name)
                    return nodeDTO.BaseUrl;
            }

            throw new Exception($"Node with name '{name}' not found");
        }

        private async UniTask<IReadOnlyList<NodeDTO>> NodesAsync(CancellationToken token)
        {
            if (cachedNodes == null)
            {
                CommonArguments arguments = "https://peer.decentraland.org/lambdas/contracts/servers";

                cachedNodes = await webRequestController
                                   .GetAsync(arguments, token, ReportCategory.GENERIC_WEB_REQUEST)
                                   .CreateFromJson<List<NodeDTO>>(WRJsonParser.Newtonsoft);
            }

            return cachedNodes;
        }

        private async UniTask<string> NameOfNodeAsync(NodeDTO nodeDTO)
        {
            if (cachedUrlToNameDictionary.TryGetValue(nodeDTO.BaseUrl, out string? name) == false)
            {
                CommonArguments arguments = $"{nodeDTO.baseUrl}/about";

                var about = await webRequestController
                                 .GetAsync(arguments, CancellationToken.None, ReportCategory.GENERIC_WEB_REQUEST)
                                 .CreateFromJson<NodeAboutDTO>(WRJsonParser.Newtonsoft);

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
