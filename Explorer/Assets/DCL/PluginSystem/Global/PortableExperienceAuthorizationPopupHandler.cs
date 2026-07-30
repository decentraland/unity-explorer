using Cysharp.Threading.Tasks;
using DCL.UI.PortableExperiences;
using MVC;
using PortableExperiences.Controller;
using System.Collections.Generic;
using System.Threading;

namespace DCL.PluginSystem.Global
{
    /// <summary>
    ///     Fulfils Portable Experience authorization requests with the MVC popup.
    ///     Bridges the UI assembly into the scene life-cycle without the latter referencing MVC.
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
