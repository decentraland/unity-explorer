using Arch.SystemGroups;
using Cysharp.Threading.Tasks;
using DCL.Chat.MessageBus;
using DCL.MapRenderer;
using DCL.Minimap;
using DCL.PlacesAPIService;
using DCL.SceneRestrictionBusController.SceneRestrictionBus;
using DCL.UI.MainUI;
using ECS;
using ECS.SceneLifeCycle;
using ECS.SceneLifeCycle.Realm;
using Global.Dynamic;
using MVC;
using System;
using System.Threading;

namespace DCL.PluginSystem.Global
{
    public class MinimapPlugin : IDCLGlobalPlugin<MinimapPlugin.MinimapSettings>
    {
        private readonly IMVCManager mvcManager;
        private readonly Lazy<MinimapController> lazyMap;

        private MinimapPlugin(
            IMVCManager mvcManager,
            MapRendererContainer mapRendererContainer,
            IPlacesAPIService placesAPIService,
            IRealmController realmController,
            IChatMessagesBus chatMessagesBus,
            IScenesCache scenesCache,
            MainUIView mainUIView,
            IMapPathEventBus mapPathEventBus,
            ISceneRestrictionBusController sceneRestrictionBusController,
            string startParcelInGenesis)
        {
            this.mvcManager = mvcManager;

            lazyMap = new Lazy<MinimapController>(() =>
            {
                return new MinimapController(
                    () =>
                    {
                        MinimapView? view = mainUIView.MinimapView;
                        view.gameObject.SetActive(true);
                        return view;
                    },
                    mapRendererContainer.MapRenderer,
                    mvcManager,
                    placesAPIService,
                realmController,
                    chatMessagesBus,
                    scenesCache,
                    mapPathEventBus,
                    sceneRestrictionBusController,
                    startParcelInGenesis);
            });
        }

        public void Dispose()
        {
            if (lazyMap.IsValueCreated)
                lazyMap.Value!.Dispose();
        }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in GlobalPluginArguments arguments)
        {
            TrackPlayerPositionSystem? trackPlayerPositionSystem = TrackPlayerPositionSystem.InjectToWorld(ref builder);
            lazyMap.Value.HookPlayerPositionTrackingSystem(trackPlayerPositionSystem);
        }

        public static MinimapPlugin NewInstance(
            IMVCManager mvcManager,
            MapRendererContainer mapRendererContainer,
            IPlacesAPIService placesAPIService,
            IRealmData realmData,
            IChatMessagesBus chatMessagesBus,
            IScenesCache scenesCache,
            MainUIView mainUIView,
            IMapPathEventBus mapPathEventBus,
            ISceneRestrictionBusController sceneRestrictionBusController,
            string startParcelInGenesis,
            out Lazy<MinimapController> minimap)
        {
            var instance = new MinimapPlugin(mvcManager, mapRendererContainer, placesAPIService, realmData, chatMessagesBus, scenesCache, mainUIView, mapPathEventBus, sceneRestrictionBusController, startParcelInGenesis);
            minimap = instance.lazyMap;
            return instance;
        }

        public UniTask InitializeAsync(MinimapSettings settings, CancellationToken ct)
        {
            var minimapController = lazyMap.Value!;
            mvcManager.RegisterController(minimapController);
            return UniTask.CompletedTask;
        }

        public class MinimapSettings : IDCLPluginSettings { }
    }
}
