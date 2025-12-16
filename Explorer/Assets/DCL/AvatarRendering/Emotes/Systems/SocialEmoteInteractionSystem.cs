using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using DCL.AvatarRendering.AvatarShape.UnityInterface;
using DCL.Character.CharacterMotion.Components;
using DCL.Character.Components;
using DCL.CharacterMotion.Components;
using DCL.CharacterMotion.Systems;
using DCL.Diagnostics;
using DCL.Multiplayer.Emotes;
using DCL.Profiles;
using DCL.SocialEmotes;
using DCL.Utilities;
using ECS.Abstract;
using System;
using UnityEngine;
using Utility.Animations;

namespace DCL.AvatarRendering.Emotes.SocialEmotes
{
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [UpdateAfter(typeof(RotateCharacterSystem))]
    [LogCategory(ReportCategory.SOCIAL_EMOTE)]
    public partial class SocialEmoteInteractionSystem : BaseUnityLoopSystem
    {
        private readonly IEmotesMessageBus messageBus;
        private readonly SocialEmotesSettings socialEmotesSettings;
        private readonly Entity playerEntity;

        public SocialEmoteInteractionSystem(World world, IEmotesMessageBus messageBus, SocialEmotesSettings socialEmotesSettings, Entity playerEntity) : base(world)
        {
            this.messageBus = messageBus;
            this.socialEmotesSettings = socialEmotesSettings;
            this.playerEntity = playerEntity;
        }

        protected override void Update(float t)
        {
            PlayInitiatorOutcomeAnimationQuery(World);
            InterpolateAvatarToOutcomeStartPoseQuery(World);
            WalkToInitiatorPositionBeforePlayingOutcomeAnimationQuery(World);
            ForceAvatarToLookAtPositionQuery(World);
            InterpolateCameraTargetTowardsNewParentQuery(World);

            CharacterEmoteComponent playerEmoteComponent = World.Get<CharacterEmoteComponent>(playerEntity);
            InitiatorLooksAtSocialEmoteTargetQuery(World, playerEmoteComponent.SocialEmote.TargetAvatarWalletAddress, playerEmoteComponent.IsPlayingEmote);
        }

        /// <summary>
        /// Plays the animation of the avatar of the initiator once both avatars interact.
        /// </summary>
        [Query]
        [All(typeof(IAvatarView))]
        [None(typeof(CharacterEmoteIntent))]
        private void PlayInitiatorOutcomeAnimation(Entity entity, Profile profile, ref CharacterEmoteComponent emoteComponent)
        {
            SocialEmoteInteractionsManager.ISocialEmoteInteractionReadOnly? socialEmoteInteraction = SocialEmoteInteractionsManager.Instance.GetInteractionState(profile.UserId);

            if (socialEmoteInteraction is { AreInteracting: true } &&
                socialEmoteInteraction.InitiatorWalletAddress == profile.UserId && !emoteComponent.SocialEmote.HasOutcomeAnimationStarted)
            {
                ReportHub.Log(ReportCategory.SOCIAL_EMOTE, "PlayInitiatorOutcomeAnimation() CharacterEmoteIntent Initiator outcome animation " + profile.UserId);

                World.Add(entity, new CharacterEmoteIntent(
                    socialEmoteInteraction.Emote.DTO.Metadata.id!,
                    triggerSource : TriggerSource.SELF,
                    spatial : true,
                    walletAddress : profile.UserId,
                    outcomeIndex : socialEmoteInteraction.OutcomeIndex,
                    useOutcomeReactionAnimation : false,
                    initiatorWalletAddress : profile.UserId,
                    useOutcomeAnimation : true,
                    interactionId : socialEmoteInteraction.Id
                ));
            }
        }

