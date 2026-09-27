using Cysharp.Threading.Tasks;
using DCL.UI.PortableExperiences;
using MVC;
using PortableExperiences.Controller;
using System.Collections.Generic;
using System.Threading;

namespace DCL.PluginSystem.Global
{
    /// <summary>
    ///     Lets the scene life-cycle reach the MVC popup without referencing MVC itself.
    /// </summary>
    public class PortableExperienceAuthorizationPopupHandler : IPortableExperienceAuthorizationHandler
    {
        private readonly IMVCManager mvcManager;

        public PortableExperienceAuthorizationPopupHandler(IMVCManager mvcManager)
        {
            this.mvcManager = mvcManager;
        }

        public UniTask<bool> RequestAuthorizationAsync(string portableExperienceName, IReadOnlyList<string> permissions, CancellationToken ct) =>
            PortableExperienceAuthorizationPopupController.RequestAuthorizationAsync(mvcManager, portableExperienceName, permissions, ct);
    }
}
