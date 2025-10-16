using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using DCL.AvatarRendering.AvatarShape.UnityInterface;
using DCL.Character.Components;
using DCL.CharacterMotion.Components;
using DCL.CharacterMotion.Systems;
using DCL.Diagnostics;
using DCL.Profiles;
using ECS.Abstract;
using UnityEngine;

namespace DCL.AvatarRendering.Emotes.SocialEmotes
{
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [UpdateAfter(typeof(RotateCharacterSystem))]
    [LogCategory(ReportCategory.EMOTE)]
    public partial class SocialEmoteInteractionSystem : BaseUnityLoopSystem
    {
        public SocialEmoteInteractionSystem(World world) : base(world)
        {
        }

        protected override void Update(float t)
        {
            MoveToSocialEmoteInteractionInitiatorTransformQuery(World);
            PlayInitiatorOutcomeAnimationQuery(World);
            MoveReceiverWhileOutcomeAnimationQuery(World);
            StopAnimationWhenInteractionFinishesQuery(World);
        }

        /// <summary>
        /// Moves the avatar of the player to the position and rotation of the avatar that initiated the social emote interaction.
        /// The avatar in the other clients will move due to the interpolation systems in other place.
        /// </summary>
        [Query]
        [All(typeof(MoveToInitiatorIntent))]
        private void MoveToSocialEmoteInteractionInitiatorTransform(in Profile profile, in CharacterController characterController, in CharacterRigidTransform characterRigidTransform)
        {
            SocialEmoteInteractionsManager.ISocialEmoteInteractionReadOnly? interaction = SocialEmoteInteractionsManager.Instance.GetInteractionState(profile.UserId);

            if (interaction is { AreInteracting: true })
            {
                characterRigidTransform.LookDirection = interaction.InitiatorRotation * Vector3.forward;

                // It has to be disabled, otherwise position will be overriden
                characterController.enabled = false;
                characterController.transform.position = interaction.InitiatorPosition;
                characterController.transform.rotation = interaction.InitiatorRotation;
                characterController.enabled = true;
            }
        }

        /// <summary>
        /// Plays the animation of the avatar of the initiator once both avatars interact.
        /// </summary>
        [Query]
        [All(typeof(IAvatarView))]
        [None(typeof(CharacterEmoteIntent))]
        private void PlayInitiatorOutcomeAnimation(Entity entity, Profile profile, CharacterEmoteComponent emoteComponent)
        {
            SocialEmoteInteractionsManager.ISocialEmoteInteractionReadOnly? socialEmoteInteraction = SocialEmoteInteractionsManager.Instance.GetInteractionState(profile.UserId);

            if (socialEmoteInteraction is { AreInteracting: true } &&
                socialEmoteInteraction.InitiatorWalletAddress == profile.UserId && !emoteComponent.HasOutcomeAnimationStarted)
            {
                ReportHub.LogError(ReportCategory.EMOTE_DEBUG, "EMOTEINTENT " + profile.UserId);

                World.Add(entity, new CharacterEmoteIntent()
                {
                    EmoteId = socialEmoteInteraction.Emote.DTO.Metadata.id!,
                    TriggerSource = TriggerSource.SELF,
                    Spatial = true,
                    WalletAddress = profile.UserId,
                    SocialEmoteOutcomeIndex = socialEmoteInteraction.OutcomeIndex,
                    UseOutcomeReactionAnimation = false,
                    SocialEmoteInitiatorWalletAddress = profile.UserId,
                    UseSocialEmoteOutcomeAnimation = true
                });
            }
        }

        /// <summary>
        /// Moves the avatar of the receiver of the interaction to the position and rotation of the avatar of the initiator.
        /// </summary>
        [Query]
        [All(typeof(IAvatarView))]
        [None(typeof(CharacterEmoteIntent), typeof(MoveToInitiatorIntent))]
        private void MoveReceiverWhileOutcomeAnimation(Entity entity, Profile profile, CharacterTransform transform)
        {
            SocialEmoteInteractionsManager.ISocialEmoteInteractionReadOnly? socialEmoteInteraction = SocialEmoteInteractionsManager.Instance.GetInteractionState(profile.UserId);

            if (socialEmoteInteraction is { AreInteracting: true } &&
                socialEmoteInteraction.ReceiverWalletAddress == profile.UserId)
            {
                MoveToInitiatorIntent newIntent = new MoveToInitiatorIntent()
                {
                    OriginalPosition = transform.Position,
                    OriginalRotation = transform.Rotation
                };
                World.Add(entity, newIntent);
            }
        }

        /// <summary>
        /// Cancels the emote animation when the interaction has finished (it could be cancelled by any of the participants).
        /// </summary>
        [Query]
        private void StopAnimationWhenInteractionFinishes(Profile profile, ref CharacterEmoteComponent emoteComponent)
        {
            if (emoteComponent.IsPlayingEmote && emoteComponent.Metadata is { IsSocialEmote: true })
            {
                SocialEmoteInteractionsManager.ISocialEmoteInteractionReadOnly? interaction = SocialEmoteInteractionsManager.Instance.GetInteractionState(profile.UserId);

                if (interaction == null)
                {
                    ReportHub.LogError(ReportCategory.EMOTE_DEBUG, "STOP no interaction " + profile.UserId);
                    emoteComponent.StopEmote = true;
                }

            }
        }
    }
}
