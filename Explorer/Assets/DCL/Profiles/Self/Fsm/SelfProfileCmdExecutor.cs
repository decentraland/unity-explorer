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
using System;
using System.Collections.Generic;
using System.Threading;
using Utility;
using Utility.Fsm;

namespace DCL.Profiles.Self
{
    /// <summary>
    ///     Performs the commands of the self-profile FSM and reports their outcomes as messages. A new fetch cancels the
    ///     activity in flight; a new deploy shares its token and never cancels a deploy in flight.
    ///     <c>ResetLocalState</c> and <c>Dispose</c> cancel any activity. A cancelled IO reports nothing; any other
    ///     failure, cancellations raised elsewhere included, is reported to Sentry and to the model.
    /// </summary>
    public class SelfProfileCmdExecutor : ICmdExecutor<SelfProfileCmd, SelfProfileMsg>
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
        private readonly bool skipCatalystDeploy;
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
            Entity playerEntity,
            bool skipCatalystDeploy)
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
            this.skipCatalystDeploy = skipCatalystDeploy;
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
                onIgnore: static (_, reason) => ReportHub.Log(ReportCategory.PROFILE, $"Self profile message ignored: {reason}"),
                onResetLocalState: static ctx => ctx.executor.ResetLocalState(),
                onFetch: static (ctx, address) => ctx.executor.StartFetch(address, ctx.inbox),
                onPublish: static (ctx, profile) => ctx.executor.Publish(profile),
                onDeploy: static (ctx, deploy) => ctx.executor.StartDeploy(deploy, ctx.inbox),
                onBatch: static (ctx, batch) => ctx.executor.ExecuteBatch(batch, ctx.inbox)
            );

        /// <summary>A command that throws is reported and does not stop the commands after it.</summary>
        private void ExecuteBatch(SelfProfileCmd[] batch, IMsgInbox<SelfProfileMsg> inbox)
        {
            foreach (SelfProfileCmd cmd in batch)
            {
                try { Execute(cmd, inbox); }
                catch (Exception e) { ReportHub.LogException(e, ReportCategory.PROFILE); }
            }
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
            try
            {
                // Not the suppressing overload: a failure must reach the model as a failure, not as an absent profile.
                // Bypasses the cache, which can hold a published edit the catalyst never confirmed.
                Profile? profile = await profileRepository.GetAsync(address.Value, 0, null, ct,
                    getFromCacheIfPossible: false,
                    batchBehaviour: IProfileRepository.FetchBehaviour.EnforceSingleGet);

                if (profile == null)
                {
                    inbox.Send(SelfProfileMsg.FromFetchNotFound(address));
                    return;
                }

                Profile fetched = Copy(profile);
                ApplySessionOverrides(fetched);
                inbox.Send(SelfProfileMsg.FromFetchSucceeded(new FetchSucceeded(address, fetched)));
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { }
            catch (Exception e)
            {
                if (ct.IsCancellationRequested)
                    return;

                ReportHub.LogException(e, ReportCategory.PROFILE);
                inbox.Send(SelfProfileMsg.FromFetchFailed(new FetchFailed(address, new ProfileFailure(e))));
            }
        }

        /// <summary>Applies the session-wide tooling overrides; an empty emote wheel is filled with the base emotes.</summary>
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

        /// <summary>The cache and the player entity each get their own copy, so neither disposes the other's instance or the one the model keeps.</summary>
        private void Publish(Profile profile)
        {
            Profile cached = Copy(profile);
            profileCache.Set(cached.UserId.Value, cached);
            UpdateAvatarInWorld(profile);
        }

        /// <summary>The cache and the repository own the instances they are given or return; the model keeps a copy of its own.</summary>
        private static Profile Copy(Profile profile) =>
            new ProfileBuilder().From(profile).Build();

        /// <summary>The player entity owns its instance: the one it replaces hands its picture over and is disposed here.</summary>
        private void UpdateAvatarInWorld(Profile profile)
        {
            // Set throws on an entity without the component.
            if (!world.TryGet(playerEntity, out Profile? replaced))
                return;

            Profile onEntity = Copy(profile);

            if (replaced != null)
                DefaultProfileCache.InheritDynamicState(replaced, onEntity);

            onEntity.IsDirty = true;
            world.Set(playerEntity, onEntity);
            ProfileUtils.CreateProfilePicturePromise(onEntity, world, PartitionComponent.TOP_PRIORITY);
            replaced?.Dispose();
        }

        private void StartDeploy(in DeployCmd deploy, IMsgInbox<SelfProfileMsg> inbox)
        {
            Profile sent = deploy.Profile;
            sent.UserId = deploy.Address;

            // A faking or previewing session never deploys what it fakes; the edit is reported as sent and keeps the deployed version.
            if (forcedWearables.Any || forcedEmotes?.Count > 0 || skipCatalystDeploy)
            {
                activity.SafeCancelAndDispose();
                activity = null;

                ReportHub.LogWarning(ReportCategory.PROFILE, "Profile deploy skipped: forced wearables or emotes, or a self preview, are active for this session");
                inbox.Send(SelfProfileMsg.FromDeploySucceeded(new DeploySucceeded(deploy.Address, sent, sent)));
                return;
            }

            sent.Version = deploy.Version;
            activity ??= new CancellationTokenSource();
            DeployAsync(deploy.Address, sent, inbox, activity.Token).Forget();
        }

        private async UniTaskVoid DeployAsync(UserId address, Profile sent, IMsgInbox<SelfProfileMsg> inbox, CancellationToken ct)
        {
            try
            {
                await profileRepository.SetAsync(Copy(sent), ct);

                // The catalyst rewrites some fields on deploy, such as the profile picture url, so the saved profile is re-read.
                Profile? saved = await profileRepository.GetAsync(address.Value, sent.Version, null, ct,
                    getFromCacheIfPossible: false,
                    batchBehaviour: IProfileRepository.FetchBehaviour.ForceFetchFromCatalyst | IProfileRepository.FetchBehaviour.DelayUntilResolved);

                if (saved == null)
                    throw new ProfileNotFoundAfterDeployException(address, sent.Version);

                inbox.Send(SelfProfileMsg.FromDeploySucceeded(new DeploySucceeded(address, sent, Copy(saved))));
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { }
            catch (Exception e)
            {
                if (ct.IsCancellationRequested)
                    return;

                ReportHub.LogException(e, ReportCategory.PROFILE);
                inbox.Send(SelfProfileMsg.FromDeployFailed(new DeployFailed(address, sent, e)));
            }
        }

        private CancellationToken StartActivity()
        {
            activity = activity.SafeRestart();
            return activity.Token;
        }
    }
}