        /// <summary>
        /// Moves and rotates the avatar to the position and rotation it will have when animation begins.
        /// </summary>
        [Query]
        [All(typeof(InterpolateToOutcomeStartPoseIntent))]
        private void InterpolateAvatarToOutcomeStartPose(Entity entity, IAvatarView avatarView, InterpolateToOutcomeStartPoseIntent moveIntent)
        {
            if (moveIntent.HasBeenCancelled)
            {
                ReportHub.Log(ReportCategory.SOCIAL_EMOTE, $"InterpolateAvatarToOutcomeStartPose() " + ((AvatarBase)avatarView).name);
                World.Remove<InterpolateToOutcomeStartPoseIntent>(entity);
                return;
            }

            float interpolation = (UnityEngine.Time.time - moveIntent.MovementStartTime) / socialEmotesSettings.OutcomeStartInterpolationDuration;

            ReportHub.Log(ReportCategory.SOCIAL_EMOTE, $"InterpolateAvatarToOutcomeStartPose() <color=#FF9933>INTERPOLATION: {interpolation.ToString("F6")} Wallet: {((AvatarBase)avatarView).name}</color>");

            // Since the outcome emote has already started to play, the avatar is moving its position, but we need to create the illusion of the avatar not moving at all
            Vector3 currentHipToOriginalPosition = moveIntent.OriginalAvatarPosition - ((AvatarBase)avatarView).HipAnchorPoint.position;
            Vector3 originalPositionWithCurrentOffset = avatarView.GetTransform().position + new Vector3(currentHipToOriginalPosition.x, 0.0f, currentHipToOriginalPosition.z);
            originalPositionWithCurrentOffset.y = moveIntent.InitiatorWorldPosition.y; // In order to avoid the avatar from leaning, due to the other avatar being at a different height, we move the reacting avatar to the initiator's height
            avatarView.GetTransform().position = Vector3.Lerp(originalPositionWithCurrentOffset, moveIntent.InitiatorWorldPosition, interpolation);

            Debug.DrawRay(avatarView.GetTransform().position, UnityEngine.Vector3.up, Color.yellow, 3.0f);
            GizmoDrawer.Instance.DrawWireSphere(5, moveIntent.OriginalAvatarPosition, 0.2f, Color.red);
            Debug.DrawRay(moveIntent.OriginalAvatarPosition, UnityEngine.Vector3.up, Color.red, 3.0f);
            GizmoDrawer.Instance.DrawWireSphere(0, moveIntent.OriginalAvatarPosition, 0.2f, Color.red);
            Debug.DrawRay(moveIntent.InitiatorWorldPosition, UnityEngine.Vector3.up, Color.cyan, 3.0f);
            GizmoDrawer.Instance.DrawWireSphere(2, moveIntent.InitiatorWorldPosition, 0.2f, Color.cyan);

            if (interpolation >= 1.0f)
            {
                ReportHub.Log(ReportCategory.SOCIAL_EMOTE, $"InterpolateAvatarToOutcomeStartPose() <color=#FF9933>INTERPOLATION FINISHED Wallet: {((AvatarBase)avatarView).name}</color>");
                World.Remove<InterpolateToOutcomeStartPoseIntent>(entity);
            }
        }

        // Executed locally only
        /// <summary>
        /// Makes the receiver's character walk to the position of the initiator.
        /// </summary>
        [Query]
        [All(typeof(MoveBeforePlayingSocialEmoteIntent))]
        [None(typeof(StopEmoteIntent))]
        private void WalkToInitiatorPositionBeforePlayingOutcomeAnimation(in Entity entity, ref CharacterTransform characterTransform, CharacterEmoteComponent emoteComponent,
            ref MovementInputComponent movementInput, in JumpInputComponent jumpInputComponent, ref CharacterAnimationComponent animationComponent,
            ref MoveBeforePlayingSocialEmoteIntent moveIntent)
        {
            // If the avatar is playing an emote, it must cancel that emote before moving to the initiator
            if (emoteComponent.IsPlayingEmote && !emoteComponent.EmoteUrn.IsNullOrEmpty())
            {
                ReportHub.Log(ReportCategory.SOCIAL_EMOTE, "WalkToInitiatorPositionBeforePlayingOutcomeAnimation() Cancelling emote before moving Initiator: " + moveIntent.TriggerEmoteIntent.InitiatorWalletAddress);

                World.Add(entity, new StopEmoteIntent(emoteComponent.EmoteUrn));

                // Sends stop signal to other clients
                messageBus.Send(emoteComponent.EmoteUrn, false, false, -1, false, string.Empty, string.Empty, true, -1);

                return;
            }

            SocialEmoteInteractionsManager.ISocialEmoteInteractionReadOnly? interaction = SocialEmoteInteractionsManager.Instance.GetInteractionState(moveIntent.TriggerEmoteIntent.InitiatorWalletAddress);

            // Checks if the initiator is still available, otherwise cancel the movement
            if (interaction == null || interaction.AreInteracting || interaction.Id != moveIntent.TriggerEmoteIntent.InteractionId)
                moveIntent.HasBeenCancelled = true;

            bool isCloseEnoughToInitiator = Vector3.SqrMagnitude(characterTransform.Position - moveIntent.InitiatorWorldPosition) < socialEmotesSettings.OutcomeStartInterpolationRadius * socialEmotesSettings.OutcomeStartInterpolationRadius;

            if (isCloseEnoughToInitiator ||
                movementInput.HasPlayerPressed ||  // If player presses any movement input, the process is canceled
                jumpInputComponent.IsPressed ||  // If player jumps, the process is canceled
                moveIntent.HasBeenCancelled ||
                UnityEngine.Time.time - moveIntent.StartTime >= socialEmotesSettings.ReactionTimeout) // Timeout, the process is canceled (the avatar got stuck for some reason and did not reach the initiator)
            {
                ReportHub.Log(ReportCategory.SOCIAL_EMOTE, $"WalkToInitiatorPositionBeforePlayingOutcomeAnimation() <color=#FF9933>ARRIVED TO INITIATOR {moveIntent.TriggerEmoteIntent.InitiatorWalletAddress} or CANCELED</color>");

                // The avatar has to stop walking, otherwise it will spend some time to blend
                animationComponent.States.MovementBlendValue = 0.0f;
                movementInput.IgnoreCamera = false;

                if (isCloseEnoughToInitiator && !moveIntent.HasBeenCancelled)
                {
                    ReportHub.Log(ReportCategory.SOCIAL_EMOTE, $"WalkToInitiatorPositionBeforePlayingOutcomeAnimation() <color=#FF9933>ARRIVED TO {moveIntent.TriggerEmoteIntent.InitiatorWalletAddress} -> Playing emote</color>");

                    // Emote playing
                    World.Add(entity, moveIntent.TriggerEmoteIntent);
                }

                World.Remove<MoveBeforePlayingSocialEmoteIntent>(entity);
            }
            else
            {
                movementInput.Kind = socialEmotesSettings.ReceiverJogs ? MovementKind.JOG : MovementKind.WALK;
                movementInput.Axes = Vector2.up;
                // The avatar can walk freely towards the initiator without taking the camera's orientation into consideration
                movementInput.IgnoreCamera = true;

                // Both avatars look at each other
                if (!moveIntent.AreAvatarsLookingAtEachOther)
                {
                    // Rotates the initiator
                    moveIntent.AreAvatarsLookingAtEachOther = true;
                    World.Add(moveIntent.InitiatorEntityId, new LookAtPositionIntention(moveIntent.TriggerEmoteIntent.InitiatorWalletAddress, characterTransform.Position));
                    messageBus.SendLookAtPositionMessage(moveIntent.TriggerEmoteIntent.InitiatorWalletAddress, characterTransform.Position.x, characterTransform.Position.y, characterTransform.Position.z);
                }

                // Rotates the receiver
                World.Add(entity, new PlayerLookAtIntent(moveIntent.InitiatorWorldPosition));
            }
        }

