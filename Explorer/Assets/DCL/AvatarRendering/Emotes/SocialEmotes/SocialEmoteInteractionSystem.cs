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
    public struct MoveToInitiatorIntent
    {
    }


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
            PlayOutcomeAnimationTransformQuery(World);
        }

        [Query]
        [All(typeof(MoveToInitiatorIntent))]
        private void MoveToSocialEmoteInteractionInitiatorTransform(in Profile profile, in CharacterController characterController, in CharacterRigidTransform characterRigidTransform)
        {
            SocialEmoteInteractionsManager.SocialEmoteInteractionReadOnly? interaction = SocialEmoteInteractionsManager.Instance.GetInteractionState(profile.UserId);

            if (interaction.HasValue && interaction.Value.AreInteracting)
            {
                characterRigidTransform.LookDirection = interaction.Value.InitiatorRotation * Vector3.forward;

                // It has to be disabled, otherwise position will be overriden
                characterController.enabled = false;
                characterController.transform.position = interaction.Value.InitiatorPosition;
                characterController.transform.rotation = interaction.Value.InitiatorRotation;
                characterController.enabled = true;
            }
        }

        [Query]
        [All(typeof(IAvatarView))]
        [None(typeof(CharacterEmoteIntent))]
        private void PlayOutcomeAnimationTransform(Entity entity, Profile profile)
        {
            SocialEmoteInteractionsManager.SocialEmoteInteractionReadOnly? socialEmoteInteraction = SocialEmoteInteractionsManager.Instance.GetInteractionState(profile.UserId);

            if (socialEmoteInteraction.HasValue &&
                socialEmoteInteraction.Value.AreInteracting &&
                socialEmoteInteraction.Value.InitiatorWalletAddress == profile.UserId)
            {
                World.Add(entity, new CharacterEmoteIntent()
                {
                    EmoteId = socialEmoteInteraction.Value.Emote.DTO.Metadata.id!,
                    TriggerSource = TriggerSource.SELF,
                    Spatial = true,
                    WalletAddress = profile.UserId,
                    SocialEmoteOutcomeIndex = socialEmoteInteraction.Value.OutcomeIndex,
                    UseOutcomeReactionAnimation = false,
                    SocialEmoteInitiatorWalletAddress = profile.UserId,
                    UseSocialEmoteOutcomeAnimation = true
                });
            }
        }
    }
}
