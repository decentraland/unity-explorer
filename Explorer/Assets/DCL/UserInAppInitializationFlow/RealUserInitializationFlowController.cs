using Arch.Core;
using Cysharp.Threading.Tasks;
using DCL.AsyncLoadReporting;
using DCL.Audio;
using DCL.AuthenticationScreenFlow;
using DCL.AvatarRendering.AvatarShape.UnityInterface;
using DCL.Profiles;
using DCL.Profiles.Self;
using DCL.SceneLoadingScreens;
using DCL.Utilities;
using MVC;
using System;
using System.Threading;
using ECS.SceneLifeCycle.Realm;
using UnityEngine;
using UnityEngine.Assertions;
using static DCL.UserInAppInitializationFlow.RealFlowLoadingStatus.Stage;

namespace DCL.UserInAppInitializationFlow
{
    public class RealUserInitializationFlowController : IUserInAppInitializationFlow
    {
        private readonly IMVCManager mvcManager;
        private readonly ISelfProfile selfProfile;
        private readonly Vector2Int startParcel;
        private readonly RealFlowLoadingStatus loadingStatus;

        private readonly ObjectProxy<AvatarBase> mainPlayerAvatarBaseProxy;

        private readonly AudioClipConfig backgroundMusic;

        private AsyncLoadProcessReport? loadReport;

        private readonly IRealmNavigator realmNavigator;


        public RealUserInitializationFlowController(RealFlowLoadingStatus loadingStatus,
            IMVCManager mvcManager,
            ISelfProfile selfProfile,
            Vector2Int startParcel,
            ObjectProxy<AvatarBase> mainPlayerAvatarBaseProxy,
            AudioClipConfig backgroundMusic,
            IRealmNavigator realmNavigator)
        {
            this.mvcManager = mvcManager;
            this.selfProfile = selfProfile;
            this.startParcel = startParcel;
            this.loadingStatus = loadingStatus;
            this.mainPlayerAvatarBaseProxy = mainPlayerAvatarBaseProxy;
            this.backgroundMusic = backgroundMusic;
            this.realmNavigator = realmNavigator;
        }

        public async UniTask ExecuteAsync(bool showAuthentication,
            bool showLoading,
            World world,
            Entity playerEntity,
            CancellationToken ct)
        {
            UIAudioEventsBus.Instance.SendPlayLoopingAudioEvent(backgroundMusic);
            loadReport = AsyncLoadProcessReport.Create();

            if (showAuthentication)
                await ShowAuthenticationScreenAsync(ct);

            UniTask showLoadingScreenAsyncTask = LoadCharacterAndWorldAsync(world, playerEntity, ct);

            if (showLoading)
                await UniTask.WhenAll(ShowLoadingScreenAsync(ct), showLoadingScreenAsyncTask);
            else
                await showLoadingScreenAsyncTask;

            UIAudioEventsBus.Instance.SendStopPlayingLoopingAudioEvent(backgroundMusic);
        }

        private async UniTask LoadCharacterAndWorldAsync(World world, Entity ownPlayerEntity, CancellationToken ct)
        {
            Profile ownProfile = await selfProfile.ProfileOrPublishIfNotAsync(ct);

            loadReport!.ProgressCounter.Value = loadingStatus.SetStage(ProfileLoaded);

            realmNavigator.SwitchMiscVisibilityAsync();
            await LoadPlayerAvatar(world, ownPlayerEntity, ownProfile, ct);
            await LoadLandscapeAsync(ct);
            await TeleportToSpawnPointAsync(world, ct);

            loadReport.ProgressCounter.Value = loadingStatus.SetStage(Completed);
            loadReport.CompletionSource.TrySetResult();
        }

        private async UniTask LoadLandscapeAsync(CancellationToken ct)
        {
            var landscapeLoadReport = new AsyncLoadProcessReport(new UniTaskCompletionSource(), new AsyncReactiveProperty<float>(0));
            await UniTask.WhenAny(
                landscapeLoadReport.PropagateProgressCounterAsync(loadReport, ct, loadReport!.ProgressCounter.Value, RealFlowLoadingStatus.PROGRESS[LandscapeLoaded]),
                realmNavigator.LoadTerrainAsync(landscapeLoadReport, ct));
            loadReport!.ProgressCounter.Value = loadingStatus.SetStage(LandscapeLoaded);
        }

        /// <summary>
        ///     Resolves Player profile and waits for the avatar to be loaded
        /// </summary>
        private UniTask LoadPlayerAvatar(World world, Entity playerEntity, Profile profile, CancellationToken ct)
        {
            // Add the profile into the player entity so it will create the avatar in world

            if (world.Has<Profile>(playerEntity))
                world.Set(playerEntity, profile);
            else
                world.Add(playerEntity, profile);

            // Eventually it will lead to the Avatar Resolution or the entity destruction
            // if the avatar is already downloaded by the authentication screen it will be resolved immediately
            return UniTask.WaitWhile(() => !mainPlayerAvatarBaseProxy.Configured && world.IsAlive(playerEntity), PlayerLoopTiming.LastPostLateUpdate, ct);
        }

        private async UniTask TeleportToSpawnPointAsync(World world, CancellationToken ct)
        {
            var teleportLoadReport = new AsyncLoadProcessReport(new UniTaskCompletionSource(), new AsyncReactiveProperty<float>(0));
            await UniTask.WhenAny(
                teleportLoadReport.PropagateProgressCounterAsync(loadReport, ct, loadReport!.ProgressCounter.Value, RealFlowLoadingStatus.PROGRESS[PlayerTeleported]),
                realmNavigator.InitializeTeleportToSpawnPointAsync(loadReport, ct, startParcel));
            loadingStatus.SetStage(PlayerTeleported);

        }

        private async UniTask ShowLoadingScreenAsync(CancellationToken ct)
        {
            var timeout = TimeSpan.FromMinutes(2);

            await mvcManager.ShowAsync(SceneLoadingScreenController.IssueCommand(new SceneLoadingScreenController.Params(loadReport!, timeout)), ct);
        }

        private async UniTask ShowAuthenticationScreenAsync(CancellationToken ct)
        {
            await mvcManager.ShowAsync(AuthenticationScreenController.IssueCommand(), ct);
        }
    }
}
