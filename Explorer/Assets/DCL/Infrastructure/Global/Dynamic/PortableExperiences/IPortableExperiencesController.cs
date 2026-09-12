using Arch.Core;
using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using Global.Dynamic;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using Utility.PortableExperiences;

namespace PortableExperiences.Controller
{
    public interface IPortableExperiencesController : IPortableExperiencesLifecycle
    {
        Dictionary<string, Entity> PortableExperienceEntities { get; }

        GlobalWorld GlobalWorld { get; set; }

        /// <summary>
        ///     Assigned from the composition root once the UI shell exists.
        /// </summary>
        IPortableExperienceAuthorizationHandler? AuthorizationHandler { get; set; }

        /// <param name="ens">ENS name that resolves to the world hosting the Portable Experience.</param>
        /// <param name="ct">Cancels the spawn flow.</param>
        /// <param name="isGlobalPortableExperience">Marks the Portable Experience as global instead of scene-spawned.</param>
        /// <param name="force">Bypasses the feature-flag and parent-scene permission checks.</param>
        /// <param name="requireUserAuthorization">Gates the spawn behind user consent even when it is global or forced; scene-spawned local Portable Experiences are always gated.</param>
        UniTask<SpawnResponse> CreatePortableExperienceByEnsAsync(ENS ens, CancellationToken ct, bool isGlobalPortableExperience = false, bool force = false, bool requireUserAuthorization = false);

        ExitResponse UnloadPortableExperienceById(string id);

        /// <summary>
        ///     Unloads the Portable Experience and records it as killed in the status tracker matching its type.
        /// </summary>
        ExitResponse KillPortableExperienceById(string id);

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
