using Cysharp.Threading.Tasks;
using DCL.WebRequests;
using System;
using System.Threading;
using UnityEngine;

namespace DCL.Multiplayer.Connections.Credentials.Hub
{
    public class WebRequestsRoomCredentialsHub : ICredentialsHub
    {
        private IWebRequestController webRequestController;

        public WebRequestsRoomCredentialsHub(IWebRequestController webRequestController)
        {
            this.webRequestController = webRequestController;
        }

        public UniTask<ICredentials> SceneRoomCredentials(Vector2Int parcelPosition, CancellationToken token)
        {
            throw new NotImplementedException();
        }

        public UniTask<ICredentials> IslandRoomCredentials(CancellationToken token)
        {
            throw new NotImplementedException();
        }
    }
}
