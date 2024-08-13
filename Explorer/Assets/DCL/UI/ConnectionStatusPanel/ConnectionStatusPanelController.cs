using Arch.Core;
using Cysharp.Threading.Tasks;
using DCL.Multiplayer.Connections.Rooms.Status;
using DCL.UI.ConnectionStatusPanel.StatusEntry;
using DCL.UI.ErrorPopup;
using DCL.UserInAppInitializationFlow;
using DCL.Utilities;
using ECS.SceneLifeCycle;
using ECS.SceneLifeCycle.CurrentScene;
using LiveKit.Proto;
using MVC;
using System;
using System.Threading;
using Utility;

namespace DCL.UI.ConnectionStatusPanel
{
    public partial class ConnectionStatusPanelController : ControllerBase<ConnectionStatusPanelView>
    {
        private readonly IUserInAppInitializationFlow userInAppInitializationFlow;
        private readonly IMVCManager mvcManager;
        private readonly ICurrentSceneInfo currentSceneInfo;
        private readonly ECSReloadScene ecsReloadScene;
        private readonly IRoomsStatus roomsStatus;
        private readonly World world;
        private readonly Entity playerEntity;
        private readonly CancellationTokenSource cancellationTokenSource = new ();
        private bool isSceneReloading;

        public override CanvasOrdering.SortingLayer Layer => CanvasOrdering.SortingLayer.Persistent;

        public ConnectionStatusPanelController(
            ViewFactoryMethod viewFactory,
            IUserInAppInitializationFlow userInAppInitializationFlow,
            IMVCManager mvcManager,
            ICurrentSceneInfo currentSceneInfo,
            ECSReloadScene ecsReloadScene,
            IRoomsStatus roomsStatus,
            World world,
            Entity playerEntity
        ) : base(viewFactory)
        {
            this.userInAppInitializationFlow = userInAppInitializationFlow;
            this.mvcManager = mvcManager;
            this.currentSceneInfo = currentSceneInfo;
            this.ecsReloadScene = ecsReloadScene;
            this.roomsStatus = roomsStatus;
            this.world = world;
            this.playerEntity = playerEntity;
        }

        protected override void OnViewInstantiated()
        {
            currentSceneInfo.SceneStatus.OnUpdate += SceneStatusOnUpdate;
            SceneStatusOnUpdate(currentSceneInfo.SceneStatus.Value);
            Bind(roomsStatus.ConnectionQualityScene, viewInstance.SceneRoom);
            Bind(roomsStatus.ConnectionQualityIsland, viewInstance.GlobalRoom);
        }

        private void SceneStatusOnUpdate(ICurrentSceneInfo.Status? obj)
        {
            const float DELAY = 5f;

            async UniTaskVoid ShowButtonAsync(CancellationToken ct)
            {
                await UniTask.Delay(TimeSpan.FromSeconds(DELAY), cancellationToken: ct);
                viewInstance.Scene.ShowReloadButton(TryReloadScene);
            }

            if (obj is not { } status)
            {
                viewInstance.Scene.HideStatus();
                return;
            }

            viewInstance.Scene.ShowStatus(status);

            if (status is ICurrentSceneInfo.Status.Crashed)
                ShowButtonAsync(cancellationTokenSource.Token).Forget();
        }

        private void Bind(IReadonlyReactiveProperty<ConnectionQuality> value, IStatusEntry statusEntry)
        {
            value.OnUpdate += newValue => UpdateStatusEntry(statusEntry, newValue);
            UpdateStatusEntry(statusEntry, value.Value);
        }

        private void UpdateStatusEntry(IStatusEntry statusEntry, ConnectionQuality quality)
        {
            if (quality is ConnectionQuality.QualityLost)
            {
                ShowErrorAsync().Forget();
                return;
            }

            var status = StatusFrom(quality);
            statusEntry.ShowStatus(status);
        }

        private async UniTaskVoid ShowErrorAsync()
        {
            await mvcManager.ShowAsync(new ShowCommand<ErrorPopupView, ErrorPopupData>(ErrorPopupData.Empty));
            await userInAppInitializationFlow.ExecuteAsync(true, true, true, world, playerEntity, cancellationTokenSource.Token);
        }

        protected override UniTask WaitForCloseIntentAsync(CancellationToken ct) =>
            UniTask.Never(ct);

        public override void Dispose()
        {
            currentSceneInfo.SceneStatus.OnUpdate -= SceneStatusOnUpdate;
            base.Dispose();

            cancellationTokenSource.SafeCancelAndDispose();
        }

        private void TryReloadScene()
        {
            async UniTask TryReloadSceneAsync()
            {
                if (isSceneReloading)
                    return;

                isSceneReloading = true;

                await ecsReloadScene.TryReloadSceneAsync(cancellationTokenSource.Token);
                isSceneReloading = false;
            }

            TryReloadSceneAsync().Forget();
        }

        private static IStatusEntry.Status StatusFrom(ConnectionQuality quality) =>
            quality switch
            {
                ConnectionQuality.QualityPoor => IStatusEntry.Status.Poor,
                ConnectionQuality.QualityGood => IStatusEntry.Status.Good,
                ConnectionQuality.QualityExcellent => IStatusEntry.Status.Excellent,
                ConnectionQuality.QualityLost => throw new ArgumentOutOfRangeException(nameof(quality), quality, null!),
                _ => throw new ArgumentOutOfRangeException(nameof(quality), quality, null!)
            };
    }
}
