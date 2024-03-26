using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using CommunicationData.URLHelpers;
using DCL.AvatarRendering.AvatarShape.UnityInterface;
using DCL.AvatarRendering.Emotes.Components;
using DCL.AvatarRendering.Wearables.Helpers;
using DCL.Character.CharacterMotion.Components;
using DCL.CharacterMotion.Components;
using DCL.DebugUtilities;
using DCL.Diagnostics;
using DCL.Multiplayer.Emotes.Interfaces;
using DCL.Profiles;
using ECS.Abstract;
using ECS.StreamableLoading.Common.Components;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;
using RfcEmote = Decentraland.Kernel.Comms.Rfc4.Emote;

namespace DCL.AvatarRendering.Emotes
{
    [LogCategory(ReportCategory.EMOTE)]
    [UpdateInGroup(typeof(PostPhysicsSystemGroup))]
    public partial class CharacterEmoteSystem : BaseUnityLoopSystem
    {
        // todo: use this to add nice Debug UI to trigger any emote?
        private readonly IDebugContainerBuilder debugContainerBuilder;

        private readonly IEmoteCache emoteCache;
        private readonly string reportCategory;
        private readonly EmotePlayer emotePlayer;
        private readonly IEmotesMessageBus messageBus;

        public CharacterEmoteSystem(World world, IEmoteCache emoteCache, IEmotesMessageBus messageBus, IDebugContainerBuilder debugContainerBuilder) : base(world)
        {
            this.messageBus = messageBus;
            this.emoteCache = emoteCache;
            this.debugContainerBuilder = debugContainerBuilder;
            reportCategory = GetReportCategory();
            emotePlayer = new EmotePlayer();
        }

        protected override void Update(float t)
        {
            messageBus.InjectWorld(World);

            ConsumeEmoteIntentQuery(World);
            ReplicateLoopingEmotesQuery(World);
            CancelEmotesByMovementQuery(World);
            CancelEmotesByTagQuery(World);
            UpdateEmoteTagsQuery(World);
        }

        // looping emotes and cancelling emotes by tag depend on tag change, this query alone is the one that updates that value at the ond of the update
        [Query]
        private void UpdateEmoteTags(ref CharacterEmoteComponent emoteComponent, in IAvatarView avatarView)
        {
            emoteComponent.CurrentAnimationTag = avatarView.GetAnimatorCurrentStateTag();
        }

        // emotes that do not loop need to trigger some kind of cancellation so we can take care of the emote props and sounds
        [Query]
        private void CancelEmotesByTag(ref CharacterEmoteComponent emoteComponent, in IAvatarView avatarView)
        {
            bool wasPlayingEmote = emoteComponent.CurrentAnimationTag == AnimationHashes.EMOTE || emoteComponent.CurrentAnimationTag == AnimationHashes.EMOTE_LOOP;

            int animatorCurrentStateTag = avatarView.GetAnimatorCurrentStateTag();
            bool isOnAnotherTag = animatorCurrentStateTag != AnimationHashes.EMOTE && animatorCurrentStateTag != AnimationHashes.EMOTE_LOOP;

            EmoteReferences? emoteReference = emoteComponent.CurrentEmoteReference;
            if (emoteReference == null) return;

            if (wasPlayingEmote && isOnAnotherTag)
                StopEmote(ref emoteComponent, emoteReference);
        }

        // when moving or jumping we detect the emote cancellation and we take care of getting rid of the emote props and sounds
        [Query]
        private void CancelEmotesByMovement(ref CharacterEmoteComponent emoteComponent, in CharacterRigidTransform rigidTransform, in IAvatarView avatarView)
        {
            float velocity = rigidTransform.MoveVelocity.Velocity.sqrMagnitude;
            float verticalVelocity = Mathf.Abs(rigidTransform.GravityVelocity.sqrMagnitude);

            bool canEmoteBeCancelled = velocity > 0.2f || verticalVelocity > 0.2f;

            if (!canEmoteBeCancelled) return;

            EmoteReferences? emoteReference = emoteComponent.CurrentEmoteReference;
            if (emoteReference == null) return;

            StopEmote(ref emoteComponent, emoteReference);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void StopEmote(ref CharacterEmoteComponent emoteComponent, EmoteReferences emoteReference)
        {
            emoteComponent.EmoteClip = null;
            emoteComponent.EmoteLoop = false;
            emoteComponent.CurrentEmoteReference = null;
            emotePlayer.Stop(emoteReference);
        }

        // if you want to trigger an emote, this query takes care of consuming the CharacterEmoteIntent to trigger an emote
        [Query]
        private void ConsumeEmoteIntent(in Entity entity, ref CharacterEmoteComponent emoteComponent, in CharacterEmoteIntent emoteIntent, in IAvatarView avatarView)
        {
            URN emoteId = emoteIntent.EmoteId;

            if (emoteCache.TryGetEmote(emoteId.Shorten(), out IEmote emote))
            {
                // emote failed to load? remove intent
                if (emote.ManifestResult is { IsInitialized: true, Exception: not null })
                {
                    ReportHub.LogError(reportCategory, $"Cant play emote {emoteId} since it failed loading \n {emote.ManifestResult}");
                    World.Remove<CharacterEmoteIntent>(entity);
                    return;
                }

                StreamableLoadingResult<WearableAsset>? streamableAsset = emote.WearableAssetResults[0];

                // the emote is still loading? dont remove the intent yet, wait for it
                if (streamableAsset == null) return;
                if (!streamableAsset.Value.Succeeded) return;
                if (streamableAsset.Value.Exception != null) return;

                GameObject? mainAsset = streamableAsset.Value.Asset?.GetMainAsset<GameObject>();

                if (mainAsset == null) return;

                if (!emotePlayer.Play(mainAsset, emote.IsLooping(), in avatarView, ref emoteComponent))
                    ReportHub.LogWarning(reportCategory, $"Emote {emote.Model.Asset.metadata.name} cant be played, AB version: {emote.ManifestResult?.Asset?.GetVersion()} should be >= 16");

                emoteComponent.EmoteUrn = emoteId;
            }

            World.Remove<CharacterEmoteIntent>(entity);
        }

        // Every time that the emote is looped we send a new message that should refresh the looping emotes on clients that didn't receive the initial message yet
        // TODO (Kinerius): This does not support scene emotes yet
        [Query]
        private void ReplicateLoopingEmotes(ref CharacterEmoteComponent animationComponent, in IAvatarView avatarView, in Profile profile)
        {
            int prevTag = animationComponent.CurrentAnimationTag;
            int currentTag = avatarView.GetAnimatorCurrentStateTag();

            if ((prevTag != AnimationHashes.EMOTE || currentTag != AnimationHashes.EMOTE_LOOP)
                && (prevTag != AnimationHashes.EMOTE_LOOP || currentTag != AnimationHashes.EMOTE)) return;

            IReadOnlyList<URN> emotes = profile.Avatar.Emotes;

            for (var i = 0; i < emotes.Count; i++)
            {
                if (emotes[i] != animationComponent.EmoteUrn) continue;

                messageBus.Send((uint)i);
                messageBus.SelfSendWithDelayAsync(new RfcEmote
                           {
                               EmoteId = (uint)i,
                               Timestamp = -1,
                           }, 0.1f)
                          .Forget();

                break;
            }
        }
    }
}