        [Query]
        [All(typeof(IAvatarView))]
        [None(typeof(PlayerComponent))]
        private void InitiatorLooksAtSocialEmoteTarget([Data] string targetWalletAddress, [Data] bool isInitiatorPlayingEmote, in CharacterTransform targetTransform, Profile targetProfile)
        {
            // A remote initiator looks at the target of a directed social emote it sent
            if (!string.IsNullOrEmpty(targetProfile.UserId) && targetWalletAddress == targetProfile.UserId && isInitiatorPlayingEmote)
                World.Add(playerEntity, new PlayerLookAtIntent(targetTransform.Position));
        }

        [Query]
        [All(typeof(LookAtPositionIntention))]
        private void ForceAvatarToLookAtPosition(Entity entity, IAvatarView avatarView, LookAtPositionIntention lookAtPositionIntention)
        {
            Vector3 avatarForward = lookAtPositionIntention.TargetPosition - avatarView.GetTransform().position;
            avatarForward.y = 0.0f;
            avatarView.GetTransform().forward = avatarForward.normalized;
            World.Remove<LookAtPositionIntention>(entity);
        }

        [Query]
        [All(typeof(InterpolateCameraTargetTowardsNewParentIntent))]
        private void InterpolateCameraTargetTowardsNewParent(Entity entity, ref PlayerComponent player, in InterpolateCameraTargetTowardsNewParentIntent interpolateIntent)
        {
            float interpolation = (UnityEngine.Time.time - interpolateIntent.StartTime) / socialEmotesSettings.OutcomeCameraInterpolationDuration;

            try
            {
                player.CameraFocus.parent = null;

                ReportHub.Log(ReportCategory.SOCIAL_EMOTE, $"InterpolateCameraTargetTowardsNewParent() <color=#559933>CAMERA INTERPOLATION: {interpolation.ToString("F6")}</color>");

                Vector3 targetPositionWithHeight = interpolateIntent.Target.position;
                targetPositionWithHeight.y = interpolateIntent.Target.position.y + interpolateIntent.LocalHeight;

                player.CameraFocus.position = Vector3.Lerp(interpolateIntent.StartPosition, targetPositionWithHeight, interpolation);
            }
            catch (Exception e)
            {
                ReportHub.LogException(e, ReportCategory.SOCIAL_EMOTE);
                interpolation = 1.0f;
            }
            finally
            {
                if (interpolation >= 1.0f)
                {
                    ReportHub.Log(ReportCategory.SOCIAL_EMOTE, "InterpolateCameraTargetTowardsNewParent() <color=#559933>CAMERA INTERPOLATION FINISHED</color>");

                    // Re-parents the camera focus object
                    player.CameraFocus.parent = interpolateIntent.Target;
                    player.CameraFocus.localPosition = new Vector3(0.0f, interpolateIntent.LocalHeight, 0.0f);
                    player.CameraFocus.localRotation = Quaternion.identity;

                    World.Remove<InterpolateCameraTargetTowardsNewParentIntent>(entity);
                }
            }
        }
    }
}
