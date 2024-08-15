using Arch.Core;
using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Threading;

namespace PortableExperiences.Controller
{
    public interface IPortableExperiencesController
    {
        Dictionary<ENS, Entity> PortableExperienceEntities { get; }

        bool CanKillPortableExperience(ENS ens);

        UniTask<SpawnResponse> CreatePortableExperienceByEnsAsync(ENS ens, CancellationToken ct, bool isGlobalPortableExperience = false);

        UniTask<ExitResponse> UnloadPortableExperienceByEnsAsync(ENS ens, CancellationToken ct);

        List<SpawnResponse> GetAllPortableExperiences();

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
