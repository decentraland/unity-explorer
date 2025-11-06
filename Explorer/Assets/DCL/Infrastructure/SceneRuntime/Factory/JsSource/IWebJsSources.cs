using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using System.Threading;
using UnityEngine.Networking;

namespace SceneRuntime.Factory.WebSceneSource
{
    public interface IWebJsSources
    {
        public UniTask<DownloadHandler> SceneSourceCodeAsync(URLAddress path, CancellationToken ct);
    }
}
