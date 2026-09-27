using Cysharp.Threading.Tasks;
using System.Collections.Generic;
using System.Threading;

namespace PortableExperiences.Controller
{
    /// <summary>
    ///     Decides whether a scene-spawned Portable Experience may run with the permissions it requests.
    ///     Implemented outside this assembly so the scene life-cycle does not depend on UI.
    /// </summary>
    public interface IPortableExperienceAuthorizationHandler
    {
        UniTask<bool> RequestAuthorizationAsync(string portableExperienceName, IReadOnlyList<string> permissions, CancellationToken ct);
    }
}
