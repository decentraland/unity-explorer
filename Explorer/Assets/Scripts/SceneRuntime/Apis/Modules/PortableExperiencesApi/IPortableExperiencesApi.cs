using Cysharp.Threading.Tasks;
using System;
using System.Threading;

namespace SceneRuntime.Apis.Modules.PortableExperiencesApi
{
    public interface IPortableExperiencesApi : IDisposable
    {
        UniTask<object> SpawnAsync(string pid, string ens, CancellationToken ct);

        UniTask<bool> KillAsync(string ens, CancellationToken ct);

        UniTask<bool> ExitAsync(string predefinedEmote, CancellationToken ct);

        bool GetPortableExperiencesLoadedAsync(CancellationToken ct);
    }
}
