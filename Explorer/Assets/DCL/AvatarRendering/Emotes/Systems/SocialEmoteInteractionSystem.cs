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
using UnityEngine;
using Utility.Animations;

namespace DCL.AvatarRendering.Emotes.SocialEmotes
{
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [UpdateAfter(typeof(RotateCharacterSystem))]
    [LogCategory(ReportCategory.EMOTE)]
    public partial class SocialEmoteInteractionSystem : BaseUnityLoopSystem
    {
        IEmotesMessageBus messageBus;

        public SocialEmoteInteractionSystem(World world, IEmotesMessageBus messageBus) : base(world)
        {
            this.messageBus = messageBus;
        }

        protected override void Update(float t)
        {
            PlayInitiatorOutcomeAnimationQuery(World);
            AdjustReceiverBeforeOutcomeAnimationQuery(World);
            WalkToInitiatorPositionBeforePlayingOutcomeAnimationQuery(World);
            ForceAvatarToLookAtPositionQuery(World);
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
                socialEmoteInteraction.InitiatorWalletAddress == profile.UserId && !emoteComponent.HasOutcomeAnimationStarted)
            {
                ReportHub.LogError(ReportCategory.EMOTE_DEBUG, "CharacterEmoteIntent Initiator outcome animation " + profile.UserId);

                World.Add(entity, new CharacterEmoteIntent()
                {
                    EmoteId = socialEmoteInteraction.Emote.DTO.Metadata.id!,
                    TriggerSource = TriggerSource.SELF,
                    Spatial = true,
                    WalletAddress = profile.UserId,
                    SocialEmoteOutcomeIndex = socialEmoteInteraction.OutcomeIndex,
                    UseOutcomeReactionAnimation = false,
                    SocialEmoteInitiatorWalletAddress = profile.UserId,
                    UseSocialEmoteOutcomeAnimation = true,
                    SocialEmoteInteractionId = socialEmoteInteraction.Id
                });
            }
        }

        /// <summary>
        /// Moves the avatar of the receiver of the interaction to the position and rotation it will have when animation begins.
        /// </summary>
        [Query]
        [All(typeof(MoveToOutcomeStartPositionIntent))]
        private void AdjustReceiverBeforeOutcomeAnimation(Entity entity, IAvatarView avatarView, MoveToOutcomeStartPositionIntent moveIntent)
        {
            if (moveIntent.HasBeenCancelled)
            {
                World.Remove<MoveToOutcomeStartPositionIntent>(entity);
                return;
            }

            const float INTERPOLATION_DURATION = 0.4f;
            float interpolation = (UnityEngine.Time.time - moveIntent.MovementStartTime) / INTERPOLATION_DURATION;

            ReportHub.LogError(ReportCategory.EMOTE_DEBUG, $"<color=#FF9933>INTERPOLATION: {interpolation.ToString("F6")} Emote tag?: {avatarView.AvatarAnimator.GetCurrentAnimatorStateInfo(0).tagHash == AnimationHashes.EMOTE} Speed: {avatarView.AvatarAnimator.GetFloat(AnimationHashes.MOVEMENT_BLEND).ToString("F6")}</color>");

            // Since the outcome emote has already started to play, the avatar is moving its position, but we need to create the illusion of the avatar not moving at all
            Vector3 currentHipToOriginalPosition = moveIntent.OriginalAvatarPosition - ((AvatarBase)avatarView).HipAnchorPoint.position;
            Vector3 originalPositionWithCurrentOffset = avatarView.GetTransform().position + new Vector3(currentHipToOriginalPosition.x, 0.0f, currentHipToOriginalPosition.z);
            avatarView.GetTransform().position = Vector3.Lerp(originalPositionWithCurrentOffset, moveIntent.InitiatorWorldPosition, interpolation);

            Debug.DrawRay(avatarView.GetTransform().position, UnityEngine.Vector3.up, Color.yellow, 3.0f);
            GizmoDrawer.Instance.DrawWireSphere(5, moveIntent.OriginalAvatarPosition, 0.2f, Color.red);
            Debug.DrawRay(moveIntent.OriginalAvatarPosition, UnityEngine.Vector3.up, Color.red, 3.0f);
            GizmoDrawer.Instance.DrawWireSphere(0, moveIntent.OriginalAvatarPosition, 0.2f, Color.red);
            Debug.DrawRay(moveIntent.TargetAvatarPosition, UnityEngine.Vector3.up, Color.green, 3.0f);
            GizmoDrawer.Instance.DrawWireSphere(1, moveIntent.TargetAvatarPosition, 0.2f, Color.green);
            Debug.DrawRay(moveIntent.InitiatorWorldPosition, UnityEngine.Vector3.up, Color.cyan, 3.0f);
            GizmoDrawer.Instance.DrawWireSphere(2, moveIntent.InitiatorWorldPosition, 0.2f, Color.cyan);

            if (interpolation >= 1.0f)
            {
                ReportHub.LogError(ReportCategory.EMOTE_DEBUG, "<color=#FF9933>INTERPOLATION FINISHED</color>");
                World.Remove<MoveToOutcomeStartPositionIntent>(entity);
            }
        }

        // Executed locally only
        /// <summary>
        /// Makes the receiver's character walk to the position of the initiator.
        /// </summary>
        [Query]
        [All(typeof(MoveBeforePlayingSocialEmoteIntent))]
        private void WalkToInitiatorPositionBeforePlayingOutcomeAnimation(in Entity entity, ref CharacterTransform characterTransform,
            ref MovementInputComponent movementInput, in JumpInputComponent jumpInputComponent, ref CharacterAnimationComponent animationComponent,
            ref MoveBeforePlayingSocialEmoteIntent moveIntent)
        {
            bool isCloseEnoughToInitiator = Vector3.SqrMagnitude(characterTransform.Position - moveIntent.InitiatorWorldPosition) < 2.0f;

            if (isCloseEnoughToInitiator ||
                movementInput.HasPlayerPressed ||  // If player presses any movement input, the process is canceled
                jumpInputComponent.IsPressed ||  // If player jumps, the process is canceled
                moveIntent.HasBeenCancelled)
            {
                ReportHub.LogError(ReportCategory.EMOTE_DEBUG, "<color=#FF9933>ARRIVED TO INITIATOR or CANCELED</color>");

                // The avatar has to stop walking, otherwise it will spend some time to blend
                animationComponent.States.MovementBlendValue = 0.0f;
                movementInput.IgnoreCamera = false;

                if (isCloseEnoughToInitiator && !moveIntent.HasBeenCancelled)
                {
                    ReportHub.LogError(ReportCategory.EMOTE_DEBUG, "<color=#FF9933>ARRIVED -> Playing emote</color>");

                    // Emote playing
                    World.Add(entity, moveIntent.TriggerEmoteIntent);
                }

                World.Remove<MoveBeforePlayingSocialEmoteIntent>(entity);
            }
            else
            {
                movementInput.Kind = MovementKind.WALK;
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
        [All(typeof(LookAtPositionIntention))]
        private void ForceAvatarToLookAtPosition(Entity entity, IAvatarView avatarView, LookAtPositionIntention lookAtPositionIntention)
        {
            ReportHub.LogError(ReportCategory.EMOTE_DEBUG, "Forward before: " + avatarView.GetTransform().forward);
            avatarView.GetTransform().forward = (lookAtPositionIntention.TargetPosition - avatarView.GetTransform().position).normalized;
            ReportHub.LogError(ReportCategory.EMOTE_DEBUG, "Forward after: " + avatarView.GetTransform().forward);
            World.Remove<LookAtPositionIntention>(entity);
        }
    }
}
