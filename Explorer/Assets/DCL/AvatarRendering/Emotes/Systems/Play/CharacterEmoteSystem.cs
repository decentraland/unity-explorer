using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using CommunicationData.URLHelpers;
using DCL.AvatarRendering.AvatarShape;
using DCL.AvatarRendering.AvatarShape.Components;
using DCL.AvatarRendering.AvatarShape.UnityInterface;
using DCL.AvatarRendering.Loading.Assets;
using DCL.AvatarRendering.Loading.Components;
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
using ECS.Prioritization.Components;
using ECS.StreamableLoading.AudioClips;
using ECS.StreamableLoading.Common.Components;
using Global.AppArgs;
using System;
using System.Linq;
using System.Runtime.CompilerServices;
using UnityEngine;
using Utility.Animations;
using EmotePromise = ECS.StreamableLoading.Common.AssetPromise<DCL.AvatarRendering.Emotes.EmotesResolution,
    DCL.AvatarRendering.Emotes.GetEmotesByPointersIntention>;

namespace DCL.AvatarRendering.Emotes.Play
{
    [LogCategory(ReportCategory.EMOTE)]
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [UpdateAfter(typeof(AvatarGroup))]
    [UpdateAfter(typeof(RemoteEmotesSystem))]
    [UpdateBefore(typeof(ChangeCharacterPositionGroup))]
    [UpdateBefore(typeof(CleanUpGroup))]
    public partial class CharacterEmoteSystem : BaseUnityLoopSystem
    {
        // todo: use this to add nice Debug UI to trigger any emote?
        private readonly IDebugContainerBuilder debugContainerBuilder;

        private readonly IEmoteStorage emoteStorage;
        private readonly EmotePlayer emotePlayer;
        private readonly IEmotesMessageBus messageBus;
        private readonly URN[] loadEmoteBuffer = new URN[1];

        public CharacterEmoteSystem(
            World world,
            IEmoteStorage emoteStorage,
            IEmotesMessageBus messageBus,
            AudioSource audioSource,
            IDebugContainerBuilder debugContainerBuilder,
            bool localSceneDevelopment,
            IAppArgs appArgs) : base(world)
        {
            this.messageBus = messageBus;
            this.emoteStorage = emoteStorage;
            this.debugContainerBuilder = debugContainerBuilder;
            emotePlayer = new EmotePlayer(audioSource, legacyAnimationsEnabled: localSceneDevelopment || appArgs.HasFlag(AppArgsFlags.SELF_PREVIEW_BUILDER_COLLECTIONS));
        }

        protected override void Update(float t)
        {
            CancelEmotesByTeleportIntentionQuery(World);
            ConsumeEmoteIntentQuery(World);
            ReplicateLoopingEmotesQuery(World);
            CancelEmotesByDeletionQuery(World);
            CancelEmotesByMovementQuery(World);
            CancelEmotesQuery(World);
            UpdateEmoteTagsQuery(World);
            DisableCharacterControllerQuery(World);
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
        [None(typeof(CharacterEmoteIntent))]
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

        // emotes that do not loop need to trigger some kind of cancellation, so we can take care of the emote props and sounds
        [Query]
        [None(typeof(CharacterEmoteIntent), typeof(DeleteEntityIntention))]
        private void CancelEmotes(ref CharacterEmoteComponent emoteComponent, in IAvatarView avatarView, in Entity entity)
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

            if (wantsToCancelEmote || World.Has<BlockedPlayerComponent>(entity))
            {
                StopEmote(ref emoteComponent, avatarView);
                return;
            }

            int animatorCurrentStateTag = avatarView.GetAnimatorCurrentStateTag();
            bool isOnAnotherTag = animatorCurrentStateTag != AnimationHashes.EMOTE && animatorCurrentStateTag != AnimationHashes.EMOTE_LOOP;

            if (isOnAnotherTag)
                StopEmote(ref emoteComponent, avatarView);
        }

        // when moving or jumping we detect the emote cancellation, and we take care of getting rid of the emote props and sounds
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
            if (emoteComponent.CurrentEmoteReference == null) return;

            emotePlayer.Stop(emoteComponent.CurrentEmoteReference);

            // Create a clean slate for the animator before setting the stop trigger
            avatarView.ResetTrigger(AnimationHashes.EMOTE);
            avatarView.ResetTrigger(AnimationHashes.EMOTE_RESET);
            avatarView.SetAnimatorTrigger(AnimationHashes.EMOTE_STOP);

            emoteComponent.Reset();
        }

