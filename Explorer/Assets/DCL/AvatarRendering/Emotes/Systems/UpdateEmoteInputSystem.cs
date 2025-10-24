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
            }
        }

        [Query]
        [All(typeof(PlayerComponent))]
        private void TriggerEmoteBySlotIntent(in Entity entity, ref TriggerEmoteBySlotIntent intent)
        {
            emotesBus.OnQuickActionEmotePlayed();
            triggeredEmoteSlotIndex = intent.Slot;
            triggeredEmoteTargetWalletAddress = intent.TargetAvatrWalletAddress;
            World.Remove<TriggerEmoteBySlotIntent>(entity);
        }

        [Query]
        [All(typeof(PlayerComponent))]
        private void TriggerEmoteReactingToSocialEmoteIntent(in Entity entity, ref TriggerEmoteReactingToSocialEmoteIntent intent)
        {
            triggeredEmoteUrn = intent.TriggeredEmoteUrn;
            socialEmoteOutcomeIndexForTrigger = intent.SocialEmoteOutcomeIndexForTrigger;
            socialEmoteInitiatorWalletAddressForTrigger = intent.SocialEmoteInitiatorWalletAddressForTrigger;

            World.Remove<TriggerEmoteReactingToSocialEmoteIntent>(entity);
        }

        [Query]
        [All(typeof(PlayerComponent))]
        [None(typeof(CharacterEmoteIntent))]
        private void TriggerEmote([Data] int emoteIndex, [Data] string emoteUrn, in Entity entity, in Profile profile, in InputModifierComponent inputModifier, in AvatarShapeComponent avatarShapeComponent)
        {
            if (!string.IsNullOrEmpty(emoteUrn))
            {
                ReportHub.LogError(ReportCategory.EMOTE_DEBUG, "<color=red>--------TRIGGER EMOTE----------</color>");

                // It's a reaction to a social emote
                SendEmoteMessage(emoteUrn, profile.UserId, entity, socialEmoteOutcomeIndexForTrigger, true, true, socialEmoteInitiatorWalletAddressForTrigger);
            }
            else
            {
                if(inputModifier.DisableEmote || !avatarShapeComponent.IsVisible)
                    return;

                IReadOnlyList<URN> emotes = profile.Avatar.Emotes;
                if (emoteIndex < 0 || emoteIndex >= emotes.Count)
                    return;

                ReportHub.LogError(ReportCategory.EMOTE_DEBUG, "<color=red>--------TRIGGER EMOTE----------</color>");

                URN emoteId = emotes[emoteIndex];

                string walletAddress = profile.UserId;
                SendEmoteMessage(emoteId, walletAddress, entity, socialEmoteInitiatorWalletAddress: walletAddress, targetAvatarWalletAddress: triggeredEmoteTargetWalletAddress);
            }
        }

        private void SendEmoteMessage(URN emoteId,
                                      string walletAddress,
                                      Entity entity,
                                      int socialEmoteOutcomeIndex = -1,
                                      bool useOutcomeReactionAnimation = false,
                                      bool useSocialEmoteOutcomeAnimation = false,
                                      string socialEmoteInitiatorWalletAddress = "",
                                      string targetAvatarWalletAddress = "")
        {
            if (emoteId.IsNullOrEmpty()) return;

            var newEmoteIntent = new CharacterEmoteIntent
            {
                EmoteId = emoteId,
                Spatial = true,
                TriggerSource = TriggerSource.SELF,
                WalletAddress = walletAddress,
                SocialEmoteOutcomeIndex = socialEmoteOutcomeIndex,
                UseOutcomeReactionAnimation = useOutcomeReactionAnimation,
                UseSocialEmoteOutcomeAnimation = useSocialEmoteOutcomeAnimation,
                SocialEmoteInitiatorWalletAddress = socialEmoteInitiatorWalletAddress,
                TargetAvatarWalletAddress = targetAvatarWalletAddress
            };
            ref var emoteIntent = ref World.AddOrGet(entity, newEmoteIntent);
            emoteIntent = newEmoteIntent;

            messageBus.Send(emoteId, false, emoteIntent.UseSocialEmoteOutcomeAnimation, emoteIntent.SocialEmoteOutcomeIndex, emoteIntent.UseOutcomeReactionAnimation, emoteIntent.SocialEmoteInitiatorWalletAddress, emoteIntent.TargetAvatarWalletAddress);
        }
    }
}
