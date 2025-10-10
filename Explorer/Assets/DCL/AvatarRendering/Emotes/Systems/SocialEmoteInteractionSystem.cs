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
        public Vector3 OriginalPosition;
        public Quaternion OriginalRotation;
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
            DCL.SocialEmotes.SocialEmoteInteractionsManager.SocialEmoteInteractionReadOnly? interaction = DCL.SocialEmotes.SocialEmoteInteractionsManager.Instance.GetInteractionState(profile.UserId);

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
        [None(typeof(CharacterEmoteIntent), typeof(MoveToInitiatorIntent))]
        private void PlayOutcomeAnimationTransform(Entity entity, Profile profile, CharacterTransform transform, CharacterEmoteComponent emoteComponente)
        {
            DCL.SocialEmotes.SocialEmoteInteractionsManager.SocialEmoteInteractionReadOnly? socialEmoteInteraction = DCL.SocialEmotes.SocialEmoteInteractionsManager.Instance.GetInteractionState(profile.UserId);

            if (socialEmoteInteraction.HasValue &&
                socialEmoteInteraction.Value.AreInteracting)
            {
                if (socialEmoteInteraction.Value.InitiatorWalletAddress == profile.UserId && !emoteComponente.HasOutcomeAnimationStarted)
                {
                    Debug.LogError("EMOTEINTENT " + profile.UserId);

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
                else if(socialEmoteInteraction.Value.ReceiverWalletAddress == profile.UserId)
                {
                    MoveToInitiatorIntent newIntent = new MoveToInitiatorIntent()
                    {
                        OriginalPosition = transform.Position,
                        OriginalRotation = transform.Rotation
                    };
                    World.Add(entity, newIntent);
                }
            }
        }
    }
}
