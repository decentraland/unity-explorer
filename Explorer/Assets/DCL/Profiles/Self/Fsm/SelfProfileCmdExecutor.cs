using Arch.Core;
using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.AvatarRendering.Emotes;
using DCL.AvatarRendering.Emotes.Equipped;
using DCL.AvatarRendering.Wearables.Equipped;
using DCL.AvatarRendering.Wearables.Helpers;
using DCL.Diagnostics;
using DCL.Profiles.Helpers;
using ECS.Prioritization.Components;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Threading;
using Utility;
using Utility.Fsm;

namespace DCL.Profiles.Self
{
    /// <summary>
    ///     Performs the commands of the self-profile FSM and reports their outcomes back as messages.
    ///     At most one fetch or deploy is in flight: starting either one cancels the previous one, and so does
    ///     <c>ResetLocalState</c>. A cancelled IO reports nothing; a completed one always reports, tagged with the
    ///     address and profile it was started for, and the update decides whether it still applies.
    /// </summary>
    public class SelfProfileCmdExecutor : ICmdExecutor<SelfProfileCmd, SelfProfileMsg>, IDisposable
    {
        private readonly IProfileRepository profileRepository;
        private readonly IProfileCache profileCache;
        private readonly IWearableStorage wearableStorage;
        private readonly IEmoteStorage emoteStorage;
        private readonly IEquippedWearables equippedWearables;
        private readonly IEquippedEmotes equippedEmotes;
        private readonly ForcedWearables forcedWearables;
        private readonly IReadOnlyList<URN>? forcedEmotes;
        private readonly World world;
        private readonly Entity playerEntity;
        private CancellationTokenSource? activity;

        public SelfProfileCmdExecutor(
            IProfileRepository profileRepository,
            IProfileCache profileCache,
            IWearableStorage wearableStorage,
            IEmoteStorage emoteStorage,
            IEquippedWearables equippedWearables,
            IEquippedEmotes equippedEmotes,
            ForcedWearables forcedWearables,
            IReadOnlyList<URN>? forcedEmotes,
            World world,
            Entity playerEntity)
        {
            this.profileRepository = profileRepository;
            this.profileCache = profileCache;
            this.wearableStorage = wearableStorage;
            this.emoteStorage = emoteStorage;
            this.equippedWearables = equippedWearables;
            this.equippedEmotes = equippedEmotes;
            this.forcedWearables = forcedWearables;
            this.forcedEmotes = forcedEmotes;
            this.world = world;
            this.playerEntity = playerEntity;
        }

        public void Dispose()
        {
            activity.SafeCancelAndDispose();
            activity = null;
        }

        public void Execute(in SelfProfileCmd cmd, IMsgInbox<SelfProfileMsg> inbox) =>
            cmd.Match(
                (executor: this, inbox),
                onNone: static _ => { },
                onIgnore: static (_, reason) => ReportHub.LogWarning(ReportCategory.PROFILE, $"Self profile message ignored: {reason}"),
                onResetLocalState: static ctx => ctx.executor.ResetLocalState(),
                onFetch: static (ctx, address) => ctx.executor.StartFetch(address, ctx.inbox),
                onPublish: static (ctx, profile) => ctx.executor.Publish(profile),
                onDeploy: static (ctx, deploy) => ctx.executor.StartDeploy(deploy, ctx.inbox),
                onBatch: static (ctx, batch) => ctx.executor.ExecuteBatch(batch, ctx.inbox)
            );

        private void ExecuteBatch(SelfProfileCmd[] batch, IMsgInbox<SelfProfileMsg> inbox)
        {
            foreach (SelfProfileCmd cmd in batch)
                Execute(cmd, inbox);
        }

        private void ResetLocalState()
        {
            activity.SafeCancelAndDispose();
            activity = null;

            // Owned NFT ids embed the owner, so the registries must be rebuilt for the next identity.
            wearableStorage.ClearOwnedNftRegistry();
            emoteStorage.ClearOwnedNftRegistry();

            equippedWearables.Clear();
            equippedEmotes.UnEquipAll();
        }

        private void StartFetch(UserId address, IMsgInbox<SelfProfileMsg> inbox) =>
            FetchAsync(address, inbox, StartActivity()).Forget();

