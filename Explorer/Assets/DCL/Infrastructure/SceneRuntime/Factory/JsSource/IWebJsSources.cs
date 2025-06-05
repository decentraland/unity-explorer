using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using System;
using System.Threading;

namespace SceneRuntime.Factory.WebSceneSource
{
    public interface IWebJsSources
    {
        UniTask<string> SceneSourceCodeAsync(Uri path, CancellationToken ct);
    }
}
