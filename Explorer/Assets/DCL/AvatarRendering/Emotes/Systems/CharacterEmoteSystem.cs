using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using CommunicationData.URLHelpers;
using DCL.AvatarRendering.AvatarShape.Components;
using DCL.AvatarRendering.AvatarShape;
using DCL.AvatarRendering.AvatarShape.UnityInterface;
using DCL.AvatarRendering.Wearables;
using DCL.AvatarRendering.Wearables.Helpers;
using DCL.Character.CharacterMotion.Components;
using DCL.Character.Components;
using DCL.CharacterMotion.Components;
using DCL.CharacterMotion.Systems;
using DCL.DebugUtilities;
using DCL.Diagnostics;
using DCL.Multiplayer.Emotes;
using DCL.Profiles;
using ECS.Abstract;
using ECS.Groups;
using ECS.LifeCycle.Components;
using ECS.StreamableLoading.Common.Components;
using System;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace DCL.AvatarRendering.Emotes
{
    [LogCategory(ReportCategory.EMOTE)]
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [UpdateAfter(typeof(AvatarGroup))]
    [UpdateAfter(typeof(RemoteEmotesSystem))]
    [UpdateBefore(typeof(InterpolateCharacterSystem))]
    [UpdateBefore(typeof(CleanUpGroup))]
    public partial class CharacterEmoteSystem : BaseUnityLoopSystem
    {
        // todo: use this to add nice Debug UI to trigger any emote?
        private readonly IDebugContainerBuilder debugContainerBuilder;

        private readonly IEmoteCache emoteCache;
        private readonly EmotePlayer emotePlayer;
        private readonly IEmotesMessageBus messageBus;

        public CharacterEmoteSystem(World world, IEmoteCache emoteCache, IEmotesMessageBus messageBus, AudioSource audioSource, IDebugContainerBuilder debugContainerBuilder) : base(world)
        {
            this.messageBus = messageBus;
            this.emoteCache = emoteCache;
            this.debugContainerBuilder = debugContainerBuilder;
            emotePlayer = new EmotePlayer(audioSource);
        }

        protected override void Update(float t)
        {
            ConsumeEmoteIntentQuery(World);
            ReplicateLoopingEmotesQuery(World);
            CancelEmotesByDeletionQuery(World);
            CancelEmotesByTeleportIntentionQuery(World);
            CancelEmotesByMovementQuery(World);
            CancelEmotesQuery(World);
            UpdateEmoteTagsQuery(World);
            CleanUpQuery(World);
        }

        [Query]
        [All(typeof(DeleteEntityIntention))]
        private void CancelEmotesByDeletion(ref CharacterEmoteComponent emoteComponent, in IAvatarView avatarView)
        {
            StopEmote(ref emoteComponent, avatarView);
        }

        [Query]
        [All(typeof(PlayerTeleportIntent))]
        private void CancelEmotesByTeleportIntention(ref CharacterEmoteComponent emoteComponent, in IAvatarView avatarView)
        {
            StopEmote(ref emoteComponent, avatarView);
        }

        // looping emotes and cancelling emotes by tag depend on tag change, this query alone is the one that updates that value at the ond of the update
        [Query]
        private void UpdateEmoteTags(ref CharacterEmoteComponent emoteComponent, in IAvatarView avatarView)
        {
            emoteComponent.CurrentAnimationTag = avatarView.GetAnimatorCurrentStateTag();
        }

        // emotes that do not loop need to trigger some kind of cancellation so we can take care of the emote props and sounds
        [Query]
        [None(typeof(CharacterEmoteIntent))]
        private void CancelEmotes(ref CharacterEmoteComponent emoteComponent, in IAvatarView avatarView)
        {
            bool wantsToCancelEmote = emoteComponent.StopEmote;
            emoteComponent.StopEmote = false;

            bool wasPlayingEmote = emoteComponent.CurrentAnimationTag == AnimationHashes.EMOTE || emoteComponent.CurrentAnimationTag == AnimationHashes.EMOTE_LOOP;

            if (!wasPlayingEmote)
            {
                avatarView.ResetTrigger(AnimationHashes.EMOTE_STOP);
                return;
            }

            EmoteReferences? emoteReference = emoteComponent.CurrentEmoteReference;

            if (emoteReference == null)
                return;

            if (wantsToCancelEmote)
            {
                StopEmote(ref emoteComponent, avatarView);
                return;
            }

            int animatorCurrentStateTag = avatarView.GetAnimatorCurrentStateTag();
            bool isOnAnotherTag = animatorCurrentStateTag != AnimationHashes.EMOTE && animatorCurrentStateTag != AnimationHashes.EMOTE_LOOP;

            if (isOnAnotherTag)
                StopEmote(ref emoteComponent, avatarView);
        }

        // when moving or jumping we detect the emote cancellation and we take care of getting rid of the emote props and sounds
        [Query]
        [None(typeof(CharacterEmoteIntent))]
        private void CancelEmotesByMovement(ref CharacterEmoteComponent emoteComponent, in CharacterRigidTransform rigidTransform, in IAvatarView avatarView)
        {
            const float CUTOFF_LIMIT = 0.2f;

            float velocity = rigidTransform.MoveVelocity.Velocity.sqrMagnitude;
            float verticalVelocity = Mathf.Abs(rigidTransform.GravityVelocity.sqrMagnitude);

            bool canEmoteBeCancelled = velocity > CUTOFF_LIMIT || verticalVelocity > CUTOFF_LIMIT;

            if (!canEmoteBeCancelled) return;

            StopEmote(ref emoteComponent, avatarView);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void StopEmote(ref CharacterEmoteComponent emoteComponent, IAvatarView avatarView)
        {
            if (emoteComponent.CurrentEmoteReference != null)
            {
                emotePlayer.Stop(emoteComponent.CurrentEmoteReference);
                avatarView.SetAnimatorTrigger(AnimationHashes.EMOTE_STOP);
            }

            emoteComponent.Reset();
        }

        // if you want to trigger an emote, this query takes care of consuming the CharacterEmoteIntent to trigger an emote
        [Query]
        [None(typeof(DeleteEntityIntention))]
        private void ConsumeEmoteIntent(Entity entity, ref CharacterEmoteComponent emoteComponent, in CharacterEmoteIntent emoteIntent, in IAvatarView avatarView, in AvatarShapeComponent avatarShapeComponent)
        {
            URN emoteId = emoteIntent.EmoteId;

            // its very important to catch any exception here to avoid not consuming the emote intent, so we dont infinitely create props
            try
            {
                // we wait until the avatar finishes moving to trigger the emote,
                // avoid the case where: you stop moving, trigger the emote, the emote gets triggered and next frame it gets cancelled because inertia keeps moving the avatar
                if (avatarView.GetAnimatorFloat(AnimationHashes.MOVEMENT_BLEND) > 0.1f)
                    return;

                if (emoteCache.TryGetEmote(emoteId.Shorten(), out IEmote emote))
                {
                    // emote failed to load? remove intent
                    if (emote.ManifestResult is { IsInitialized: true, Succeeded: false })
                    {
                        ReportHub.LogError(GetReportCategory(), $"Cant play emote {emoteId} since it failed loading \n {emote.ManifestResult}");
                        World.Remove<CharacterEmoteIntent>(entity);
                        return;
                    }

                    BodyShape bodyShape = avatarShapeComponent.BodyShape;
                    StreamableLoadingResult<WearableRegularAsset>? streamableAsset = emote.AssetResults[bodyShape];

                    // the emote is still loading? dont remove the intent yet, wait for it
                    if (streamableAsset == null)
                        return;

                    StreamableLoadingResult<WearableRegularAsset> streamableAssetValue = streamableAsset.Value;
                    GameObject? mainAsset;

                    if (streamableAssetValue is { Succeeded: false } || (mainAsset = streamableAssetValue.Asset?.MainAsset) == null)
                    {
                        // We can't play emote, remove intent, otherwise there is no place to remove it
                        World.Remove<CharacterEmoteIntent>(entity);
                        return;
                    }

                    StreamableLoadingResult<AudioClip>? audioAssetResult = emote.AudioAssetResults[bodyShape];
                    AudioClip? audioClip = audioAssetResult?.Asset;

                    if (!emotePlayer.Play(mainAsset, audioClip, emote.IsLooping(), emoteIntent.Spatial, in avatarView, ref emoteComponent))
                        ReportHub.LogWarning(GetReportCategory(), $"Emote {emote.Model.Asset.metadata.name} cant be played, AB version: {emote.ManifestResult?.Asset?.GetVersion()} should be >= 16");

                    emoteComponent.EmoteUrn = emoteId;
                    World.Remove<CharacterEmoteIntent>(entity);
                }
            }
            catch (Exception e) { ReportHub.LogException(e, GetReportCategory()); }
        }

        // Every time the emote is looped we send a new message that should refresh the looping emotes on clients that didn't receive the initial message yet
        // TODO (Kinerius): This does not support scene emotes yet
        [Query]
        [All(typeof(PlayerComponent))]
        [None(typeof(CharacterEmoteIntent))]
        private void ReplicateLoopingEmotes(ref CharacterEmoteComponent animationComponent, in IAvatarView avatarView)
        {
            int prevTag = animationComponent.CurrentAnimationTag;
            int currentTag = avatarView.GetAnimatorCurrentStateTag();

            if ((prevTag != AnimationHashes.EMOTE || currentTag != AnimationHashes.EMOTE_LOOP)
                && (prevTag != AnimationHashes.EMOTE_LOOP || currentTag != AnimationHashes.EMOTE)) return;

            messageBus.Send(animationComponent.EmoteUrn, true, true);
        }

        [Query]
        private void CleanUp(Profile profile, in DeleteEntityIntention deleteEntityIntention)
        {
            if (!deleteEntityIntention.DeferDeletion)
                messageBus.OnPlayerRemoved(profile.UserId);
        }
    }
}
