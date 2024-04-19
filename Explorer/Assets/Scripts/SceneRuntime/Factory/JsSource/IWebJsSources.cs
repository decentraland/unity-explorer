using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using System.Threading;

namespace SceneRuntime.Factory.WebSceneSource
{
    public interface IWebJsSources
    {
        UniTask<string> SceneSourceCode(URLAddress path, CancellationToken ct);
    }
}
