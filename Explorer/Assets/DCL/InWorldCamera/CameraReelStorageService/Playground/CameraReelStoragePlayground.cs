using DCL.Browser.DecentralandUrls;
using DCL.Multiplayer.Connections.DecentralandUrls;
using DCL.Web3.Identities;
using DCL.WebRequests;
using UnityEngine;

namespace DCL.InWorldCamera.Playground
{
    public class CameraReelStoragePlayground : MonoBehaviour
    {
        public DecentralandEnvironment env;
        private CameraReelWebRequestClient client;

        public void Initialize()
        {
            var urlsSource = new DecentralandUrlsSource(env);
            client = new CameraReelWebRequestClient(new IWeb3IdentityCache.Default(), IWebRequestController.DEFAULT, urlsSource);
        }

        [ContextMenu(nameof(TEST))]
        public void TEST()
        {
            Initialize();
            client.Test();
        }
    }
}
