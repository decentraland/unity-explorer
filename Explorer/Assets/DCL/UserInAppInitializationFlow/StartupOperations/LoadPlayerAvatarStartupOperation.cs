using Arch.Core;
using Cysharp.Threading.Tasks;
using DCL.AuthenticationScreenFlow;
using DCL.AvatarRendering.AvatarShape.UnityInterface;
using DCL.Diagnostics;
using DCL.Profiles;
using DCL.Profiles.Helpers;
using DCL.Profiles.Self;
using DCL.RealmNavigation;
using DCL.Utilities;
using DCL.Utilities.Extensions;
using DCL.Utility.Types;
using ECS.Prioritization.Components;
using System;
using System.Threading;

namespace DCL.UserInAppInitializationFlow
{
    /// <summary>
    ///     Resolves Player profile and waits for the avatar to be loaded
    /// </summary>
    public class LoadPlayerAvatarStartupOperation : IStartupOperation
    {
        private readonly ILoadingStatus loadingStatus;
        private readonly SelfProfile selfProfile;
        private readonly ObjectProxy<AvatarBase> mainPlayerAvatarBaseProxy;

        public LoadPlayerAvatarStartupOperation(ILoadingStatus loadingStatus, SelfProfile selfProfile, ObjectProxy<AvatarBase> mainPlayerAvatarBaseProxy)
        {
            this.loadingStatus = loadingStatus;
            this.selfProfile = selfProfile;
            this.mainPlayerAvatarBaseProxy = mainPlayerAvatarBaseProxy;
        }

        public async UniTask<EnumResult<TaskError>> ExecuteAsync(IStartupOperation.Params args, CancellationToken ct)
        {
            float finalizationProgress = loadingStatus.SetCurrentStage(LoadingStatus.LoadingStage.ProfileLoading);
            ProfileReadResult read = await selfProfile.ProfileAsync(ct);

            return await read.Match(
                (self: this, args, finalizationProgress, ct),
                onOk: static (ctx, profile) => ctx.self.LoadAvatarAsync(new ProfileBuilder().From(profile).Build(), ctx.args, ctx.finalizationProgress, ctx.ct).SuppressToResultAsync(ReportCategory.STARTUP),
                onError: static (_, error) => UniTask.FromResult(ToStartupError(error)));
        }

        private async UniTask LoadAvatarAsync(Profile profile, IStartupOperation.Params args, float finalizationProgress, CancellationToken ct)
        {
            args.Report.SetProgress(finalizationProgress);

            // Add the profile into the player entity so it will create the avatar in world

            World world = args.FlowParameters.World;
            Entity playerEntity = args.FlowParameters.PlayerEntity;

            // The entity owns its instance: the one it replaces is disposed
            if (world.TryGet(playerEntity, out Profile? replaced))
            {
                // Make all systems update again since the previous profile was already stored and processed, but we now updated it
                profile.IsDirty = true;
                world.Set(playerEntity, profile);
                replaced?.Dispose();
            }
            else
                world.Add(playerEntity, profile);

            // Trigger the local player picture download
            ProfileUtils.CreateProfilePicturePromise(profile, world, PartitionComponent.TOP_PRIORITY);

            // Eventually it will lead to the Avatar Resolution or the entity destruction
            // if the avatar is already downloaded by the authentication screen it will be resolved immediately

            finalizationProgress = loadingStatus.SetCurrentStage(LoadingStatus.LoadingStage.PlayerAvatarLoading);
            await UniTask.WaitWhile(() => !mainPlayerAvatarBaseProxy.Configured && world.IsAlive(playerEntity), PlayerLoopTiming.LastPostLateUpdate, ct);
            args.Report.SetProgress(finalizationProgress);
        }

        private static EnumResult<TaskError> ToStartupError(ProfileReadError error) =>
            error switch
            {
                ProfileReadError.NotFound => EnumResult<TaskError>.ErrorResult(TaskError.MessageError, "Own profile is not deployed at the catalyst", new ProfileNotFoundException()),
                ProfileReadError.FetchFailed => EnumResult<TaskError>.ErrorResult(TaskError.Timeout, "Own profile could not be fetched from the catalyst"),
                ProfileReadError.NoIdentity => EnumResult<TaskError>.ErrorResult(TaskError.MessageError, "Own profile cannot be loaded without an identity"),
                ProfileReadError.Cancelled => EnumResult<TaskError>.CancelledResult(TaskError.Cancelled),
                _ => throw new ArgumentOutOfRangeException(nameof(error), error, null),
            };
    }
}
