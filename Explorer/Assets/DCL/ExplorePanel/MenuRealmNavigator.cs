using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.RealmNavigation;
using DCL.SceneLoadingScreens.LoadingScreen;
using DCL.Utilities;
using DCL.Utility.Types;
using ECS.SceneLifeCycle.Realm;
using System;
using System.Threading;
using UnityEngine;
using Utility;

namespace DCL.ExplorePanel
{
    /// <summary>Defers menu travel until the current world load has completed.</summary>
    public sealed class MenuRealmNavigator : IRealmNavigator, IDisposable
    {
        private readonly CancellationTokenSource lifetimeCancellation = new ();
        private readonly CancellationToken lifetime;
        private readonly IRealmNavigator navigator;
        private readonly IReadOnlyLoadingStatus loadingStatus;
        private readonly ILoadingScreen loadingScreen;

        public event Action<Vector2Int> NavigationExecuted
        {
            add => navigator.NavigationExecuted += value;
            remove => navigator.NavigationExecuted -= value;
        }

        public MenuRealmNavigator(IRealmNavigator navigator, IReadOnlyLoadingStatus loadingStatus, ILoadingScreen loadingScreen)
        {
            lifetime = lifetimeCancellation.Token;
            this.navigator = navigator;
            this.loadingStatus = loadingStatus;
            this.loadingScreen = loadingScreen;
        }

        public void Dispose() => lifetimeCancellation.SafeCancelAndDispose();

        public async UniTask<EnumResult<ChangeRealmError>> TryChangeRealmAsync(URLDomain realm, CancellationToken ct,
            Vector2Int parcelToTeleport = default, bool isWorld = false, bool allowsSpawnPointerOverride = false,
            bool landOnParcel = false, string? spawnPointName = null)
        {
            if (ct.IsCancellationRequested || lifetime.IsCancellationRequested) return EnumResult<ChangeRealmError>.CancelledResult(ChangeRealmError.ChangeCancelled);
            using var request = CancellationTokenSource.CreateLinkedTokenSource(ct, lifetime);
            var preparation = await WaitForWorldAsync(request.Token);
            if (preparation.Error is { } error) return EnumResult<ChangeRealmError>.ErrorResult(error.State.AsChangeRealmError(), error.Message, error.Exception);
            return await navigator.TryChangeRealmAsync(realm, request.Token, parcelToTeleport, isWorld, allowsSpawnPointerOverride, landOnParcel, spawnPointName);
        }

        public async UniTask<EnumResult<TaskError>> TeleportToParcelAsync(Vector2Int parcel, CancellationToken ct,
            bool isLocal, bool landOnParcel = false, string? spawnPointName = null)
        {
            if (ct.IsCancellationRequested || lifetime.IsCancellationRequested) return EnumResult<TaskError>.CancelledResult(TaskError.Cancelled);
            using var request = CancellationTokenSource.CreateLinkedTokenSource(ct, lifetime);
            var preparation = await WaitForWorldAsync(request.Token);
            if (!preparation.Success) return preparation;
            return await navigator.TeleportToParcelAsync(parcel, request.Token, isLocal, landOnParcel, spawnPointName);
        }

        public bool IsAlreadyOnRealm(URLDomain realm) => navigator.IsAlreadyOnRealm(realm);

        public void RemoveCameraSamplingData() => navigator.RemoveCameraSamplingData();

        private UniTask<EnumResult<TaskError>> WaitForWorldAsync(CancellationToken ct) =>
            loadingStatus.CurrentStage.Value == LoadingStatus.LoadingStage.Completed
                ? UniTask.FromResult(EnumResult<TaskError>.SuccessResult())
                : loadingScreen.ShowWhileExecuteTaskAsync(WaitForPreparationAsync, ct);

        private async UniTask<EnumResult<TaskError>> WaitForPreparationAsync(AsyncLoadProcessReport report, CancellationToken ct)
        {
            var completion = new UniTaskCompletionSource<EnumResult<TaskError>>();
            void UpdateProgress(LoadingStatus.LoadingStage stage)
            {
                report.SetProgress(LoadingStatus.GetProgress(stage));
                switch (stage)
                {
                    case LoadingStatus.LoadingStage.Completed: completion.TrySetResult(EnumResult<TaskError>.SuccessResult()); break;
                    case LoadingStatus.LoadingStage.Failed: completion.TrySetResult(EnumResult<TaskError>.ErrorResult(TaskError.MessageError, "World preparation failed.")); break;
                    case LoadingStatus.LoadingStage.Cancelled: completion.TrySetResult(EnumResult<TaskError>.CancelledResult(TaskError.Cancelled)); break;
                }
            }
            using var subscription = loadingStatus.CurrentStage.Subscribe(UpdateProgress);
            UpdateProgress(loadingStatus.CurrentStage.Value);
            using var cancellation = ct.RegisterWithoutCaptureExecutionContext(() => completion.TrySetResult(EnumResult<TaskError>.CancelledResult(TaskError.Cancelled)));
            return await completion.Task;
        }
    }
}
