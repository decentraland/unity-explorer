using Arch.Core;
using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using SceneRuntime.Apis.Modules.PortableExperiencesApi;
using System.Collections.Generic;
using System.Threading;

namespace PortableExperiences.Controller
{
    public interface IPortableExperiencesController
    {
        Dictionary<ENS, Entity> PortableExperienceEntities { get; }

        bool CanKillPortableExperience(ENS ens);

        UniTask<IPortableExperiencesApi.SpawnResponse> CreatePortableExperienceAsync(ENS ens, URN urn, CancellationToken ct, bool isGlobalPortableExperience = false);

        UniTask<IPortableExperiencesApi.ExitResponse> UnloadPortableExperienceAsync(ENS ens, CancellationToken ct);

        List<IPortableExperiencesApi.SpawnResponse> GetAllPortableExperiences();

    }
}
