﻿using Arch.Core;
using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using Global.Dynamic;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Threading;

namespace PortableExperiences.Controller
{
    public interface IPortableExperiencesController
    {
        Dictionary<ENS, Entity> PortableExperienceEntities { get; }

        bool CanKillPortableExperience(ENS ens);

        UniTask<SpawnResponse> CreatePortableExperienceByEnsAsync(ENS ens, CancellationToken ct, bool isGlobalPortableExperience = false, bool force = false);

        ExitResponse UnloadPortableExperienceByEns(ENS ens);

        List<SpawnResponse> GetAllPortableExperiences();
        GlobalWorld GlobalWorld { get; set; }

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

        void UnloadAllPortableExperiences();
    }
}
