using Arch.Core;
using Cysharp.Threading.Tasks;
using SceneRuntime.Apis.Modules.PortableExperiencesApi;
using System.Collections.Generic;
using System.Threading;

namespace PortableExperiences.Controller
{
    public interface IPortableExperiencesController
    {
        Dictionary<string, Entity> PortableExperienceEntities { get; }

        bool CanKillPortableExperience(string ens);

        UniTask<IPortableExperiencesApi.SpawnResponse> CreatePortableExperienceAsync(string ens, string urn, CancellationToken ct, bool isGlobalPortableExperience = false);

        UniTask<IPortableExperiencesApi.ExitResponse> UnloadPortableExperienceAsync(string ens, CancellationToken ct);

        List<IPortableExperiencesApi.SpawnResponse> GetAllPortableExperiences();

    }
}
