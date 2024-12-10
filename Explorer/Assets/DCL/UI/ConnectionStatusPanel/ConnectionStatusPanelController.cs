using Arch.Core;
using Cysharp.Threading.Tasks;
using DCL.DebugUtilities;
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
using System.Collections.Generic;
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
        private readonly IDebugContainerBuilder debugBuilder;
        private readonly CancellationTokenSource cancellationTokenSource = new ();
        private readonly List<IDisposable> subscriptions = new (2);
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
            Entity playerEntity,
            IDebugContainerBuilder debugBuilder
        ) : base(viewFactory)
        {
            this.userInAppInitializationFlow = userInAppInitializationFlow;
            this.mvcManager = mvcManager;
            this.currentSceneInfo = currentSceneInfo;
            this.ecsReloadScene = ecsReloadScene;
            this.roomsStatus = roomsStatus;
            this.world = world;
            this.playerEntity = playerEntity;
            this.debugBuilder = debugBuilder;
        }

        protected override void OnViewInstantiated()
        {
            currentSceneInfo.SceneStatus.OnUpdate += SceneStatusOnUpdate;
            SceneStatusOnUpdate(currentSceneInfo.SceneStatus.Value);
            Bind(roomsStatus.ConnectionQualityScene, viewInstance.SceneRoom);
            Bind(roomsStatus.ConnectionQualityIsland, viewInstance.GlobalRoom);
        }

        protected override void OnViewShow() =>
            SetVisibility(debugBuilder.IsVisible);

        public void SetVisibility(bool isVisible) =>
            viewInstance?.gameObject.SetActive(isVisible);

        private void SceneStatusOnUpdate(ICurrentSceneInfo.Status? obj)
        {
            const float DELAY = 5f;

            async UniTaskVoid ShowButtonAsync(CancellationToken ct)
            {
                await UniTask.Delay(TimeSpan.FromSeconds(DELAY), cancellationToken: ct);

                viewInstance.Scene.ShowReloadButton(TryReloadScene);
            }

            if (obj == null)
            {
                viewInstance.Scene.HideStatus();
                return;
            }

            var status = obj.Value;

            viewInstance.Scene.ShowStatus(status);

            if (status is ICurrentSceneInfo.Status.Crashed)
                ShowButtonAsync(cancellationTokenSource.Token).Forget();
        }

        private void Bind(IReadonlyReactiveProperty<ConnectionQuality> value, IStatusEntry statusEntry)
        {
            var subscription = value.Subscribe(newValue => UpdateStatusEntry(statusEntry, value, newValue));
            UpdateStatusEntry(statusEntry, value, value.Value);
            subscriptions.Add(subscription);
        }

        private void UpdateStatusEntry(IStatusEntry statusEntry, IReadonlyReactiveProperty<ConnectionQuality> value, ConnectionQuality quality)
        {
            var status = StatusFrom(quality);
            statusEntry.ShowStatus(status);

            if (status is IStatusEntry.Status.Lost)
                TryShowErrorAsync(value, cancellationTokenSource.Token).Forget();
        }

        private async UniTaskVoid TryShowErrorAsync(IReadonlyReactiveProperty<ConnectionQuality> value, CancellationToken ct)
        {
            const float DELAY_BEFORE_LOST_ACCEPT = 10;

            await UniTask.Delay(TimeSpan.FromSeconds(DELAY_BEFORE_LOST_ACCEPT), cancellationToken: ct);

            if (value.Value is not ConnectionQuality.QualityLost)
                return;

            await mvcManager.ShowAsync(new ShowCommand<ErrorPopupView, ErrorPopupData>(ErrorPopupData.Default), ct);
            await userInAppInitializationFlow.ExecuteAsync(
                new UserInAppInitializationFlowParameters
                {
                    ShowAuthentication = true,
                    ShowLoading = true,
                    ReloadRealm = true,
                    FromLogout = false,
                    World = world,
                    PlayerEntity = playerEntity,
                }, ct);
        }

        protected override UniTask WaitForCloseIntentAsync(CancellationToken ct) =>
            UniTask.Never(ct);

        public override void Dispose()
        {
            foreach (IDisposable subscription in subscriptions) subscription.Dispose();
            subscriptions.Clear();

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
                ConnectionQuality.QualityLost => IStatusEntry.Status.Lost,
                _ => throw new ArgumentOutOfRangeException(nameof(quality), quality, null!)
            };
    }
}
