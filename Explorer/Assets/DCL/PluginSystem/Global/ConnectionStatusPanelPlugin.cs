using Arch.SystemGroups;
using Cysharp.Threading.Tasks;
using DCL.UI.ConnectionStatusPanel;
using DCL.UI.MainUI;
using MVC;
using System.Threading;

namespace DCL.PluginSystem.Global
{
    public class ConnectionStatusPanelPlugin : DCLGlobalPluginBase<ConnectionStatusPanelPlugin.ConnectionStatusPanelSettings>
    {
        private readonly IMVCManager mvcManager;
        private readonly MainUIView mainUIView;

        public ConnectionStatusPanelPlugin(IMVCManager mvcManager, MainUIView mainUIView)
        {
            this.mvcManager = mvcManager;
            this.mainUIView = mainUIView;
        }

        protected override UniTask<ContinueInitialization?> InitializeInternalAsync(ConnectionStatusPanelSettings settings, CancellationToken ct) =>
            UniTask.FromResult<ContinueInitialization?>(
                (ref ArchSystemsWorldBuilder<Arch.Core.World> _, in GlobalPluginArguments _) =>
                {
                    mvcManager.RegisterController(
                        new ConnectionStatusPanelController(() =>
                            {
                                var view = mainUIView.ConnectionStatusPanelView;
                                view!.gameObject.SetActive(true);
                                return view;
                            }
                        )
                    );
                }
            );

        protected override void InjectSystems(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in GlobalPluginArguments arguments) { }

        public class ConnectionStatusPanelSettings : IDCLPluginSettings { }
    }
}
