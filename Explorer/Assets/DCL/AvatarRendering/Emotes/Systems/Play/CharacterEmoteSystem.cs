using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.AvatarRendering.AvatarShape;
using DCL.AvatarRendering.AvatarShape.Components;
using DCL.AvatarRendering.AvatarShape.UnityInterface;
using DCL.AvatarRendering.Emotes.Load;
using DCL.AvatarRendering.Emotes.SocialEmotes;
using DCL.AvatarRendering.Loading.Assets;
using DCL.AvatarRendering.Loading.Components;
using DCL.Character.Components;
using DCL.CharacterMotion.Components;
using DCL.CharacterMotion.Systems;
using DCL.DebugUtilities;
using DCL.Diagnostics;
using DCL.Multiplayer.Emotes;
using DCL.Profiles;
using DCL.SocialEmotes;
using DCL.UI.EphemeralNotifications;
using DCL.Utilities;
using DCL.Web3.Identities;
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
    [UpdateBefore(typeof(CleanUpGroup))]
    public partial class CharacterEmoteSystem : BaseUnityLoopSystem
    {
        /// <summary>
        /// Tells the system that the avatar has to be rotated so it coincides with the initiator, before playing a social emote.
        /// </summary>
        private struct RotateReceiverAvatarToCoincideWithInitiatorAvatarIntent
        {
            public readonly Entity InitiatorEntity;

            public RotateReceiverAvatarToCoincideWithInitiatorAvatarIntent(Entity initiatorEntity)
            {
                InitiatorEntity = initiatorEntity;
            }
        }

        // todo: use this to add nice Debug UI to trigger any emote?
        private readonly IDebugContainerBuilder debugContainerBuilder;
        private readonly IScenesCache scenesCache;

        private readonly IEmoteStorage emoteStorage;
        private readonly EmotePlayer emotePlayer;
        private readonly IEmotesMessageBus messageBus;
        private readonly URN[] loadEmoteBuffer = new URN[1];
        private readonly IWeb3IdentityCache identityCache;
        private readonly EphemeralNotificationsController ephemeralNotificationsController;
        private readonly SocialEmotesSettings socialEmotesSettings;

        // The local height of the Camera Focus object in the character controller, used to interpolate the position
        // of the camera when starting or finishing a social emote interaction
        private float cameraFocusHeight = 1.75f;

        public CharacterEmoteSystem(
            World world,
            IEmoteStorage emoteStorage,
            IEmotesMessageBus messageBus,
            AudioSource audioSource,
            IDebugContainerBuilder debugContainerBuilder,
            bool localSceneDevelopment,
            IAppArgs appArgs,
            IScenesCache scenesCache,
            IWeb3IdentityCache identityCache,
            EphemeralNotificationsController ephemeralNotificationsController,
            SocialEmotesSettings socialEmotesSettings) : base(world)
        {
            this.messageBus = messageBus;
            this.emoteStorage = emoteStorage;
            this.debugContainerBuilder = debugContainerBuilder;
            this.scenesCache = scenesCache;
            this.identityCache = identityCache;
            this.ephemeralNotificationsController = ephemeralNotificationsController;
            this.socialEmotesSettings = socialEmotesSettings;
            emotePlayer = new EmotePlayer(audioSource, legacyAnimationsEnabled: localSceneDevelopment || appArgs.HasFlag(AppArgsFlags.SELF_PREVIEW_BUILDER_COLLECTIONS));
        }

        protected override void Update(float t)
        {
            RemoveConsumedFilterMessagesByIsInstantIntentQuery(World);
            AvatarStateMachineEventHandlerInitializationQuery(World);
            CancelEmotesQuery(World);
            CancelEmotesByTeleportIntentionQuery(World);
            CancelEmotesOnMovePlayerToInvokedQuery(World);
            CancelEmotesByMovementQuery(World);
            ConsumeStopEmoteIntentQuery(World);
            ReplicateLoopingEmotesQuery(World);
            BeforePlayingCheckEmoteAssetQuery(World);
            BeforePlayingSynchronizeRemoteInteractionQuery(World);
            BeforePlayingStopCurrentEmoteQuery(World);
            PlayNewEmoteQuery(World);
            AfterPlayingUpdateSocialEmoteInteractionsQuery(World);
            RotateReceiverAvatarToCoincideWithInitiatorAvatarQuery(World); // This must occur after ConsumeEmoteIntentQuery, because it has to rotate the avatar one frame after the emote plays, at least
            ConsumeStopEmoteIntentQuery(World); // Repeated on purpose, if the state of both participants in a social emote interaction must be consistent all the time
            CancelEmotesByDeletionQuery(World);
            UpdateEmoteTagsQuery(World);
            DisableCharacterControllerQuery(World);
            DisableAnimatorWhenPlayingLegacyAnimationsQuery(World);
            CleanUpQuery(World);
        }

        [Query]
        [None(typeof(AvatarStateMachineEventHandler))]
        private void AvatarStateMachineEventHandlerInitialization(Entity entity, IAvatarView avatarView)
        {
            if(avatarView.AvatarAnimator)
                World.Add(entity, new AvatarStateMachineEventHandler(entity, avatarView.AvatarAnimator));
        }

        [Query]
        [All(typeof(DeleteEntityIntention))]
        private void CancelEmotesByDeletion(Entity entity, ref CharacterEmoteComponent emoteComponent, in IAvatarView avatarView, in Profile profile)
        {
            StopEmote(entity, ref emoteComponent, avatarView, profile.UserId);
        }

        /// <summary>
        /// Stops emote playback whenever the teleport intent is present on the entity.
        /// Doesn't handle movePlayerTo calls.
        /// </summary>
        [Query]
        [All(typeof(PlayerTeleportIntent))]
        [None(typeof(CharacterEmoteIntent), typeof(MovePlayerToInfo))]
        private void CancelEmotesByTeleportIntention(Entity entity, ref CharacterEmoteComponent emoteComponent, in IAvatarView avatarView, in Profile profile)
        {
            StopEmote(entity, ref emoteComponent, avatarView, profile.UserId);
        }

        /// <summary>
        /// Stops emote playback when movePlayerTo is invoked.<br/>
        /// Will not cancel a scene emote that was triggered the same frame movePlayerTo was invoked.
        /// </summary>
        [Query]
        [All(typeof(PlayerTeleportIntent))]
        [None(typeof(CharacterEmoteIntent))]
        private void CancelEmotesOnMovePlayerToInvoked(Entity entity, in MovePlayerToInfo movePlayerTo, ref CharacterEmoteComponent emoteComponent, in IAvatarView avatarView, in Profile profile)
        {
            if (World.TryGet(entity, out CharacterWaitingSceneEmoteLoading waitingEmote) &&
                movePlayerTo.FrameCount == waitingEmote.FrameCount)
                return;

            StopEmote(entity, ref emoteComponent, avatarView, profile.UserId);
        }

        // looping emotes and cancelling emotes by tag depend on tag change, this query alone is the one that updates that value at the ond of the update
        [Query]
        private void UpdateEmoteTags(ref CharacterEmoteComponent emoteComponent, in IAvatarView avatarView)
        {
            emoteComponent.CurrentAnimationTag = avatarView.GetAnimatorCurrentStateTag();
   //         ReportHub.Log(ReportCategory.EMOTE_DEBUG, "Update emote tag for " + ((AvatarBase)avatarView).name + " : " + emoteComponent.CurrentAnimationTag);
        }

        // emotes that do not loop need to trigger some kind of cancellation, so we can take care of the emote props and sounds
        [Query]
        [None(typeof(CharacterEmoteIntent), typeof(DeleteEntityIntention))]
        private void CancelEmotes(ref CharacterEmoteComponent emoteComponent, in IAvatarView avatarView, in Entity entity, in Profile profile)
        {
            bool wantsToCancelEmote = emoteComponent.StopEmote;
            emoteComponent.StopEmote = false;

            EmoteReferences? emoteReference = emoteComponent.CurrentEmoteReference;
            if (!emoteReference) return;

            bool shouldCancelEmote = wantsToCancelEmote || World.Has<HiddenPlayerComponent>(entity);
            if (shouldCancelEmote)
            {
                StopEmote(entity, ref emoteComponent, avatarView, profile.UserId);
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
                    StopEmote(entity, ref emoteComponent, avatarView, profile.UserId);
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
        // Note: Applies only to local avatar
        [Query]
        [None(typeof(CharacterEmoteIntent), typeof(PlayerTeleportIntent.JustTeleported), typeof(MoveBeforePlayingSocialEmoteIntent))]
        private void CancelEmotesByMovement(Entity entity, ref CharacterEmoteComponent emoteComponent, in CharacterRigidTransform rigidTransform, in IAvatarView avatarView, in Profile profile)
        {
            if(!emoteComponent.IsPlayingEmote)
                return;

            const float XZ_CUTOFF_LIMIT = 0.01f;
            // The seemingly strange 0.447f value is because we were previously only using the squared threshold, and it was 0.2f
            // The value 0.447^2 is approximately 0.2f
            const float VERTICAL_CUTOFF_LIMIT = 0.447f;

            float horizontalSpeedSq = rigidTransform.MoveVelocity.Velocity.sqrMagnitude;
            float verticalSpeed = rigidTransform.GravityVelocity.y;

            bool shouldCancelEmote = horizontalSpeedSq > XZ_CUTOFF_LIMIT ||
                                     // If going up (v speed > 0), cancel the emote
                                     // Otherwise, we only cancel the emote if not grounded
                                     // This is because we always have some vertical velocity, even when grounded
                                     // See ApplyGravity.Execute(), all code paths ultimately add to CharacterRigidTransform.GravityVelocity
                                     (Mathf.Abs(verticalSpeed) > VERTICAL_CUTOFF_LIMIT && (verticalSpeed > 0 || !rigidTransform.IsGrounded));

            if (!shouldCancelEmote) return;

            ReportHub.LogError(ReportCategory.EMOTE_DEBUG, "Cancel velocity: " + horizontalSpeedSq.ToString("F6") + " " + profile.UserId);

            URN emoteUrn = emoteComponent.EmoteUrn;

            StopEmote(entity, ref emoteComponent, avatarView, profile.UserId);

            if (emoteUrn != default)
            {
                // Sends stop signal to other clients
                messageBus.Send(emoteUrn, false, false, -1, false, string.Empty, string.Empty, true, emoteComponent.SocialEmote.InteractionId);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void StopEmote(Entity entity, ref CharacterEmoteComponent emoteComponent, IAvatarView avatarView, string walletAddress)
        {
            ReportHub.LogError(ReportCategory.EMOTE_DEBUG, "-StopEmote- " + ((AvatarBase)avatarView).name);

            if (emoteComponent.CurrentEmoteReference == null)
                return;

            emotePlayer.Stop(emoteComponent.CurrentEmoteReference);

            bool isSocialEmote = emoteComponent.Metadata != null && emoteComponent.Metadata.IsSocialEmote;

            if (isSocialEmote)
            {
                if(emoteComponent.SocialEmote.IsPlayingOutcome)
                    StopOtherParticipant(entity, ref emoteComponent, walletAddress);

                SocialEmoteInteractionsManager.Instance.StopInteraction(walletAddress);
            }

            // Create a clean slate for the animator before setting the stop trigger
            avatarView.ResetAnimatorTrigger(AnimationHashes.EMOTE);
            avatarView.ResetAnimatorTrigger(AnimationHashes.EMOTE_RESET);
            avatarView.SetAnimatorTrigger(AnimationHashes.EMOTE_STOP);

            avatarView.RestoreArmatureName();

            // See https://github.com/decentraland/unity-explorer/issues/4198
            // Some emotes changes the armature rotation, we need to restore it
            avatarView.ResetArmatureInclination();

            ReportHub.LogError(ReportCategory.EMOTE_DEBUG, "RESET EmoteComponent User: " + ((AvatarBase)avatarView).name);
            emoteComponent.Reset();

            // Cancels interpolating to start position in a social emote outcome animation
            if(World.Has<InterpolateToOutcomeStartPoseIntent>(entity))
                World.Get<InterpolateToOutcomeStartPoseIntent>(entity).HasBeenCancelled = true;
        }

        private void StopOtherParticipant(Entity entity, ref CharacterEmoteComponent emoteComponent, string walletAddress)
        {
            SocialEmoteInteractionsManager.ISocialEmoteInteractionReadOnly? interaction = SocialEmoteInteractionsManager.Instance.GetInteractionState(walletAddress);

            if (interaction != null)
            {
                ReportHub.LogError(ReportCategory.EMOTE_DEBUG, "Entities: initiator " + interaction.InitiatorEntity + " receiver " + interaction.ReceiverEntity);
                ReportHub.LogError(ReportCategory.EMOTE_DEBUG, "IsReactingToSocialEmote: " + emoteComponent.SocialEmote.IsReacting + " IsPlayingSocialEmoteOutcome " + emoteComponent.SocialEmote.IsPlayingOutcome);

                Entity socialEmoteAvatarToStop = Entity.Null;

                if (emoteComponent.SocialEmote.IsReacting) // It's the receiver
                    socialEmoteAvatarToStop = interaction.InitiatorEntity;
                else if(emoteComponent.SocialEmote.IsPlayingOutcome) // It's the initiator
                    socialEmoteAvatarToStop = interaction.ReceiverEntity;

                if (socialEmoteAvatarToStop != Entity.Null)
                {
                    ReportHub.LogError(ReportCategory.EMOTE_DEBUG, "Stop: " + entity + " also stops: " + socialEmoteAvatarToStop);
                    World.Add(socialEmoteAvatarToStop, new StopEmoteIntent(emoteComponent.EmoteUrn));
                }
            }
        }

        [Query]
        [All(typeof(StopEmoteIntent))]
        [None(typeof(DeleteEntityIntention))]
        private void ConsumeStopEmoteIntent(Entity entity, ref CharacterEmoteComponent emoteComponent, in IAvatarView avatarView, in Profile profile,
            in StopEmoteIntent stopEmoteIntent)
        {
            ReportHub.Log(ReportCategory.EMOTE_DEBUG, "stopintent urn: " + emoteComponent.EmoteUrn);

            if (emoteComponent.IsPlayingEmote && emoteComponent.EmoteUrn == stopEmoteIntent.EmoteUrn)
            {
                ReportHub.LogError(ReportCategory.EMOTE_DEBUG, "Consume stopintent urn: " + emoteComponent.EmoteUrn);
                StopEmote(entity, ref emoteComponent, avatarView, profile.UserId);
                World.Remove<StopEmoteIntent>(entity);
            }
        }

        [Query]
        [None(typeof(DeleteEntityIntention))]
        private void BeforePlayingCheckEmoteAsset(Entity entity, ref CharacterEmoteIntent emoteIntent,
            ref AvatarShapeComponent avatarShapeComponent)
        {
            if(emoteIntent.EmoteAsset != null) // TODO: Replace with another intent
                return;

            URN emoteId = emoteIntent.EmoteId;

            // it's very important to catch any exception here to avoid not consuming the emote intent, so we don't infinitely create props
            try
            {
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

                    StreamableLoadingResult<AttachmentRegularAsset> streamableAssetValue = emote.AssetResults[bodyShape]!.Value;

                    if (streamableAssetValue is { Succeeded: false } || streamableAssetValue.Asset?.MainAsset == null)
                    {
                        // We can't play emote, remove intent, otherwise there is no place to remove it
                        World.Remove<CharacterEmoteIntent>(entity);
                        return;
                    }

                    emoteIntent.EmoteAsset = emote;
                }
                else
                {
                    // Request the emote when not it cache. It will eventually endup in the emoteStorage so it can be played by this query
                    CreateEmotePromise(emoteIntent.EmoteId, avatarShapeComponent.BodyShape);
                }
            }
            catch (Exception e)
            {
                World.Remove<CharacterEmoteIntent>(entity);
                ReportHub.LogException(e, GetReportData());
            }
        }

        [Query]
        [None(typeof(DeleteEntityIntention))]
        private void BeforePlayingStopCurrentEmote(Entity entity, ref CharacterEmoteComponent emoteComponent, in CharacterEmoteIntent emoteIntent, in IAvatarView avatarView)
        {
            if (emoteIntent.EmoteAsset == null)
                return;

            ReportHub.LogError(ReportCategory.EMOTE_DEBUG, "Consuming emote - Is reacting? " + emoteIntent.SocialEmote.UseOutcomeReactionAnimation + " mov: " + avatarView.GetAnimatorFloat(AnimationHashes.MOVEMENT_BLEND));

            // it's very important to catch any exception here to avoid not consuming the emote intent, so we don't infinitely create props
            try
            {
                // we wait until the avatar finishes moving to trigger the emote,
                // avoid the case where: you stop moving, trigger the emote, the emote gets triggered and next frame it gets cancelled because inertia keeps moving the avatar
                //We also avoid triggering the emote while the character is jumping or landing, as the landing animation breaks the emote flow if they have props
                if (avatarView.IsAnimatorInTag(AnimationHashes.JUMPING_TAG) ||
                    (avatarView.GetAnimatorFloat(AnimationHashes.MOVEMENT_BLEND) > 0.1f && !emoteIntent.SocialEmote.UseOutcomeReactionAnimation)) // Note: When playing the outcome animation of an avatar that is reacting, there is no movement blending
                    return;

                if (emoteComponent.Metadata != null)
                    ReportHub.LogError(ReportCategory.EMOTE_DEBUG, "is social: " + emoteComponent.Metadata.IsSocialEmote + " is playing: " + emoteComponent.IsPlayingEmote + " urn1: " + emoteComponent.EmoteUrn.Shorten() + " urn2: " + emoteIntent.EmoteId.Shorten());

                // Previous social emote interaction has to be stopped before starting a new one
                // When the avatar is already playing a social emote (outcome phase) and then it plays the same one (start phase) it cancels the interaction
                // Playing a different emote cancels the interaction
                // If the emote is the same (excepting previous rules) it may be a loop animation and must NOT cancel the interaction
                if (emoteComponent.Metadata != null &&
                    emoteComponent.Metadata.IsSocialEmote &&
                    emoteComponent.IsPlayingEmote)
                {
                    bool isPlayingDifferentEmote = emoteComponent.EmoteUrn.Shorten() != emoteIntent.EmoteId.Shorten();

                    // It's a different emote OR it was playing the outcome phase and now it wants to play the start phase of the same emote interaction
                    // (triggered the same social emote again while the previous interaction didn't finish yet, it cancels it)
                    if (isPlayingDifferentEmote || (emoteComponent.SocialEmote.IsPlayingOutcome && !emoteIntent.SocialEmote.UseOutcomeAnimation))
                    {
                        ReportHub.LogError(ReportCategory.EMOTE_DEBUG, "DIFFERENT PHASE? " + emoteIntent.WalletAddress + " " + (emoteComponent.SocialEmote.IsPlayingOutcome && !emoteIntent.SocialEmote.UseOutcomeAnimation));
                        StopEmote(entity, ref emoteComponent, avatarView, emoteIntent.WalletAddress);
                    }

                    // Playing a social emote for a different interaction, it could happen if the initiator plays the same start animation
                    if (emoteComponent.SocialEmote.InteractionId != emoteIntent.SocialEmote.InteractionId &&
                        SocialEmoteInteractionsManager.Instance.InteractionExists(emoteComponent.SocialEmote.InitiatorWalletAddress))
                    {
                        ReportHub.LogError(ReportCategory.EMOTE_DEBUG, "Cancels social emote and resets transforms of initiator");
                        StopEmote(entity, ref emoteComponent, avatarView, emoteIntent.WalletAddress);
                    }
                }
            }
            catch (Exception e)
            {
                World.Remove<CharacterEmoteIntent>(entity);
                ReportHub.LogException(e, GetReportData());
            }
        }

        // This is a corner case that happens if 2 avatars are playing a looping social emote and a new avatar joins the scene. The third will receive the messages
        // of both players when they iterate the animation loop, but those messages arrive in an unknown order. So if the initiator comes first, it has to wait for the receiver's
        // and this flag is set until it arrives.
        // If the message of the receiver comes first, it's ignored (the message is discarded in a condition at RemoteEmotesSystem). It will be handled when the initiator's is already there waiting.
        // Once both have arrived, both emotes play at the same time.
        [Query]
        [None(typeof(DeleteEntityIntention))]
        private void BeforePlayingSynchronizeRemoteInteraction(Entity entity, ref CharacterEmoteComponent emoteComponent, ref CharacterEmoteIntent emoteIntent, ref CharacterTransform characterTransform)
        {
            if(emoteIntent.EmoteAsset == null || !emoteIntent.EmoteAsset.IsSocial)
                return;

            if (emoteIntent.SocialEmote.UseOutcomeReactionAnimation)
            {
                ReportHub.LogError(ReportCategory.EMOTE_DEBUG, "Simulating receiver " + emoteIntent.WalletAddress);

                SocialEmoteInteractionsManager.ISocialEmoteInteractionReadOnly? interaction = SocialEmoteInteractionsManager.Instance.GetInteractionState(emoteIntent.SocialEmote.InitiatorWalletAddress);

                // The message of the receiver arrived and there is already an initiator waiting for that interaction...
                if(interaction != null &&
                   World.TryGet(interaction.InitiatorEntity, out CharacterEmoteIntent initiatorIntent) &&
                   initiatorIntent.SocialEmote.IsInitiatorOutcomeAnimationWaitingForReceiverAnimationLoop)
                {
                    ReportHub.LogError(ReportCategory.EMOTE_DEBUG, "Initiator UNLOCKED " + initiatorIntent.WalletAddress);
                    initiatorIntent.SocialEmote.IsInitiatorOutcomeAnimationWaitingForReceiverAnimationLoop = false;

                    World.Set(interaction.InitiatorEntity, initiatorIntent);
                }
            }
            else if(emoteIntent.SocialEmote.IsInitiatorOutcomeAnimationWaitingForReceiverAnimationLoop)
            {
                ReportHub.LogError(ReportCategory.EMOTE_DEBUG, "Simulating initiator " + emoteIntent.WalletAddress);

                // Starting interaction (no emote is played yet)
                SocialEmoteInteractionsManager.Instance.StartInteraction(emoteIntent.WalletAddress, entity, emoteIntent.EmoteAsset, characterTransform.Transform, emoteIntent.SocialEmote.InteractionId, emoteIntent.SocialEmote.TargetAvatarWalletAddress);
                emoteComponent.SocialEmote.InitiatorWalletAddress = emoteIntent.WalletAddress;
            }
        }

        // This query takes care of consuming the CharacterEmoteIntent to trigger an emote
        [Query]
        [None(typeof(DeleteEntityIntention))]
        private void PlayNewEmote(Entity entity, ref CharacterEmoteComponent emoteComponent, ref CharacterEmoteIntent emoteIntent,
            in IAvatarView avatarView, ref AvatarShapeComponent avatarShapeComponent)
        {
            if(emoteIntent.EmoteAsset == null || emoteIntent.SocialEmote.IsInitiatorOutcomeAnimationWaitingForReceiverAnimationLoop)
                return;

            IEmote emote = emoteIntent.EmoteAsset;

            ReportHub.LogError(ReportCategory.EMOTE_DEBUG, "Consuming emote - address: " + emoteIntent.WalletAddress +  "Is reacting? " + emoteIntent.SocialEmote.UseOutcomeReactionAnimation + " mov: " + avatarView.GetAnimatorFloat(AnimationHashes.MOVEMENT_BLEND));

            // it's very important to catch any exception here to avoid not consuming the emote intent, so we don't infinitely create props
            try
            {
                // Cancels walking to the initiator of a social emote
                if(World.Has<MoveBeforePlayingSocialEmoteIntent>(entity))
                    World.Get<MoveBeforePlayingSocialEmoteIntent>(entity).HasBeenCancelled = true;

                // we wait until the avatar finishes moving to trigger the emote,
                // avoid the case where: you stop moving, trigger the emote, the emote gets triggered and next frame it gets cancelled because inertia keeps moving the avatar
                //We also avoid triggering the emote while the character is jumping or landing, as the landing animation breaks the emote flow if they have props
                if (avatarView.IsAnimatorInTag(AnimationHashes.JUMPING_TAG) ||
                    (avatarView.GetAnimatorFloat(AnimationHashes.MOVEMENT_BLEND) > 0.1f && !emoteIntent.SocialEmote.UseOutcomeReactionAnimation)) // Note: When playing the outcome animation of an avatar that is reacting, there is no movement blending
                    return;

                BodyShape bodyShape = avatarShapeComponent.BodyShape;
                GameObject mainAsset = emote.AssetResults[bodyShape]!.Value.Asset!.MainAsset;

                // Existing emoteComponent is overwritten with new emote info
                emoteComponent.Reset();
                emoteComponent.EmoteUrn = emoteIntent.EmoteId;
                emoteComponent.Metadata = (EmoteDTO.EmoteMetadataDto)emote.DTO.Metadata;
                StreamableLoadingResult<AudioClipData>? audioAssetResult = emote.AudioAssetResults[bodyShape];
                AudioClip? audioClip = audioAssetResult?.Asset;

                emoteComponent.SocialEmote.IsPlayingOutcome = emoteIntent.SocialEmote.UseOutcomeAnimation;
                emoteComponent.SocialEmote.CurrentOutcome = emoteIntent.SocialEmote.OutcomeIndex;
                emoteComponent.SocialEmote.IsReacting = emoteIntent.SocialEmote.UseOutcomeReactionAnimation;
                emoteComponent.SocialEmote.InitiatorWalletAddress = emoteIntent.SocialEmote.InitiatorWalletAddress;
                emoteComponent.SocialEmote.InteractionId = emoteIntent.SocialEmote.InteractionId;
                emoteComponent.SocialEmote.TargetAvatarWalletAddress = emoteIntent.SocialEmote.TargetAvatarWalletAddress;

                if (emoteComponent.Metadata.IsSocialEmote &&
                    emoteIntent.TriggerSource != TriggerSource.PREVIEW &&
                    emoteComponent.SocialEmote.IsPlayingOutcome &&
                    emoteComponent.SocialEmote.CurrentOutcome < emote.SocialEmoteOutcomeAudioAssetResults.Count) // TODO: This IF step should never be true
                {
                    ReportHub.LogError(ReportCategory.EMOTE_DEBUG, "AUDIO for outcome " + emoteComponent.SocialEmote.CurrentOutcome);

                    audioClip = emote.SocialEmoteOutcomeAudioAssetResults[emoteComponent.SocialEmote.CurrentOutcome].Asset;
                }

                ReportHub.LogError(ReportCategory.EMOTE_DEBUG, "PLAY USER: " + emoteIntent.WalletAddress);

                bool playedSuccessfully = emotePlayer.Play(mainAsset, audioClip, emote.IsLooping(), emoteIntent.Spatial, in avatarView, ref emoteComponent);

                if (!playedSuccessfully)
                {
                    ReportHub.LogError(ReportCategory.EMOTE, $"Emote name:{emoteIntent.EmoteId} cant be played.");
                    World.Remove<CharacterEmoteIntent>(entity);
                    return;
                }

                emoteIntent.HasPlayedEmote = true;

                ReportHub.LogError(ReportCategory.EMOTE_DEBUG, "AFTER PLAY USER: hash " + emoteComponent.GetHashCode() + " " + emoteIntent.WalletAddress + " " + emoteComponent.EmoteUrn + " " + emoteComponent.Metadata?.name ?? string.Empty);
            }
            catch (Exception e)
            {
                World.Remove<CharacterEmoteIntent>(entity);
                ReportHub.LogException(e, GetReportData());
            }
        }

        [Query]
        [None(typeof(DeleteEntityIntention))]
        private void AfterPlayingUpdateSocialEmoteInteractions(Entity entity, ref CharacterEmoteComponent emoteComponent, in CharacterEmoteIntent emoteIntent,
            CharacterTransform characterTransform, AvatarStateMachineEventHandler avatarStateMachineEventHandler)
        {
            if(emoteIntent.EmoteAsset == null || !emoteIntent.HasPlayedEmote)
                return;

            // it's very important to catch any exception here to avoid not consuming the emote intent, so we don't infinitely create props
            try
            {
                IEmote? emote = emoteIntent.EmoteAsset;

                if (emoteComponent.Metadata!.IsSocialEmote && emoteIntent.TriggerSource != TriggerSource.PREVIEW)
                {
                    if (emoteComponent.SocialEmote.IsPlayingOutcome)
                    {
                        if (SocialEmoteInteractionsManager.Instance.InteractionExists(emoteIntent.SocialEmote.InitiatorWalletAddress) && // When the outcome is a loop, it may receive an emote intent when the interaction has just finished locally
                            emoteComponent.SocialEmote.IsReacting)
                        {
                            // Reacting to the interaction
                            SocialEmoteInteractionsManager.Instance.AddParticipantToInteraction(emoteIntent.WalletAddress, entity, emoteComponent.SocialEmote.CurrentOutcome, emoteIntent.SocialEmote.InitiatorWalletAddress);
                        }
                    }
                    else
                    {
                        // Starting interaction
                        SocialEmoteInteractionsManager.Instance.StartInteraction(emoteIntent.WalletAddress, entity, emote, characterTransform.Transform, emoteComponent.SocialEmote.InteractionId, emoteIntent.SocialEmote.TargetAvatarWalletAddress);
                        emoteComponent.SocialEmote.InitiatorWalletAddress = emoteIntent.WalletAddress;

                        // Directed social emote
                        if (emoteIntent.SocialEmote.TargetAvatarWalletAddress == identityCache.Identity!.Address)
                        {
                            // The outline of the initiator blinks
                            if (!World.Has<PlayAvatarHighlightBlinkingAnimationIntent>(entity))
                                World.Add(entity, new PlayAvatarHighlightBlinkingAnimationIntent(socialEmotesSettings.DirectedEmoteReceivedAnimationThickness, socialEmotesSettings.DirectedEmoteReceivedAnimationColor, socialEmotesSettings.DirectedEmoteReceivedAnimationDuration, socialEmotesSettings.DirectedEmoteReceivedAnimationLoops));

                            // Notification displayed
                            ephemeralNotificationsController.AddNotificationAsync(socialEmotesSettings.DirectedSocialEmoteEphemeralNotificationPrefab.name, emoteIntent.WalletAddress, new string[] { emote.GetName() }).Forget();
                        }
                    }
                }
                else if (!emoteComponent.Metadata.IsSocialEmote &&
                         emoteIntent.SocialEmote.TargetAvatarWalletAddress == identityCache.Identity!.Address &&
                         !emoteIntent.IsRepeating)
                {
                    // Directed normal emote
                    // Notification displayed
                    ephemeralNotificationsController.AddNotificationAsync(socialEmotesSettings.DirectedEmoteEphemeralNotificationPrefab.name, emoteIntent.WalletAddress, new string[] { emote.GetName() }).Forget();
                }

                if (emoteComponent.Metadata.IsSocialEmote && emoteIntent.SocialEmote.UseOutcomeReactionAnimation)
                {
                    ReportHub.LogError(ReportCategory.EMOTE_DEBUG, "Added Animate");
                    PrepareToAdjustReceiverBeforeOutcomeAnimation(emoteIntent.SocialEmote.InitiatorWalletAddress);

                    // The rotation of the avatar has to coincide with initiator avatar's when the emote starts, which occurs at least 1 frame later
                    World.Add(entity, new RotateReceiverAvatarToCoincideWithInitiatorAvatarIntent(SocialEmoteInteractionsManager.Instance.GetInteractionState(emoteIntent.WalletAddress)!.InitiatorEntity));
                }

                if (emoteComponent.Metadata!.IsSocialEmote)
                    avatarStateMachineEventHandler.EmoteStateExiting = OnEmoteStateExiting; // Setting and not subscribing because it could play the emote more than once and we can't know if it is the first for this client

                World.Remove<CharacterEmoteIntent>(entity);
            }
            catch (Exception e)
            {
                World.Remove<CharacterEmoteIntent>(entity);
                ReportHub.LogException(e, GetReportData());
            }
        }

        // It just removes the intent once it has been consumed in PlayerMovementNetSendSystem
        [Query]
        [All(typeof(PlayerTeleportIntent.JustTeleportedLocally))]
        private void RemoveConsumedFilterMessagesByIsInstantIntent(Entity entity, ref PlayerTeleportIntent.JustTeleportedLocally intent)
        {
            if (intent.IsConsumed)
            {
                ReportHub.LogError(ReportCategory.EMOTE_DEBUG, "REMOVE TELEPORT");
                World.Remove<PlayerTeleportIntent.JustTeleportedLocally>(entity);
            }
        }

        [Query]
        [All(typeof(RotateReceiverAvatarToCoincideWithInitiatorAvatarIntent))]
        private void RotateReceiverAvatarToCoincideWithInitiatorAvatar(Entity entity, ref IAvatarView avatarView, RotateReceiverAvatarToCoincideWithInitiatorAvatarIntent animationInfo)
        {
            // It waits for the Emote state to execute
            if (avatarView.GetAnimatorCurrentStateTag() != AnimationHashes.EMOTE)
                return;

            ReportHub.LogError(ReportCategory.EMOTE_DEBUG, "Rotation of reacting avatar as initiator");

            // Makes sure the avatar of the initiator is looking at the receiver's
            IAvatarView initiatorAvatar = World.Get<IAvatarView>(animationInfo.InitiatorEntity);
            Vector3 initiatorForward = avatarView.GetTransform().position - initiatorAvatar.GetTransform().position;
            initiatorForward.y = 0.0f;
            initiatorAvatar.GetTransform().forward = initiatorForward.normalized;

            // Rotates the reacting avatar to coincide with the initiator's avatar, because the animation of the reaction occurs at the same place with same pose
            avatarView.GetTransform().rotation = initiatorAvatar.GetTransform().rotation;
            World.Remove<RotateReceiverAvatarToCoincideWithInitiatorAvatarIntent>(entity);
        }

        // Every time the emote is looped we send a new message that should refresh the looping emotes on clients that didn't receive the initial message yet
        // TODO (Kinerius): This does not support scene emotes yet
        [Query]
        [All(typeof(PlayerComponent))]
        [None(typeof(CharacterEmoteIntent))]
        private void ReplicateLoopingEmotes(ref CharacterEmoteComponent emoteComponent, in IAvatarView avatarView, Profile profile)
        {
            int prevTag = emoteComponent.CurrentAnimationTag;
            int currentTag = avatarView.GetAnimatorCurrentStateTag();

            if ((prevTag != AnimationHashes.EMOTE || currentTag != AnimationHashes.EMOTE_LOOP)
                && (prevTag != AnimationHashes.EMOTE_LOOP || currentTag != AnimationHashes.EMOTE))
                return;

            ReportHub.Log(ReportCategory.EMOTE_DEBUG, "Looping " + profile.UserId + " " + prevTag + " hash " + emoteComponent.GetHashCode());

            if (emoteComponent.EmoteUrn.IsNullOrEmpty())
            {
                ReportHub.LogError(ReportCategory.EMOTE_DEBUG, "URN null");
                return;
            }

            messageBus.Send(emoteComponent.EmoteUrn, true, emoteComponent.SocialEmote.IsPlayingOutcome, emoteComponent.SocialEmote.CurrentOutcome, emoteComponent.SocialEmote.IsReacting, emoteComponent.SocialEmote.InitiatorWalletAddress, emoteComponent.SocialEmote.TargetAvatarWalletAddress, false, emoteComponent.SocialEmote.InteractionId);
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
            if(SocialEmoteInteractionsManager.Instance.InteractionExists(profile.UserId))
                SocialEmoteInteractionsManager.Instance.StopInteraction(profile.UserId);

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

        private void ResetAvatarAndControllerTransforms(Entity entity)
        {
            AvatarBase avatarBase = (AvatarBase)World.Get<IAvatarView>(entity);

            ReportHub.LogError(ReportCategory.EMOTE_DEBUG, "<color=cyan>EXITING EMOTE STATE " + avatarBase.name + " </color>");

            avatarBase.RestoreArmatureName(); // This is necessary to avoid jittering during the transition

            Vector3 newCharacterForward = avatarBase.HipAnchorPoint.forward;
            newCharacterForward.y = 0.0f;
            newCharacterForward.Normalize();

            GizmoDrawer.Instance.DrawWireCube(0, avatarBase.GetTransform().position, 0.2f * Vector3.one, Color.coral);

            Vector3 hipsWorldPosition = avatarBase.HipAnchorPoint.position;
            hipsWorldPosition.y = avatarBase.GetTransform().position.y;

            ref CharacterController characterController = ref World.TryGetRef<CharacterController>(entity, out bool isLocal);
            ref CharacterRigidTransform characterRigidTransform = ref World.TryGetRef<CharacterRigidTransform>(entity, out bool _);
            ref PlayerComponent playerComponent = ref World.TryGetRef<PlayerComponent>(entity, out bool _);

            if(isLocal)
                Debug.DrawRay(characterController.transform.position + characterController.center, newCharacterForward, Color.red, 3.0f);
            else
                Debug.DrawRay(hipsWorldPosition, newCharacterForward, Color.magenta, 3.0f);

            Vector3 cameraFocusCurrentPosition = Vector3.zero;

            // Below, the CharacterController or CharacterTransform will be placed where the avatar is,
            // and the avatar, which is its child, will reset its local transform to coincide with its parent

            try
            {
                if (isLocal)
                {
                    ReportHub.LogError(ReportCategory.EMOTE_DEBUG, "Local controller transform reset " + avatarBase.name);
                    cameraFocusCurrentPosition = playerComponent.CameraFocus.position;
                    playerComponent.CameraFocus.transform.parent = null; // It's necessary to do this before the interpolation starts in order to avoid jittering
                    characterController.transform.position = hipsWorldPosition;
                    characterRigidTransform.MoveVelocity.Velocity = Vector3.zero;
                    characterRigidTransform.LookDirection = newCharacterForward;
                }
                else
                {
                    // Although the position of the remote avatar will be overriden with incoming position messages, the animation may finish
                    // before those messages arrive so we have to update the position as if they already arrived or the avatar will make a weird movement
                    ReportHub.LogError(ReportCategory.EMOTE_DEBUG, "Remote controller transform reset " + avatarBase.name);
                    ref CharacterTransform characterTransform = ref World.Get<CharacterTransform>(entity);
                    characterTransform.Transform.position = hipsWorldPosition;
                    characterTransform.Transform.forward = newCharacterForward;
                }

                ReportHub.LogError(ReportCategory.EMOTE_DEBUG, "Avatar transform reset");

                avatarBase.GetTransform().localPosition = Vector3.zero;
                avatarBase.GetTransform().localRotation = Quaternion.identity;

                if (isLocal)
                {
                    World.Add(entity, new PlayerLookAtIntent(characterController.transform.position + characterController.center + newCharacterForward));
                    // With this intent the next network movement message is marked as instant which will be used in the other clients to avoid
                    // a problem that made the remote avatar move to a previous position (the old position of the CharacterController) before moving
                    // to the current position, due to interpolation
                    World.Add<PlayerTeleportIntent.JustTeleportedLocally>(entity);

                    // Interpolates the position of the object the camera is looking at, from current position to original position in the controller
                    World.Add(entity, new InterpolateCameraTargetTowardsNewParentIntent(cameraFocusCurrentPosition, characterController.transform, cameraFocusHeight));
                }

                if(isLocal)
                    Debug.DrawRay(characterController.transform.position + characterController.center, avatarBase.GetTransform().forward, Color.cyan, 3.0f);
                else
                    Debug.DrawRay(hipsWorldPosition, avatarBase.GetTransform().forward, Color.blue, 3.0f);
            }
            catch // The try/catch is necessary to avoid that playerComponent.CameraFocus ends up without parent due to an exception
            {
                if(characterController != null)
                    playerComponent.CameraFocus.parent = characterController.transform;

                throw;
            }
        }

        private void PrepareToAdjustReceiverBeforeOutcomeAnimation(string initiatorWalletAddress)
        {
            SocialEmoteInteractionsManager.ISocialEmoteInteractionReadOnly interaction = SocialEmoteInteractionsManager.Instance.GetInteractionState(initiatorWalletAddress)!;

            // Since the avatar is reacting, the emote is already available
            AvatarBase receiverAvatar = (AvatarBase)World.TryGetRef<IAvatarView>(interaction.ReceiverEntity, out bool _);
            AvatarBase initiatorAvatar = (AvatarBase)World.TryGetRef<IAvatarView>(interaction.InitiatorEntity, out bool _);

            // Calculates the pose of the receiver avatar when the outcome animation starts, to be used as target in the interpolation
            Vector3 originalAvatarPosition = receiverAvatar.GetTransform().position;

            ReportHub.LogError(ReportCategory.EMOTE_DEBUG, $"<color=#FF9933>Movement: {originalAvatarPosition.ToString("F3")} -> {initiatorAvatar.GetTransform().position.ToString("F3")}</color>");

            // Adjustment interpolation
            World.Add(interaction.ReceiverEntity, new InterpolateToOutcomeStartPoseIntent(
                originalAvatarPosition,
                initiatorAvatar.GetTransform().position));

            // Interpolates the position of the object the camera is looking at, from current position to the position of the avatar's head
            if (World.TryGet(interaction.ReceiverEntity, out PlayerComponent playerComponent))
            {
                cameraFocusHeight = playerComponent.CameraFocus.localPosition.y;
                World.Add(interaction.ReceiverEntity, new InterpolateCameraTargetTowardsNewParentIntent(playerComponent.CameraFocus.position, receiverAvatar.GetTransform(), cameraFocusHeight));
            }
        }

        // Comes from the animator's state machine behaviour
        private void OnEmoteStateExiting(Entity entity, AvatarStateMachineEventHandler avatarStateMachineEventHandler)
        {
            avatarStateMachineEventHandler.EmoteStateExiting = null;

            // This must occur right at the moment the Emote or Emote Loop states transition to Movement
            ResetAvatarAndControllerTransforms(entity);
        }
    }
}
