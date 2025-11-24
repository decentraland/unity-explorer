using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using CommunicationData.URLHelpers;
using DCL.AvatarRendering.AvatarShape;
using DCL.AvatarRendering.AvatarShape.Components;
using DCL.AvatarRendering.AvatarShape.UnityInterface;
using DCL.AvatarRendering.Emotes.Load;
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
using ECS.SceneLifeCycle;
using ECS.StreamableLoading.AudioClips;
using ECS.StreamableLoading.Common.Components;
using Global.AppArgs;
using SceneRunner.Scene;
using System;
using System.Runtime.CompilerServices;
using UnityEngine;
using Utility.Animations;
using EmotePromise = ECS.StreamableLoading.Common.AssetPromise<DCL.AvatarRendering.Emotes.EmotesResolution, DCL.AvatarRendering.Emotes.GetEmotesByPointersIntention>;
using SceneEmoteFromRealmPromise = ECS.StreamableLoading.Common.AssetPromise<DCL.AvatarRendering.Emotes.EmotesResolution, DCL.AvatarRendering.Emotes.GetSceneEmoteFromRealmIntention>;

namespace DCL.AvatarRendering.Emotes.Play
{
    [LogCategory(ReportCategory.EMOTE)]
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [UpdateAfter(typeof(AvatarGroup))]
    [UpdateAfter(typeof(RemoteEmotesSystem))]
    [UpdateAfter(typeof(LoadEmotesByPointersSystem))]
    [UpdateBefore(typeof(ChangeCharacterPositionGroup))]
    public partial class CharacterEmoteSystem : BaseUnityLoopSystem
    {
        // todo: use this to add nice Debug UI to trigger any emote?
        private readonly IDebugContainerBuilder debugContainerBuilder;
        private readonly IScenesCache scenesCache;

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
            IAppArgs appArgs,
            IScenesCache scenesCache) : base(world)
        {
            this.messageBus = messageBus;
            this.emoteStorage = emoteStorage;
            this.debugContainerBuilder = debugContainerBuilder;
            this.scenesCache = scenesCache;
            emotePlayer = new EmotePlayer(audioSource, legacyAnimationsEnabled: localSceneDevelopment || appArgs.HasFlag(AppArgsFlags.SELF_PREVIEW_BUILDER_COLLECTIONS));
        }

        protected override void Update(float t)
        {
            CancelEmotesQuery(World);
            CancelEmotesByTeleportIntentionQuery(World);
            CancelEmotesOnMovePlayerToInvokedQuery(World);
            CancelEmotesByMovementQuery(World);
            ReplicateLoopingEmotesQuery(World);
            ConsumeEmoteIntentQuery(World);
            CancelEmotesByDeletionQuery(World);
            UpdateEmoteTagsQuery(World);
            DisableCharacterControllerQuery(World);
            DisableAnimatorWhenPlayingLegacyAnimationsQuery(World);
            CleanUpQuery(World);
        }

        [Query]
        [All(typeof(DeleteEntityIntention))]
        private void CancelEmotesByDeletion(ref CharacterEmoteComponent emoteComponent, in IAvatarView avatarView)
        {
            StopEmote(ref emoteComponent, avatarView);
        }

        /// <summary>
        /// Stops emote playback whenever the teleport intent is present on the entity.
        /// Doesn't handle movePlayerTo calls.
        /// </summary>
        [Query]
        [All(typeof(PlayerTeleportIntent))]
        [None(typeof(CharacterEmoteIntent), typeof(MovePlayerToInfo))]
        private void CancelEmotesByTeleportIntention(ref CharacterEmoteComponent emoteComponent, in IAvatarView avatarView)
        {
            StopEmote(ref emoteComponent, avatarView);
        }

        /// <summary>
        /// Stops emote playback when movePlayerTo is invoked.<br/>
        /// Will not cancel a scene emote that was triggered the same frame movePlayerTo was invoked.
        /// </summary>
        [Query]
        [All(typeof(PlayerTeleportIntent))]
        [None(typeof(CharacterEmoteIntent))]
        private void CancelEmotesOnMovePlayerToInvoked(Entity entity, in MovePlayerToInfo movePlayerTo, ref CharacterEmoteComponent emoteComponent, in IAvatarView avatarView)
        {
            if (World.TryGet(entity, out CharacterWaitingSceneEmoteLoading waitingEmote) &&
                movePlayerTo.FrameCount == waitingEmote.FrameCount)
                return;

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

            EmoteReferences? emoteReference = emoteComponent.CurrentEmoteReference;
            if (!emoteReference) return;

            bool shouldCancelEmote = wantsToCancelEmote || World.Has<HiddenPlayerComponent>(entity);
            if (shouldCancelEmote)
            {
                StopEmote(ref emoteComponent, avatarView);
                return;
            }

            if (!emoteReference.legacy)
            {
                if (!emoteComponent.IsPlayingEmote)
                {
                    avatarView.ResetAnimatorTrigger(AnimationHashes.EMOTE_STOP);
                    return;
                }

                int animatorCurrentStateTag = avatarView.GetAnimatorCurrentStateTag();
                bool isOnAnotherTag = animatorCurrentStateTag != AnimationHashes.EMOTE && animatorCurrentStateTag != AnimationHashes.EMOTE_LOOP;

                if (isOnAnotherTag)
                    StopEmote(ref emoteComponent, avatarView);
            }
        }

