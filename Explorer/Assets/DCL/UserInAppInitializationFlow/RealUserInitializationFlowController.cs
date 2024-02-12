using Arch.Core;
using Cysharp.Threading.Tasks;
using DCL.AsyncLoadReporting;
using DCL.AuthenticationScreenFlow;
using DCL.AvatarRendering.Wearables;
using DCL.AvatarRendering.Wearables.Helpers;
using DCL.ParcelsService;
using DCL.Profiles;
using DCL.SceneLoadingScreens;
using DCL.Web3.Identities;
using MVC;
using System;
using System.Threading;
using UnityEngine;
using Avatar = DCL.Profiles.Avatar;

namespace DCL.UserInAppInitializationFlow
{
    public class RealUserInitializationFlowController : IUserInAppInitializationFlow
    {
        private readonly ITeleportController teleportController;
        private readonly IMVCManager mvcManager;
        private readonly IWeb3IdentityCache web3IdentityCache;
        private readonly IProfileRepository profileRepository;
        private readonly Vector2Int startParcel;

        private World? world;
        private Entity? playerEntity;

        private AsyncLoadProcessReport? loadReport;

        public RealUserInitializationFlowController(ITeleportController teleportController,
            IMVCManager mvcManager,
            IWeb3IdentityCache web3IdentityCache,
            IProfileRepository profileRepository,
            Vector2Int startParcel)
        {
            this.teleportController = teleportController;
            this.mvcManager = mvcManager;
            this.web3IdentityCache = web3IdentityCache;
            this.profileRepository = profileRepository;
            this.startParcel = startParcel;
        }

        public void InjectToWorld(World world, in Entity playerEntity)
        {
            this.world = world;
            this.playerEntity = playerEntity;
        }

        public async UniTask ExecuteAsync(
            bool showAuthentication,
            bool showLoading,
            CancellationToken ct)
        {
            loadReport = AsyncLoadProcessReport.Create();

            if (showAuthentication)
                await ShowAuthenticationScreenAsync(ct);

            if (showLoading)
                await UniTask.WhenAll(ShowLoadingScreenAsync(ct), LoadCharacterAndWorldAsync(ct));
            else
                await LoadCharacterAndWorldAsync(ct);
        }

        private async UniTask LoadCharacterAndWorldAsync(CancellationToken ct)
        {
            Profile ownProfile = await GetOwnProfileAsync(ct);

            loadReport!.ProgressCounter.Value = 0.2f;

            // Add the profile into the player entity so it will create the avatar in world
            world!.Add(playerEntity!.Value, ownProfile);

            await TeleportToSpawnPointAsync(ct);

            loadReport.ProgressCounter.Value = 1f;
            loadReport.CompletionSource.TrySetResult();
        }

        private async UniTask<Profile> GetOwnProfileAsync(CancellationToken ct)
        {
            if (web3IdentityCache.Identity == null) return CreateRandomProfile();

            return await profileRepository.GetAsync(web3IdentityCache.Identity.Address, 0, ct)
                   ?? CreateRandomProfile();
        }

        private Profile CreateRandomProfile() =>
            new (web3IdentityCache.Identity?.Address ?? "fakeUserId", "Player",
                new Avatar(
                    BodyShape.MALE,
                    WearablesConstants.DefaultWearables.GetDefaultWearablesForBodyShape(BodyShape.MALE),
                    WearablesConstants.DefaultColors.GetRandomEyesColor(),
                    WearablesConstants.DefaultColors.GetRandomHairColor(),
                    WearablesConstants.DefaultColors.GetRandomSkinColor()));

        private async UniTask TeleportToSpawnPointAsync(CancellationToken ct)
        {
            var teleportLoadReport = new AsyncLoadProcessReport(new UniTaskCompletionSource(), new AsyncReactiveProperty<float>(0));

            await UniTask.WhenAny(teleportLoadReport.PropagateProgressCounterAsync(loadReport, ct, loadReport!.ProgressCounter.Value, 0.8f),
                teleportController.TeleportToSceneSpawnPointAsync(
                    startParcel, teleportLoadReport, ct));
        }

        private async UniTask ShowLoadingScreenAsync(CancellationToken ct)
        {
            var timeout = TimeSpan.FromMinutes(2);

            await mvcManager.ShowAsync(SceneLoadingScreenController.IssueCommand(new SceneLoadingScreenController.Params(loadReport!, timeout)))
                            .AttachExternalCancellation(ct);
        }

        private async UniTask ShowAuthenticationScreenAsync(CancellationToken ct)
        {
            await mvcManager.ShowAsync(AuthenticationScreenController.IssueCommand())
                            .AttachExternalCancellation(ct);
        }
    }
}
