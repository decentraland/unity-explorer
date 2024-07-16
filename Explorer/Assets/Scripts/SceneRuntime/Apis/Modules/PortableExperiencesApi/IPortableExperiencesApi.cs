using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Threading;

namespace SceneRuntime.Apis.Modules.PortableExperiencesApi
{
    public interface IPortableExperiencesApi : IDisposable
    {
        UniTask<SpawnResponse> SpawnAsync(URN pid, ENS ens, CancellationToken ct);

        UniTask<ExitResponse> KillAsync(ENS ens, CancellationToken ct);

        UniTask<ExitResponse> ExitAsync(CancellationToken ct);

        List<SpawnResponse> GetLoadedPortableExperiences(CancellationToken ct);

        [SuppressMessage("ReSharper", "InconsistentNaming")]
        public struct SpawnResponse
        {
            public string pid;
            public string parent_cid;
            public string name;
            public string ens;
        }

        [SuppressMessage("ReSharper", "InconsistentNaming")]
        public struct ExitResponse
        {
            public bool status;
        }
    }
}
