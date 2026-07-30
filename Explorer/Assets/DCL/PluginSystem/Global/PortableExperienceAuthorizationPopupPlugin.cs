using Arch.SystemGroups;
using Cysharp.Threading.Tasks;
using DCL.AssetsProvision;
using MVC;
using PortableExperiences.Controller;
using System;
using System.Threading;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace DCL.PluginSystem.Global
{
    /// <summary>
    ///     Registers the popup that asks the user to authorize the capabilities requested
    ///     by a scene-spawned Portable Experience.
    /// </summary>
    public class PortableExperienceAuthorizationPopupPlugin : IDCLGlobalPlugin<PortableExperienceAuthorizationPopupPlugin.Settings>
    {
        private readonly IAssetsProvisioner assetsProvisioner;
        private readonly IMVCManager mvcManager;

        public PortableExperienceAuthorizationPopupPlugin(IAssetsProvisioner assetsProvisioner, IMVCManager mvcManager)
        {
            this.assetsProvisioner = assetsProvisioner;
            this.mvcManager = mvcManager;
        }

        public void Dispose() { }

        public async UniTask InitializeAsync(Settings settings, CancellationToken ct)
        {
            GameObject prefab = await assetsProvisioner.ProvideMainAssetValueAsync(settings.AuthorizationPopup, ct);
            var view = prefab.GetComponent<PortableExperienceAuthorizationPopupView>();
            var viewFactory = PortableExperienceAuthorizationPopupController.CreateLazily(view, null);

            mvcManager.RegisterController(new PortableExperienceAuthorizationPopupController(viewFactory));
        }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in GlobalPluginArguments arguments) { }

        [Serializable]
        public class Settings : IDCLPluginSettings
        {
            public AssetReferenceGameObject AuthorizationPopup;
        }
    }
}
