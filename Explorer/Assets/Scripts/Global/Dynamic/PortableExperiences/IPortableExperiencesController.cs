using Arch.Core;
using Cysharp.Threading.Tasks;
using System.Collections.Generic;
using System.Threading;

namespace PortableExperiences.Controller
{
    public interface IPortableExperiencesController
    {
        Dictionary<string, Entity> PortableExperienceEntities { get; }
        bool CanKillPortableExperience(string ens);
        UniTask CreatePortableExperienceAsync(string ens, string urn, CancellationToken ct, bool isGlobalPortableExperience = false);
        UniTask UnloadPortableExperienceAsync(string ens, CancellationToken ct);
    }
}