        private async UniTaskVoid FetchAsync(UserId address, IMsgInbox<SelfProfileMsg> inbox, CancellationToken ct)
        {
            Profile? profile;

            try
            {
                // Not the suppressing overload: a failure must reach the model as a failure, not as an absent profile.
                profile = await profileRepository.GetAsync(address.Value, 0, null, ct,
                    batchBehaviour: IProfileRepository.FetchBehaviour.EnforceSingleGet);
            }
            catch (OperationCanceledException) { return; }
            catch (Exception e)
            {
                if (!ct.IsCancellationRequested)
                    inbox.Send(SelfProfileMsg.FromFetchFailed(new FetchFailed(address, new ProfileFailure(ClassifyFetchFailure(e), e))));

                return;
            }

            if (profile == null)
            {
                inbox.Send(SelfProfileMsg.FromFetchNotFound(address));
                return;
            }

            ApplySessionOverrides(profile);
            inbox.Send(SelfProfileMsg.FromFetchSucceeded(new FetchSucceeded(address, profile)));
        }

        private static FailureKind ClassifyFetchFailure(Exception e) =>
            e is JsonException ? FailureKind.Malformed : FailureKind.Transient;

        /// <summary>
        ///     Forced wearables and emotes are session-wide overrides used by tooling; an empty emote wheel is filled
        ///     with the base emotes so the wheel is never blank.
        /// </summary>
        private void ApplySessionOverrides(Profile profile)
        {
            forcedWearables.ApplyTo(profile);

            if (forcedEmotes != null)
                for (var slot = 0; slot < forcedEmotes.Count && slot < profile.Avatar.Emotes.Count; slot++)
                    profile.Avatar.emotes[slot] = forcedEmotes[slot];

            if (profile.Avatar.IsEmotesWheelEmpty())
                for (var slot = 0; slot < emoteStorage.BaseEmotesUrns.Count && slot < profile.Avatar.Emotes.Count; slot++)
                    profile.Avatar.emotes[slot] = emoteStorage.BaseEmotesUrns[slot];
        }

        private void Publish(Profile profile)
        {
            profileCache.Set(profile.UserId.Value, profile);
            UpdateAvatarInWorld(profile);
        }

        private void UpdateAvatarInWorld(Profile profile)
        {
            // The entity only carries a profile once the startup flow put one there, and setting a missing component throws.
            if (!world.Has<Profile>(playerEntity))
                return;

            profile.IsDirty = true;
            world.Set(playerEntity, profile);
            ProfileUtils.CreateProfilePicturePromise(profile, world, PartitionComponent.TOP_PRIORITY);
        }

        private void StartDeploy(in DeployCmd deploy, IMsgInbox<SelfProfileMsg> inbox)
        {
            Profile sent = deploy.Profile;
            sent.UserId = deploy.Address;
            sent.Version = deploy.Version;

            // A faking session never deploys what it fakes; the edit is confirmed as sent so the model settles.
            if (forcedWearables.Any || forcedEmotes?.Count > 0)
            {
                activity.SafeCancelAndDispose();
                activity = null;

                ReportHub.LogWarning(ReportCategory.PROFILE, "Profile deploy skipped: forced wearables or emotes are active for this session");
                inbox.Send(SelfProfileMsg.FromDeploySucceeded(new DeploySucceeded(deploy.Address, sent, sent)));
                return;
            }

            DeployAsync(deploy.Address, sent, inbox, StartActivity()).Forget();
        }

        private async UniTaskVoid DeployAsync(UserId address, Profile sent, IMsgInbox<SelfProfileMsg> inbox, CancellationToken ct)
        {
            Profile? saved;

            try
            {
                await profileRepository.SetAsync(sent, ct);

                // The catalyst rewrites some fields on deploy, such as the profile picture url, so the saved profile is re-read.
                saved = await profileRepository.GetAsync(address.Value, sent.Version, null, ct,
                    getFromCacheIfPossible: false,
                    batchBehaviour: IProfileRepository.FetchBehaviour.ForceFetchFromCatalyst | IProfileRepository.FetchBehaviour.DelayUntilResolved);
            }
            catch (OperationCanceledException) { return; }
            catch (Exception e)
            {
                if (!ct.IsCancellationRequested)
                    inbox.Send(SelfProfileMsg.FromDeployFailed(new DeployFailed(address, sent, e)));

                return;
            }

            inbox.Send(saved != null
                ? SelfProfileMsg.FromDeploySucceeded(new DeploySucceeded(address, sent, saved))
                : SelfProfileMsg.FromDeployFailed(new DeployFailed(address, sent, new ProfileNotFoundAfterDeployException(address, sent.Version))));
        }

        private CancellationToken StartActivity()
        {
            activity = activity.SafeRestart();
            return activity.Token;
        }
    }

    public class ProfileNotFoundAfterDeployException : Exception
    {
        public ProfileNotFoundAfterDeployException(UserId address, int version)
            : base($"Profile v{version} of {address.Value} was not found on the catalyst after the deploy") { }
    }
}
