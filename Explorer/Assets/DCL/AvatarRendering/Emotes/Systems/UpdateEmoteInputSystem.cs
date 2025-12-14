using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using CommunicationData.URLHelpers;
using DCL.AvatarRendering.AvatarShape.Components;
using DCL.Character.Components;
using DCL.Diagnostics;
using DCL.Input;
using DCL.Multiplayer.Emotes;
using DCL.Profiles;
using DCL.SDKComponents.InputModifier.Components;
using ECS.Abstract;
using System.Collections.Generic;

namespace DCL.AvatarRendering.Emotes
{
    [LogCategory(ReportCategory.EMOTE)]
    [UpdateInGroup(typeof(InputGroup))]
    public partial class UpdateEmoteInputSystem : BaseUnityLoopSystem
    {
        private readonly EmotesBus emotesBus;
        private readonly IEmotesMessageBus messageBus;

        private int triggeredEmoteSlotIndex = -1;
        private string triggeredEmoteTargetWalletAddress = string.Empty;

        private bool isWheelBlocked;
        private int framesAfterWheelWasClosed;
        private string? triggeredEmoteUrn;
        private int socialEmoteOutcomeIndexForTrigger;
        private string socialEmoteInitiatorWalletAddressForTrigger;
        private int socialEmoteInteractionIdForTrigger;

        private UpdateEmoteInputSystem(World world, IEmotesMessageBus messageBus, EmotesBus emotesBus)
            : base(world)
        {
            this.messageBus = messageBus;
            this.emotesBus = emotesBus;

            GetReportData();
        }

        protected override void Update(float t)
        {
            TriggerEmoteBySlotIntentQuery(World);
            TriggerEmoteReactingToSocialEmoteIntentQuery(World);

            if (triggeredEmoteSlotIndex >= 0 || !string.IsNullOrEmpty(triggeredEmoteUrn))
            {
                TriggerEmoteQuery(World, triggeredEmoteSlotIndex, triggeredEmoteUrn);
                triggeredEmoteSlotIndex = -1;
                triggeredEmoteUrn = null;
                triggeredEmoteTargetWalletAddress = string.Empty;
                socialEmoteOutcomeIndexForTrigger = -1;
                socialEmoteInitiatorWalletAddressForTrigger = string.Empty;
                socialEmoteInteractionIdForTrigger = 0;
            }
        }

        [Query]
        [All(typeof(PlayerComponent))]
        private void TriggerEmoteBySlotIntent(in Entity entity, ref TriggerEmoteBySlotIntent intent)
        {
            emotesBus.OnQuickActionEmotePlayed();
            triggeredEmoteSlotIndex = intent.Slot;
            triggeredEmoteTargetWalletAddress = intent.TargetAvatarWalletAddress;
            World.Remove<TriggerEmoteBySlotIntent>(entity);
        }

        [Query]
        [All(typeof(PlayerComponent))]
        private void TriggerEmoteReactingToSocialEmoteIntent(in Entity entity, ref TriggerEmoteReactingToSocialEmoteIntent intent)
        {
            triggeredEmoteUrn = intent.TriggeredEmoteUrn;
            socialEmoteOutcomeIndexForTrigger = intent.OutcomeIndex;
            socialEmoteInitiatorWalletAddressForTrigger = intent.InitiatorWalletAddress;
            socialEmoteInteractionIdForTrigger = intent.InteractionId;

            World.Remove<TriggerEmoteReactingToSocialEmoteIntent>(entity);
        }

        [Query]
        [All(typeof(PlayerComponent))]
        [None(typeof(CharacterEmoteIntent))]
        private void TriggerEmote([Data] int emoteIndex, [Data] string emoteUrn, in Entity entity, in Profile profile, in InputModifierComponent inputModifier, in AvatarShapeComponent avatarShapeComponent)
        {
            if (!string.IsNullOrEmpty(emoteUrn)) // It's a reaction to a social emote
            {
                ReportHub.Log(ReportCategory.SOCIAL_EMOTE, $"UpdateEmoteInputSystem.TriggerEmote() <color=red>--------TRIGGER EMOTE---------- reaction to a social emote. wallet: {profile.UserId} emoteUrn: {emoteUrn}</color>");

                SendEmoteMessage(emoteUrn, profile.UserId, entity, socialEmoteOutcomeIndexForTrigger, true, true, socialEmoteInitiatorWalletAddressForTrigger, triggeredEmoteTargetWalletAddress, socialEmoteInteractionIdForTrigger);
            }
            else // Normal emotes, or social emote start animation
            {
                if (inputModifier.DisableEmote || !avatarShapeComponent.IsVisible)
                    return;

                var emotes = profile.Avatar.Emotes;
                if (emoteIndex < 0 || emoteIndex >= emotes.Count)
                    return;

                ReportHub.Log(ReportCategory.SOCIAL_EMOTE, $"UpdateEmoteInputSystem.TriggerEmote() <color=red>--------TRIGGER EMOTE---------- Normal emotes, or social emote start animation. wallet: {profile.UserId} emoteUrn: {emoteUrn}</color>");

                var emoteId = emotes[emoteIndex];

                string walletAddress = profile.UserId;
                int interactionId = UnityEngine.Time.frameCount; // Whatever is unique, increasing and positive in this client (used when playing start animation of social emote)
                SendEmoteMessage(emoteId, walletAddress, entity, socialEmoteInitiatorWalletAddress: walletAddress, targetAvatarWalletAddress: triggeredEmoteTargetWalletAddress, socialEmoteInteractionId: interactionId);
            }
        }

        private void SendEmoteMessage(URN emoteId,
            string walletAddress,
            Entity entity,
            int socialEmoteOutcomeIndex = -1,
            bool useOutcomeReactionAnimation = false,
            bool useSocialEmoteOutcomeAnimation = false,
            string socialEmoteInitiatorWalletAddress = "",
            string targetAvatarWalletAddress = "",
            int socialEmoteInteractionId = 0)
        {
            if (emoteId.IsNullOrEmpty())
                return;

            var newEmoteIntent = new CharacterEmoteIntent
            {
                EmoteId = emoteId, Spatial = true, TriggerSource = TriggerSource.SELF, WalletAddress = walletAddress,
                SocialEmote = new CharacterEmoteIntent.SocialEmoteData
                {
                    OutcomeIndex = socialEmoteOutcomeIndex, UseOutcomeReactionAnimation = useOutcomeReactionAnimation, UseOutcomeAnimation = useSocialEmoteOutcomeAnimation, InitiatorWalletAddress = socialEmoteInitiatorWalletAddress,
                    TargetAvatarWalletAddress = targetAvatarWalletAddress, InteractionId = socialEmoteInteractionId
                }
            };
            ref var emoteIntent = ref World.AddOrGet(entity, newEmoteIntent);
            emoteIntent = newEmoteIntent;

            messageBus.Send(emoteId, false, emoteIntent.SocialEmote.UseOutcomeAnimation, emoteIntent.SocialEmote.OutcomeIndex, emoteIntent.SocialEmote.UseOutcomeReactionAnimation, emoteIntent.SocialEmote.InitiatorWalletAddress, emoteIntent.SocialEmote.TargetAvatarWalletAddress, false, emoteIntent.SocialEmote.InteractionId);
        }
    }
}
