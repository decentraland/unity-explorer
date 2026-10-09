using Arch.Core;
using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.AvatarRendering.Emotes;
using DCL.AvatarRendering.Emotes.Equipped;
using DCL.AvatarRendering.Wearables.Equipped;
using DCL.AvatarRendering.Wearables.Helpers;
using DCL.Diagnostics;
using DCL.Profiles.Helpers;
using DCL.Web3.Identities;
using System;
using System.Collections.Generic;
using System.Threading;
using Utility;
using Utility.Fsm;

namespace DCL.Profiles.Self
{
    /// <summary>
    ///     Performs the self-profile FSM commands and reports outcomes as messages. A fetch cancels the activity in flight; a deploy
    ///     never cancels a deploy in flight; <c>ResetLocalState</c> and <c>Dispose</c> cancel any. A cancelled IO reports nothing;
    ///     any other failure is logged and sent as a failure message.
    /// </summary>
    public class SelfProfileCmdExecutor : ICmdExecutor<SelfProfileCmd, SelfProfileMsg>
    {
        private readonly IProfileRepository profileRepository;
        private readonly IProfileCache profileCache;
        private readonly IWeb3IdentityCache identityCache;
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
            IWeb3IdentityCache identityCache,
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
            this.identityCache = identityCache;
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
                // Not the suppressing overload, so a failure is sent as FetchFailed rather than FetchNotFound.
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

                // A non-guest identity has connected web3, even when the deployed profile was made by a guest.
                if (identityCache.Identity is { } identity && !identity.IsGuest())
                    fetched.HasConnectedWeb3 = true;

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

        /// <summary>The cache and the player entity each get their own copy, so neither shares an instance with the other or with the given profile.</summary>
        private void Publish(Profile profile)
        {
            Profile cached = Copy(profile);
            profileCache.Set(cached.UserId.Value, cached);
            ProfileUtils.ReplaceOnEntity(world, playerEntity, profile);
        }

        /// <summary>An independent instance, since the cache and the repository own the instances they are given or return.</summary>
        private static Profile Copy(Profile profile) =>
            new ProfileBuilder().From(profile).Build();

        private void StartDeploy(in DeployCmd deploy, IMsgInbox<SelfProfileMsg> inbox)
        {
            Profile sent = deploy.Profile;
            sent.UserId = deploy.Address;

            // A local-only edit, or any edit of a faking session, is never deployed: it is reported as saved, without a new version.
            if (deploy.LocalOnly || forcedWearables.Any || forcedEmotes?.Count > 0)
            {
                activity.SafeCancelAndDispose();
                activity = null;

                if (deploy.LocalOnly)
                    ReportHub.Log(ReportCategory.PROFILE, "Profile deploy skipped: the edit is local only");
                else
                    ReportHub.LogWarning(ReportCategory.PROFILE, "Profile deploy skipped: forced wearables or emotes are active for this session");

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
