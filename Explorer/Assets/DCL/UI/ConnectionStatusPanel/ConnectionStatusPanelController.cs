using Cysharp.Threading.Tasks;
using DCL.Multiplayer.Connections.Rooms.Status;
using DCL.UI.ConnectionStatusPanel.StatusEntry;
using DCL.Utilities;
using ECS.SceneLifeCycle;
using LiveKit.Proto;
using MVC;
using System;
using System.Threading;

namespace DCL.UI.ConnectionStatusPanel
{
    public partial class ConnectionStatusPanelController : ControllerBase<ConnectionStatusPanelView>
    {
        private readonly IMVCManager mvcManager;
        private readonly ECSReloadScene ecsReloadScene;
        private readonly IRoomsStatus roomsStatus;
        private readonly CancellationTokenSource cancellationTokenSource = new ();
        private bool isSceneReloading;

        public override CanvasOrdering.SortingLayer Layer => CanvasOrdering.SortingLayer.Persistent;

        public ConnectionStatusPanelController(ViewFactoryMethod viewFactory, IMVCManager mvcManager, ECSReloadScene ecsReloadScene, IRoomsStatus roomsStatus) : base(viewFactory)
        {
            this.mvcManager = mvcManager;
            this.ecsReloadScene = ecsReloadScene;
            this.roomsStatus = roomsStatus;
        }

        protected override void OnViewInstantiated()
        {
            //TODO health status of scene? Display it
            viewInstance.Scene.ShowReloadButton(TryReloadScene);

            Bind(roomsStatus.ConnectionQualityScene, viewInstance.SceneRoom);
            Bind(roomsStatus.ConnectionQualityIsland, viewInstance.GlobalRoom);
        }

        private void Bind(IReadonlyReactiveProperty<ConnectionQuality> value, IStatusEntry statusEntry)
        {
            value.OnUpdate += newValue => UpdateStatusEntry(statusEntry, newValue);
            UpdateStatusEntry(statusEntry, value.Value);
        }

        private void UpdateStatusEntry(IStatusEntry statusEntry, ConnectionQuality quality)
        {
            var status = StatusFrom(quality);

            if (status == null)
            {
                statusEntry.ShowReloadButton(() =>
                {
                    //mvcManager.ShowAsync()//TODO show popup
                }); //TODO bind reload action
                return;
            }

            statusEntry.ShowStatus(status.Value);
        }

        protected override UniTask WaitForCloseIntentAsync(CancellationToken ct) =>
            UniTask.Never(ct);

        public override void Dispose()
        {
            base.Dispose();

            try { cancellationTokenSource.Dispose(); }
            catch
            {
                //ignore
            }
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

        private static IStatusEntry.Status? StatusFrom(ConnectionQuality quality) =>
            quality switch
            {
                ConnectionQuality.QualityPoor => IStatusEntry.Status.Poor,
                ConnectionQuality.QualityGood => IStatusEntry.Status.Good,
                ConnectionQuality.QualityExcellent => IStatusEntry.Status.Excellent,
                ConnectionQuality.QualityLost => null,
                _ => throw new ArgumentOutOfRangeException(nameof(quality), quality, null)
            };
    }
}
