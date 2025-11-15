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
using ECS.StreamableLoading.AudioClips;
using ECS.StreamableLoading.Common.Components;
using Global.AppArgs;
using System;
using System.Runtime.CompilerServices;
using ECS.SceneLifeCycle;
using SceneRunner.Scene;
using UnityEngine;
using Utility.Animations;
using EmotePromise = ECS.StreamableLoading.Common.AssetPromise<DCL.AvatarRendering.Emotes.EmotesResolution,
    DCL.AvatarRendering.Emotes.GetEmotesByPointersIntention>;
using SceneEmoteFromRealmPromise = ECS.StreamableLoading.Common.AssetPromise<DCL.AvatarRendering.Emotes.EmotesResolution,
    DCL.AvatarRendering.Emotes.GetSceneEmoteFromRealmIntention>;

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
        // todo: use this to add nice Debug UI to trigger any emote?
        private readonly IDebugContainerBuilder debugContainerBuilder;
        private readonly IScenesCache scenesCache;

        private readonly IEmoteStorage emoteStorage;
        private readonly EmotePlayer emotePlayer;
        private readonly IEmotesMessageBus messageBus;
        private readonly URN[] loadEmoteBuffer = new URN[1];
        private readonly IWeb3IdentityCache identityCache;
        private readonly EphemeralNotificationsController ephemeralNotificationsController;

        private const string DIRECTED_SOCIAL_EMOTE_EPHEMERAL_NOTIFICATION_PREFAB_NAME = "DirectedSocialEmoteEphemeralNotification";
        private const string DIRECTED_EMOTE_EPHEMERAL_NOTIFICATION_PREFAB_NAME = "DirectedEmoteEphemeralNotification";

        private readonly IWeb3IdentityCache playerIdentity;

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
            EphemeralNotificationsController ephemeralNotificationsController) : base(world)
        {
            this.messageBus = messageBus;
            this.emoteStorage = emoteStorage;
            this.debugContainerBuilder = debugContainerBuilder;
            this.scenesCache = scenesCache;
            this.identityCache = identityCache;
            this.ephemeralNotificationsController = ephemeralNotificationsController;
            emotePlayer = new EmotePlayer(audioSource, legacyAnimationsEnabled: localSceneDevelopment || appArgs.HasFlag(AppArgsFlags.SELF_PREVIEW_BUILDER_COLLECTIONS));
            this.playerIdentity = playerIdentity;
        }

        protected override void Update(float t)
        {
            CancelEmotesQuery(World);
            CancelEmotesByTeleportIntentionQuery(World);
            CancelEmotesByMovementQuery(World);
            ConsumeStopEmoteIntentQuery(World);
            ReplicateLoopingEmotesQuery(World);
            ConsumeEmoteIntentQuery(World);
            RotateReceiverAvatarToCoincideWithInitiatorAvatarQuery(World); // This must occur after ConsumeEmoteIntentQuery, because it has to rotate the avatar one frame after the emote plays, at least
            ConsumeStopEmoteIntentQuery(World); // Repeated on purpose, if the state of both participants in a social emote interaction must be consistent all the time
            CancelEmotesByDeletionQuery(World);
            UpdateEmoteTagsQuery(World);
            DisableCharacterControllerQuery(World);
            CleanUpQuery(World);
        }

        [Query]
        [All(typeof(DeleteEntityIntention))]
        private void CancelEmotesByDeletion(Entity entity, ref CharacterEmoteComponent emoteComponent, in IAvatarView avatarView, in Profile profile)
        {
            StopEmote(entity, ref emoteComponent, avatarView, profile.UserId);
        }

        [Query]
        [All(typeof(PlayerTeleportIntent))]
        [None(typeof(CharacterEmoteIntent))]
        private void CancelEmotesByTeleportIntention(Entity entity, ref CharacterEmoteComponent emoteComponent, in IAvatarView avatarView, in Profile profile)
        {
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

        // when moving or jumping we detect the emote cancellation, and we take care of getting rid of the emote props and sounds
        // Note: Applies only to local avatar
        [Query]
        [None(typeof(CharacterEmoteIntent), typeof(MoveBeforePlayingSocialEmoteIntent))]
        private void CancelEmotesByMovement(Entity entity, ref CharacterEmoteComponent emoteComponent, in CharacterRigidTransform rigidTransform, in IAvatarView avatarView, in Profile profile)
        {
            const float XZ_CUTOFF_LIMIT = 0.01f;
            const float VERTICAL_CUTOFF_LIMIT = 0.2f;

            float velocity = rigidTransform.MoveVelocity.Velocity.sqrMagnitude;
            float verticalVelocity = Mathf.Abs(rigidTransform.GravityVelocity.sqrMagnitude);

            bool canEmoteBeCancelled = velocity > XZ_CUTOFF_LIMIT || verticalVelocity > VERTICAL_CUTOFF_LIMIT;

            if (!canEmoteBeCancelled) return;

            ReportHub.LogError(ReportCategory.EMOTE_DEBUG, "Cancel velocity: " + velocity.ToString("F6") + " " + profile.UserId);

            URN emoteUrn = emoteComponent.EmoteUrn;

            StopEmote(entity, ref emoteComponent, avatarView, profile.UserId);

            if (emoteUrn != default)
            {
                // Sends stop signal to other clients
                messageBus.Send(emoteUrn, false, false, -1, false, string.Empty, string.Empty, true, emoteComponent.SocialEmoteInteractionId);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void StopEmote(Entity entity, ref CharacterEmoteComponent emoteComponent, IAvatarView avatarView, string walletAddress)
        {
            ReportHub.LogError(ReportCategory.EMOTE_DEBUG, "-StopEmote- " + ((AvatarBase)avatarView).name);

            if (emoteComponent.CurrentEmoteReference == null)
                return;

            emotePlayer.Stop(emoteComponent.CurrentEmoteReference);

            if (emoteComponent.Metadata != null && emoteComponent.Metadata.IsSocialEmote)
            {
                if(emoteComponent.IsPlayingSocialEmoteOutcome)
                    StopOtherParticipant(entity, ref emoteComponent, walletAddress);

                SocialEmoteInteractionsManager.Instance.StopInteraction(walletAddress);
            }

            // Create a clean slate for the animator before setting the stop trigger
            avatarView.ResetAnimatorTrigger(AnimationHashes.EMOTE);
            avatarView.ResetAnimatorTrigger(AnimationHashes.EMOTE_RESET);
            avatarView.SetAnimatorTrigger(AnimationHashes.EMOTE_STOP);

            avatarView.RestoreArmatureName();

            avatarView.GetTransform().localPosition = Vector3.zero;
            avatarView.GetTransform().localRotation = Quaternion.identity;

            ReportHub.LogError(ReportCategory.EMOTE_DEBUG, "RESET EmoteComponent User: " + ((AvatarBase)avatarView).name);
            emoteComponent.Reset();

            if(World.Has<MoveBeforePlayingSocialEmoteIntent>(entity))
                World.Remove<MoveBeforePlayingSocialEmoteIntent>(entity);
        }

        private void StopOtherParticipant(Entity entity, ref CharacterEmoteComponent emoteComponent, string walletAddress)
        {
            SocialEmoteInteractionsManager.ISocialEmoteInteractionReadOnly? interaction = SocialEmoteInteractionsManager.Instance.GetInteractionState(walletAddress);

            if (interaction != null)
            {
                ReportHub.LogError(ReportCategory.EMOTE_DEBUG, "Entities: initiator " + interaction.InitiatorEntity + " receiver " + interaction.ReceiverEntity);
                ReportHub.LogError(ReportCategory.EMOTE_DEBUG, "IsReactingToSocialEmote: " + emoteComponent.IsReactingToSocialEmote + " IsPlayingSocialEmoteOutcome " + emoteComponent.IsPlayingSocialEmoteOutcome);

                Entity socialEmoteAvatarToStop = Entity.Null;

                if (emoteComponent.IsReactingToSocialEmote) // It's the receiver
                    socialEmoteAvatarToStop = interaction.InitiatorEntity;
                else if(emoteComponent.IsPlayingSocialEmoteOutcome) // It's the initiator
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
            if (emoteComponent.IsPlayingEmote && emoteComponent.EmoteUrn == stopEmoteIntent.EmoteUrn)
            {
                ReportHub.LogError(ReportCategory.EMOTE_DEBUG, "Consume stopintent urn: " + emoteComponent.EmoteUrn);
                StopEmote(entity, ref emoteComponent, avatarView, profile.UserId);
                World.Remove<StopEmoteIntent>(entity);
            }
        }

        private void PrepareToAdjustReceiverBeforeOutcomeAnimation(string initiatorWalletAddress)
        {
            SocialEmoteInteractionsManager.ISocialEmoteInteractionReadOnly interaction = SocialEmoteInteractionsManager.Instance.GetInteractionState(initiatorWalletAddress)!;

            // Since the avatar is reacting, the emote is already available
            IEmote emote;
            URN emoteUrn = interaction.Emote.GetUrn().Shorten();
            emoteStorage.TryGetElement(emoteUrn, out emote);

            Vector3 targetAvatarHipRelativePosition = Vector3.zero;

            // All social emotes should have this info, this protection is added just to prevent old test emotes from failing
            if(emote.AssetResults[BodyShape.MALE]!.Value.Asset!.SocialEmoteOutcomeAnimationStartPoses != null)
                targetAvatarHipRelativePosition = emote.AssetResults[BodyShape.MALE]!.Value.Asset!.SocialEmoteOutcomeAnimationStartPoses[interaction.OutcomeIndex].Position;

            ReportHub.LogError(ReportCategory.EMOTE_DEBUG, "<color=#FF9933>target hip: " + targetAvatarHipRelativePosition.ToString("F6") + "</color>");

            AvatarBase receiverAvatar = (AvatarBase)World.TryGetRef<IAvatarView>(interaction.ReceiverEntity, out bool _);
            AvatarBase initiatorAvatar = (AvatarBase)World.TryGetRef<IAvatarView>(interaction.InitiatorEntity, out bool _);

            Vector3 originalAvatarPosition = receiverAvatar.GetTransform().position;
            Vector3 originalHipRelativePosition = Vector3.Scale(receiverAvatar.HipAnchorPoint.localPosition, receiverAvatar.HipAnchorPoint.parent.localScale);
            Vector3 targetAvatarPosition = initiatorAvatar.GetTransform().position
                                           + initiatorAvatar.GetTransform().rotation * new Vector3(targetAvatarHipRelativePosition.x, 0.0f, targetAvatarHipRelativePosition.z)
                                           // Small adjustment to make current position of the hips in the current animation with the future position of the hips
                                           - receiverAvatar.GetTransform().rotation * new Vector3(originalHipRelativePosition.x, 0.0f, originalHipRelativePosition.y);

            ReportHub.LogError(ReportCategory.EMOTE_DEBUG, $"<color=#FF9933>Movement: {originalAvatarPosition.ToString("F3")} -> {targetAvatarPosition.ToString("F3")}</color>");

            GizmoDrawer.Instance.DrawWireSphere(3, targetAvatarPosition, 0.2f, Color.magenta);

//              targetAvatarPosition = targetAvatarPosition + (targetAvatarPosition - moveIntent.InitiatorWorldPosition);

            // Adjustment interpolation
            World.Add(interaction.ReceiverEntity, new MoveToOutcomeStartPositionIntent(
                originalAvatarPosition,
                receiverAvatar.GetTransform().rotation,
                targetAvatarPosition,
                initiatorAvatar.GetTransform().rotation,
                new TriggerEmoteReactingToSocialEmoteIntent(emoteUrn, interaction.OutcomeIndex, initiatorWalletAddress, interaction.Id),
                initiatorAvatar.GetTransform().position));
        }

        // This query takes care of consuming the CharacterEmoteIntent to trigger an emote
        [Query]
        [None(typeof(DeleteEntityIntention))]
        private void ConsumeEmoteIntent(Entity entity, ref CharacterEmoteComponent emoteComponent, in CharacterEmoteIntent emoteIntent,
            in IAvatarView avatarView, ref AvatarShapeComponent avatarShapeComponent, CharacterTransform characterTransform)
        {
            URN emoteId = emoteIntent.EmoteId;

            ReportHub.LogError(ReportCategory.EMOTE_DEBUG, "Consuming emote - Is reacting? " + emoteIntent.UseOutcomeReactionAnimation + " mov: " + avatarView.GetAnimatorFloat(AnimationHashes.MOVEMENT_BLEND));

            // it's very important to catch any exception here to avoid not consuming the emote intent, so we don't infinitely create props
            try
            {
                // we wait until the avatar finishes moving to trigger the emote,
                // avoid the case where: you stop moving, trigger the emote, the emote gets triggered and next frame it gets cancelled because inertia keeps moving the avatar
                //We also avoid triggering the emote while the character is jumping or landing, as the landing animation breaks the emote flow if they have props
                if (avatarView.IsAnimatorInTag(AnimationHashes.JUMPING_TAG) ||
                    (avatarView.GetAnimatorFloat(AnimationHashes.MOVEMENT_BLEND) > 0.1f && !emoteIntent.UseOutcomeReactionAnimation)) // Note: When playing the outcome animation of an avatar that is reacting, there is no movement blending
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

                    bool isPlayingDifferentEmote = emoteComponent.EmoteUrn.Shorten() != emoteIntent.EmoteId.Shorten();

                    // Previous social emote interaction has to be stopped before starting a new one
                    // When the avatar is already playing a social emote (outcome phase) and then it plays the same one (start phase) it cancels the interaction
                    // Playing a different emote cancels the interaction
                    // If the emote is the same (excepting previous rules) it may be a loop animation and must NOT cancel the interaction
                    if(emoteComponent.Metadata != null)
                        ReportHub.LogError(ReportCategory.EMOTE_DEBUG, "is social: " + emoteComponent.Metadata.IsSocialEmote + " is playing: " + emoteComponent.IsPlayingEmote + " urn1: " + emoteComponent.EmoteUrn.Shorten() + " urn2: " + emoteIntent.EmoteId.Shorten());

                    if (emoteComponent.Metadata != null &&
                        emoteComponent.Metadata.IsSocialEmote &&
                        emoteComponent.IsPlayingEmote &&
                        (isPlayingDifferentEmote || (emoteComponent.IsPlayingSocialEmoteOutcome && !emoteIntent.UseSocialEmoteOutcomeAnimation))) // It's a different emote OR it was playing the outcome phase and now it wants to play the start phase of the same emote interaction (triggered the same social emote again while the previous interaction didn't finish yet, it cancels it)
                    {
                        ReportHub.LogError(ReportCategory.EMOTE_DEBUG, "DIFFERENT PHASE? " + emoteIntent.WalletAddress + " " + (emoteComponent.IsPlayingSocialEmoteOutcome && !emoteIntent.UseSocialEmoteOutcomeAnimation));

                        if(emoteComponent.IsPlayingSocialEmoteOutcome)
                            StopOtherParticipant(entity, ref emoteComponent, emoteIntent.WalletAddress);

                        SocialEmoteInteractionsManager.Instance.StopInteraction(emoteComponent.SocialEmoteInitiatorWalletAddress);
                    }

                    // Playing a social emote for a different interaction, it could happen if the initiator plays the same start animation
                    if (emoteComponent.Metadata != null &&
                        emoteComponent.Metadata.IsSocialEmote &&
                        emoteComponent.IsPlayingEmote &&
                        emoteComponent.SocialEmoteInteractionId != emoteIntent.SocialEmoteInteractionId)
                    {
                        SocialEmoteInteractionsManager.Instance.StopInteraction(emoteComponent.SocialEmoteInitiatorWalletAddress);
                    }

                    // Existing emoteComponent is overwritten with new emote info
                    emoteComponent.Reset();
                    emoteComponent.EmoteUrn = emoteId;
                    emoteComponent.Metadata = (EmoteDTO.EmoteMetadataDto)emote.DTO.Metadata;
                    StreamableLoadingResult<AudioClipData>? audioAssetResult = emote.AudioAssetResults[bodyShape];
                    AudioClip? audioClip = audioAssetResult?.Asset;

                    emoteComponent.IsPlayingSocialEmoteOutcome = emoteIntent.UseSocialEmoteOutcomeAnimation;
                    emoteComponent.CurrentSocialEmoteOutcome = emoteIntent.SocialEmoteOutcomeIndex;
                    emoteComponent.IsReactingToSocialEmote = emoteIntent.UseOutcomeReactionAnimation;
                    emoteComponent.SocialEmoteInitiatorWalletAddress = emoteIntent.SocialEmoteInitiatorWalletAddress;
                    emoteComponent.SocialEmoteInteractionId = emoteIntent.SocialEmoteInteractionId;
                    emoteComponent.TargetAvatarWalletAddress = emoteIntent.TargetAvatarWalletAddress;

                    bool isLoopingSameEmote = emote.IsLooping() && emoteComponent.IsPlayingEmote && !isPlayingDifferentEmote;

                    if (emoteComponent.Metadata.IsSocialEmote && emoteIntent.TriggerSource != TriggerSource.PREVIEW)
                    {
                        if (emoteComponent.IsPlayingSocialEmoteOutcome)
                        {
                            if (SocialEmoteInteractionsManager.Instance.InteractionExists(emoteIntent.SocialEmoteInitiatorWalletAddress)) // When the outcome is a loop, it may receive an emote intent when the interaction has just finished locally
                            {
                                if (emoteComponent.IsReactingToSocialEmote)
                                {
                                    SocialEmoteInteractionsManager.Instance.AddParticipantToInteraction(emoteIntent.WalletAddress, entity, emoteComponent.CurrentSocialEmoteOutcome, emoteIntent.SocialEmoteInitiatorWalletAddress);
                                }

                                // TODO: This IF should never be true
                                if (emoteComponent.CurrentSocialEmoteOutcome < emote.SocialEmoteOutcomeAudioAssetResults.Count)
                                {
                                    ReportHub.LogError(ReportCategory.EMOTE_DEBUG, "AUDIO for outcome " + emoteComponent.CurrentSocialEmoteOutcome);

                                    audioClip = emote.SocialEmoteOutcomeAudioAssetResults[emoteComponent.CurrentSocialEmoteOutcome].Asset;
                                }
                            }
                        }
                        else // Starting interaction
                        {
                            SocialEmoteInteractionsManager.Instance.StartInteraction(emoteIntent.WalletAddress, entity, emote, characterTransform.Transform, emoteComponent.SocialEmoteInteractionId, emoteIntent.TargetAvatarWalletAddress);
                            emoteComponent.SocialEmoteInitiatorWalletAddress = emoteIntent.WalletAddress;

                            if (!isLoopingSameEmote && emoteIntent.TargetAvatarWalletAddress == identityCache.Identity!.Address.OriginalFormat)
                            {
                                ephemeralNotificationsController.AddNotificationAsync(DIRECTED_SOCIAL_EMOTE_EPHEMERAL_NOTIFICATION_PREFAB_NAME, emoteIntent.WalletAddress, new string[]{ emote.GetName() }).Forget();
                            }

//TODO: The initiator has to look at the receiver
                        }
                    }
                    else if (!emoteComponent.Metadata.IsSocialEmote && !isLoopingSameEmote && emoteIntent.TargetAvatarWalletAddress == identityCache.Identity!.Address.OriginalFormat)
                    {
                        ephemeralNotificationsController.AddNotificationAsync(DIRECTED_EMOTE_EPHEMERAL_NOTIFICATION_PREFAB_NAME, emoteIntent.WalletAddress, new string[]{ emote.GetName() }).Forget();
                    }

                    ReportHub.LogError(ReportCategory.EMOTE_DEBUG, "PLAY USER: " + emoteIntent.WalletAddress);

                    if (!emotePlayer.Play(mainAsset, audioClip, emote.IsLooping(), emoteIntent.Spatial, in avatarView, ref emoteComponent))
                        ReportHub.LogError(ReportCategory.EMOTE, $"Emote name:{emoteId} cant be played.");

                    if (emoteComponent.Metadata.IsSocialEmote && emoteIntent.UseOutcomeReactionAnimation)
                    {
                        ReportHub.LogError(ReportCategory.EMOTE_DEBUG, "Added Animate");
                        PrepareToAdjustReceiverBeforeOutcomeAnimation(emoteIntent.SocialEmoteInitiatorWalletAddress);
                        // The rotation of the avatar has to coincide with initiator avatar's when the emote starts, which occurs at least 1 frame later
                        World.Add(entity, new RotateReceiverAvatarToCoincideWithInitiatorAvatarIntent(SocialEmoteInteractionsManager.Instance.GetInteractionState(emoteIntent.WalletAddress)!.InitiatorEntity));
                    }

                    ReportHub.LogError(ReportCategory.EMOTE_DEBUG, "AFTER PLAY USER: hash " + emoteComponent.GetHashCode() + " " + emoteIntent.WalletAddress + " " + emoteComponent.EmoteUrn + " " + emoteComponent.Metadata?.name ?? string.Empty);

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
// TODO
        private struct RotateReceiverAvatarToCoincideWithInitiatorAvatarIntent
        {
            public readonly Entity InitiatorEntity;

            public RotateReceiverAvatarToCoincideWithInitiatorAvatarIntent(Entity initiatorEntity)
            {
                InitiatorEntity = initiatorEntity;
            }
        }

        [Query]
        [All(typeof(RotateReceiverAvatarToCoincideWithInitiatorAvatarIntent))]
        private void RotateReceiverAvatarToCoincideWithInitiatorAvatar(Entity entity, ref IAvatarView avatarView, RotateReceiverAvatarToCoincideWithInitiatorAvatarIntent animationInfo)
        {
            // It waits for the Emote state to execute
            if (avatarView.AvatarAnimator.GetCurrentAnimatorStateInfo(0).tagHash != AnimationHashes.EMOTE)
                return;

            ReportHub.LogError(ReportCategory.EMOTE_DEBUG, "Rotation of reacting avatar as initiator");

            // Makes sure the avatar of the initiator is looking at the receiver's
            IAvatarView initiatorAvatar = World.Get<IAvatarView>(animationInfo.InitiatorEntity);
            initiatorAvatar.GetTransform().forward = (avatarView.GetTransform().position - initiatorAvatar.GetTransform().position).normalized;

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
                && (prevTag != AnimationHashes.EMOTE_LOOP || currentTag != AnimationHashes.EMOTE)) return;

            ReportHub.Log(ReportCategory.EMOTE_DEBUG, "Looping " + profile.UserId + " " + prevTag + " hash " + emoteComponent.GetHashCode());

            if (emoteComponent.EmoteUrn.IsNullOrEmpty())
            {
                ReportHub.LogError(ReportCategory.EMOTE_DEBUG, "URN null");
                return;
            }

            messageBus.Send(emoteComponent.EmoteUrn, true, emoteComponent.IsPlayingSocialEmoteOutcome, emoteComponent.CurrentSocialEmoteOutcome, emoteComponent.IsReactingToSocialEmote, emoteComponent.SocialEmoteInitiatorWalletAddress, emoteComponent.TargetAvatarWalletAddress, false, emoteComponent.SocialEmoteInteractionId);
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
