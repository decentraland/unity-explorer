using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using DCL.AvatarRendering.AvatarShape.UnityInterface;
using DCL.AvatarRendering.Loading.Components;
using DCL.Character.CharacterMotion.Components;
using DCL.Character.Components;
using DCL.CharacterMotion.Components;
using DCL.CharacterMotion.Systems;
using DCL.Diagnostics;
using DCL.Profiles;
using DCL.SocialEmotes;
using DCL.Utilities;
using ECS.Abstract;
using UnityEngine;

namespace DCL.AvatarRendering.Emotes.SocialEmotes
{
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [UpdateAfter(typeof(RotateCharacterSystem))]
    [LogCategory(ReportCategory.EMOTE)]
    public partial class SocialEmoteInteractionSystem : BaseUnityLoopSystem
    {
        private readonly IEmoteStorage emoteStorage;

        public SocialEmoteInteractionSystem(World world, IEmoteStorage emoteStorage) : base(world)
        {
            this.emoteStorage = emoteStorage;
        }

        protected override void Update(float t)
        {
            PlayInitiatorOutcomeAnimationQuery(World);
            AdjustReceiverBeforeOutcomeAnimationQuery(World);
            WalkToInitiatorPositionBeforePlayingOutcomeAnimationQuery(World);
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
        private void AdjustReceiverBeforeOutcomeAnimation(Entity entity, IAvatarView avatarView, MoveToOutcomeStartPositionIntent moveIntent)
        {
            const float INTERPOLATION_DURATION = 1.0f;
            float interpolation = (UnityEngine.Time.time - moveIntent.MovementStartTime) / INTERPOLATION_DURATION;

            ReportHub.LogError(ReportCategory.EMOTE_DEBUG, $"<color=#FF9933>INTERPOLATION: {interpolation.ToString("F6")}</color>");

            avatarView.GetTransform().position = Vector3.Lerp(moveIntent.OriginalAvatarPosition, moveIntent.TargetAvatarPosition, interpolation);
            avatarView.GetTransform().rotation = Quaternion.AngleAxis(180.0f, Vector3.up) * moveIntent.TargetAvatarRotation;

            Debug.DrawRay(moveIntent.OriginalAvatarPosition, UnityEngine.Vector3.up, Color.red, 3.0f);
            GizmoDrawer.Instance.DrawWireSphere(0, moveIntent.OriginalAvatarPosition, 0.2f, Color.red);
            Debug.DrawRay(moveIntent.TargetAvatarPosition, UnityEngine.Vector3.up, Color.green, 3.0f);
            GizmoDrawer.Instance.DrawWireSphere(1, moveIntent.TargetAvatarPosition, 0.2f, Color.green);
            Debug.DrawRay(moveIntent.InitiatorWorldPosition, UnityEngine.Vector3.up, Color.cyan, 3.0f);
            GizmoDrawer.Instance.DrawWireSphere(2, moveIntent.InitiatorWorldPosition, 0.2f, Color.cyan);

            if (interpolation >= 1.0f)
            {
                ReportHub.LogError(ReportCategory.EMOTE_DEBUG, "<color=#FF9933>INTERPOLATION FINISHED</color>");

                World.Add(entity, moveIntent.TriggerEmoteIntent);

                World.Remove<MoveToOutcomeStartPositionIntent>(entity);
            }
        }

// TODO: Rename to a more generic thing
        [Query]
        private void WalkToInitiatorPositionBeforePlayingOutcomeAnimation(in Entity entity, ref CharacterTransform characterTransform, ref MovementInputComponent movementInput, in JumpInputComponent jumpInputComponent, AvatarBase avatarBase,
            MoveBeforePlayingSocialEmoteIntent moveIntent)
        {
            bool isCloseEnoughToInitiator = Vector3.SqrMagnitude(characterTransform.Position - moveIntent.InitiatorWorldPosition) < 2.0f;

            if (isCloseEnoughToInitiator ||
                movementInput.HasPlayerPressed ||  // If player presses any movement input, the process is canceled
                jumpInputComponent.IsPressed)  // If player jumps, the process is canceled
            {
                ReportHub.LogError(ReportCategory.EMOTE_DEBUG, "<color=#FF9933>ARRIVED TO INITIATOR or CANCELED</color>");
                movementInput.IgnoreCamera = false;
                World.Remove<MoveBeforePlayingSocialEmoteIntent>(entity);

                if (isCloseEnoughToInitiator)
                {
                    // Since the avatar is reacting, the emote is already available
                    IEmote emote;
                    emoteStorage.TryGetElement(moveIntent.TriggerEmoteIntent.TriggeredEmoteUrn!, out emote);

                    Vector3 targetAvatarHipRelativePosition = Vector3.zero;

                    if(emote.AssetResults[BodyShape.MALE]!.Value.Asset!.SocialEmoteOutcomeAnimationStartPoses != null)
                        targetAvatarHipRelativePosition = emote.AssetResults[BodyShape.MALE]!.Value.Asset!.SocialEmoteOutcomeAnimationStartPoses[moveIntent.TriggerEmoteIntent.OutcomeIndex].Position;

                    ReportHub.LogError(ReportCategory.EMOTE_DEBUG, "<color=#FF9933>target hip: " + targetAvatarHipRelativePosition.ToString("F6") + "</color>");

                    Vector3 originalAvatarPosition = avatarBase.GetTransform().position;
                    Vector3 originalHipRelativePosition = Vector3.Scale(avatarBase.HipAnchorPoint.localPosition, avatarBase.HipAnchorPoint.parent.localScale);
                    Vector3 targetAvatarPosition = moveIntent.InitiatorWorldPosition
                                                   + World.Get<IAvatarView>(moveIntent.InitiatorEntityId).GetTransform().rotation * new Vector3(targetAvatarHipRelativePosition.x, 0.0f, targetAvatarHipRelativePosition.z)
                                                   // Small adjustment to make current position of the hips in the current animation with the future position of the hips
                                                   - avatarBase.GetTransform().rotation * new Vector3(originalHipRelativePosition.x, 0.0f, originalHipRelativePosition.y);

                    ReportHub.LogError(ReportCategory.EMOTE_DEBUG, $"<color=#FF9933>Movement: {originalAvatarPosition.ToString("F3")} -> {targetAvatarPosition.ToString("F3")}</color>");

                    World.Add(entity, new MoveToOutcomeStartPositionIntent(
                        originalAvatarPosition,
                        avatarBase.GetTransform().rotation,
                        targetAvatarPosition,
                        World.Get<IAvatarView>(moveIntent.InitiatorEntityId).GetTransform().rotation,
                        moveIntent.TriggerEmoteIntent,
                        moveIntent.InitiatorWorldPosition));
                }
            }
            else
            {
                movementInput.Kind = MovementKind.RUN;
                movementInput.Axes = Vector2.up;
                movementInput.IgnoreCamera = true;
                // Both avatars look at each other
                World.Add(entity, new PlayerLookAtIntent(moveIntent.InitiatorWorldPosition));
                World.TryGetRef<IAvatarView>(moveIntent.InitiatorEntityId, out bool _).GetTransform().forward = (characterTransform.Position - moveIntent.InitiatorWorldPosition).normalized;
                //World.Add(moveIntent.InitiatorEntityId, new PlayerLookAtIntent(characterTransform.Transform.position));
            }
        }
    }
}