        // This query takes care of consuming the CharacterEmoteIntent to trigger an emote
        [Query]
        [None(typeof(DeleteEntityIntention))]
        private void ConsumeEmoteIntent(Entity entity, ref CharacterEmoteComponent emoteComponent, in CharacterEmoteIntent emoteIntent,
            in IAvatarView avatarView, ref AvatarShapeComponent avatarShapeComponent)
        {
            URN emoteId = emoteIntent.EmoteId;

            // it's very important to catch any exception here to avoid not consuming the emote intent, so we don't infinitely create props
            try
            {
                // we wait until the avatar finishes moving to trigger the emote,
                // avoid the case where: you stop moving, trigger the emote, the emote gets triggered and next frame it gets cancelled because inertia keeps moving the avatar
                //We also avoid triggering the emote while the character is jumping or landing, as the landing animation breaks the emote flow if they have props
                if (avatarView.IsAnimatorInTag(AnimationHashes.JUMPING_TAG) ||
                    avatarView.GetAnimatorFloat(AnimationHashes.MOVEMENT_BLEND) > 0.1f)
                    return;

                if (emoteStorage.TryGetElement(emoteId.Shorten(), out IEmote emote))
                {
                    // emote failed to load? remove intent
                    if (emote.ManifestResult is { IsInitialized: true, Succeeded: false })
                    {
                        ReportHub.LogError(GetReportData(), $"Cant play emote {emoteId} since it failed loading \n {emote.ManifestResult}");
                        World.Remove<CharacterEmoteIntent>(entity);
                        return;
                    }

                    BodyShape bodyShape = avatarShapeComponent.BodyShape;
                    StreamableLoadingResult<AttachmentRegularAsset>? streamableAsset = emote.AssetResults[bodyShape];

                    // the emote is still loading? don't remove the intent yet, wait for it
                    if (streamableAsset == null)
                        return;

                    StreamableLoadingResult<AttachmentRegularAsset> streamableAssetValue = streamableAsset.Value;
                    GameObject? mainAsset;

                    if (streamableAssetValue is { Succeeded: false } || (mainAsset = streamableAssetValue.Asset?.MainAsset) == null)
                    {
                        // We can't play emote, remove intent, otherwise there is no place to remove it
                        World.Remove<CharacterEmoteIntent>(entity);
                        return;
                    }

                    emoteComponent.EmoteUrn = emoteId;
                    StreamableLoadingResult<AudioClipData>? audioAssetResult = emote.AudioAssetResults[bodyShape];
                    AudioClip? audioClip = audioAssetResult?.Asset;

                    if (!emotePlayer.Play(mainAsset, audioClip, emote.IsLooping(), emoteIntent.Spatial, in avatarView, ref emoteComponent))
                        ReportHub.LogWarning(GetReportData(), $"Emote {emote.Model.Asset?.metadata.name} cant be played, AB version: {emote.ManifestResult?.Asset?.GetVersion()} should be >= 16");

                    World.Remove<CharacterEmoteIntent>(entity);
                }
                else
                    // Request the emote when not it cache. It will eventually endup in the emoteStorage so it can be played by this query
                    LoadEmote(emoteId, avatarShapeComponent.BodyShape);
            }
            catch (Exception e) { ReportHub.LogException(e, GetReportData()); }
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

            messageBus.Send(animationComponent.EmoteUrn, true);
        }

        [Query]
        private void CleanUp(Profile profile, in DeleteEntityIntention deleteEntityIntention)
        {
            if (!deleteEntityIntention.DeferDeletion)
                messageBus.OnPlayerRemoved(profile.UserId);
        }

        [Query]
        private void DisableCharacterController(ref CharacterController characterController, in CharacterEmoteComponent emoteComponent)
        {
            characterController.enabled = !emoteComponent.IsPlayingEmote;
        }

        private void LoadEmote(URN emoteId, BodyShape bodyShape)
        {
            var isLoadingThisEmote = false;

            World.Query(in new QueryDescription().WithAll<EmotePromise>(), (Entity entity, ref EmotePromise promise) =>
            {
                if (!promise.IsConsumed && promise.LoadingIntention.Pointers.Contains(emoteId))
                    isLoadingThisEmote = true;
            });

            if (isLoadingThisEmote) return;

            World.Create(CreateEmotePromise(emoteId, bodyShape));
        }

        private EmotePromise CreateEmotePromise(URN urn, BodyShape bodyShape)
        {
            loadEmoteBuffer[0] = urn;

            return EmotePromise.Create(World, EmoteComponentsUtils.CreateGetEmotesByPointersIntention(bodyShape, loadEmoteBuffer),
                PartitionComponent.TOP_PRIORITY);
        }
    }
}
