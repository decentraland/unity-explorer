using Arch.Core;
using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using Global.Dynamic;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Threading;

namespace PortableExperiences.Controller
{
    public interface IPortableExperiencesController
    {
        event Action<string> PortableExperienceLoaded;

        event Action<string> PortableExperienceUnloaded;

        Dictionary<string, Entity> PortableExperienceEntities { get; }

        GlobalWorld GlobalWorld { get; set; }

        bool CanKillPortableExperience(string id);

        UniTask<SpawnResponse> CreatePortableExperienceByEnsAsync(ENS ens, CancellationToken ct, bool isGlobalPortableExperience = false, bool force = false);

        ExitResponse UnloadPortableExperienceById(string id);

        List<SpawnResponse> GetAllPortableExperiences();

        void UnloadAllPortableExperiences();

        void AddPortableExperience(string id, Entity portableExperience);

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
