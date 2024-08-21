using Arch.SystemGroups;
using Cysharp.Threading.Tasks;
using DCL.AssetsProvision;
using DCL.SidebarBus;
using DCL.UI.MainUI;
using MVC;
using System;
using System.Threading;
using UnityEngine;

namespace DCL.PluginSystem.Global
{
    public class MainUIPlugin : DCLGlobalPluginBase<MainUIPlugin.Settings>
    {
        private readonly IMVCManager mvcManager;
        private readonly ISidebarBus sidebarBus;
        private readonly MainUIView mainUIView;

        public MainUIPlugin(
            IMVCManager mvcManager,
            ISidebarBus sidebarBus,
            MainUIView mainUIView)
        {
            this.mvcManager = mvcManager;
            this.sidebarBus = sidebarBus;
            this.mainUIView = mainUIView;
        }

        protected override void InjectSystems(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in GlobalPluginArguments arguments) { }

        protected override async UniTask<ContinueInitialization?> InitializeInternalAsync(Settings settings, CancellationToken ct)
        {
            return (ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in GlobalPluginArguments arguments) =>
            {
                var mainUIController = new MainUIController(
                    () =>
                    {
                        MainUIView view = mainUIView;
                        view.gameObject.SetActive(true);
                        return view;
                    },
                    sidebarBus,
                    mvcManager
                );

                mvcManager.RegisterController(mainUIController);
            };
        }

        public class Settings : IDCLPluginSettings { }
    }
}