        /// <summary>
        /// Cancel the emote whenever:
        /// - Moving horizontally
        /// - OR moving up
        /// - OR falling, which can only be true if the character is NOT grounded
        ///
        /// The falling flag is computed that way because it's possible to accumulate large vertical speed after teleporting
        /// even if the character is actually grounded and not moving down
        ///
        /// The JustTeleport tag check is needed because the grounded flag is set to false while we are in that 'just teleported' state.
        /// </summary>
        [Query]
        [None(typeof(CharacterEmoteIntent), typeof(PlayerTeleportIntent.JustTeleported))]
        private void CancelEmotesByMovement(ref CharacterEmoteComponent emoteComponent, in CharacterRigidTransform rigidTransform, in IAvatarView avatarView)
        {
            // The seemingly strange 0.447f value is because we were previously only using the squared threshold, and it was 0.2f
            // The value 0.447^2 is approximately 0.2f
            const float SPEED_THRESHOLD = 0.447f;
            const float SPEED_THRESHOLD_SQ = SPEED_THRESHOLD * SPEED_THRESHOLD;

            float horizontalSpeedSq = rigidTransform.MoveVelocity.Velocity.sqrMagnitude;
            float verticalSpeed = rigidTransform.GravityVelocity.y;

            bool shouldCancelEmote = horizontalSpeedSq > SPEED_THRESHOLD_SQ ||
                                     // If going up (v speed > 0), cancel the emote
                                     // Otherwise, we only cancel the emote if not grounded
                                     // This is because we always have some vertical velocity, even when grounded
                                     // See ApplyGravity.Execute(), all code paths ultimately add to CharacterRigidTransform.GravityVelocity
                                     (Mathf.Abs(verticalSpeed) > SPEED_THRESHOLD && (verticalSpeed > 0 || !rigidTransform.IsGrounded));

            if (shouldCancelEmote) StopEmote(ref emoteComponent, avatarView);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void StopEmote(ref CharacterEmoteComponent emoteComponent, IAvatarView avatarView)
        {
            if (emoteComponent.CurrentEmoteReference == null) return;

            emotePlayer.Stop(emoteComponent.CurrentEmoteReference);

            // Create a clean slate for the animator before setting the stop trigger
            avatarView.ResetAnimatorTrigger(AnimationHashes.EMOTE);
            avatarView.ResetAnimatorTrigger(AnimationHashes.EMOTE_RESET);
            avatarView.SetAnimatorTrigger(AnimationHashes.EMOTE_STOP);
            // See https://github.com/decentraland/unity-explorer/issues/4198
            // Some emotes changes the armature rotation, we need to restore it
            avatarView.ResetArmatureInclination();

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
                    if (emote.IsLoading)
                        return;

                    // emote failed to load? remove intent
                    if (emote.Model is { IsInitialized: true, Succeeded: false })
                    {
                        ReportHub.LogError(GetReportData(), $"Cant play emote {emoteId} since it failed loading \n the DTO");
                        World.Remove<CharacterEmoteIntent>(entity);
                        return;
                    }

                    // emote failed to load? remove intent
                    if (emote.DTO.assetBundleManifestVersion is { assetBundleManifestRequestFailed: true } and { IsLSDAsset: false })
                    {
                        ReportHub.LogError(GetReportData(), $"Cant play emote {emoteId} since it failed loading the manifest");
                        World.Remove<CharacterEmoteIntent>(entity);
                        return;
                    }

                    BodyShape bodyShape = avatarShapeComponent.BodyShape;

                    //Loading not complete
                    if (emote.AssetResults[bodyShape] == null)
                        return;

                    StreamableLoadingResult<AttachmentRegularAsset> streamableAssetValue = emote.AssetResults[bodyShape].Value;
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
                        ReportHub.LogError(ReportCategory.EMOTE, $"Emote name:{emoteId} cant be played.");

                    World.Remove<CharacterEmoteIntent>(entity);
                }
                else
                {
                    // Request the emote when not it cache. It will eventually endup in the emoteStorage so it can be played by this query
                    CreateEmotePromise(emoteId, avatarShapeComponent.BodyShape);
                }

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
        private void DisableCharacterController(ref CharacterController characterController, in CharacterEmoteComponent emoteComponent)
        {
            characterController.enabled = !emoteComponent.IsPlayingEmote;
        }

        [Query]
        private void DisableAnimatorWhenPlayingLegacyAnimations(in IAvatarView avatarView, in CharacterEmoteComponent emote)
        {
            if (emote.CurrentEmoteReference && emote.CurrentEmoteReference.legacy)
                avatarView.AvatarAnimator.enabled = false;
        }

        [Query]
        private void CleanUp(Profile profile, in DeleteEntityIntention deleteEntityIntention)
        {
            if (!deleteEntityIntention.DeferDeletion)
                messageBus.OnPlayerRemoved(profile.UserId);
        }

        private void CreateEmotePromise(URN urn, BodyShape bodyShape)
        {
            loadEmoteBuffer[0] = urn;

            if (GetSceneEmoteFromRealmIntention.TryParseFromURN(urn, out string sceneId, out string emoteHash, out bool loop))
            {
                if (!scenesCache.TryGetBySceneId(sceneId, out ISceneFacade? scene)) return;

                SceneEmoteFromRealmPromise.Create(World,
                    new GetSceneEmoteFromRealmIntention(sceneId, scene!.SceneData.SceneEntityDefinition.assetBundleManifestVersion!, emoteHash, loop, bodyShape),
                    PartitionComponent.TOP_PRIORITY);
            }
            else
                EmotePromise.Create(World,
                    EmoteComponentsUtils.CreateGetEmotesByPointersIntention(bodyShape, loadEmoteBuffer),
                    PartitionComponent.TOP_PRIORITY);
        }
    }
}
