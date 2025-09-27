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
using MVC;
using System;
using System.Collections.Generic;
using UnityEngine.InputSystem;
using InputAction = UnityEngine.InputSystem.InputAction;

namespace DCL.AvatarRendering.Emotes
{
    [LogCategory(ReportCategory.EMOTE)]
    [UpdateInGroup(typeof(InputGroup))]
    public partial class UpdateEmoteInputSystem : BaseUnityLoopSystem
    {
        private readonly EmotesBus emotesBus;
        private readonly Dictionary<string, int> actionNameById = new ();
        private readonly IEmotesMessageBus messageBus;
        private readonly IMVCManager mvcManager;
        private readonly DCLInput.EmotesActions emotesActions;

        private int triggeredEmote = -1;
        private bool isWheelBlocked;
        private int framesAfterWheelWasClosed;
        private URN socialEmoteUrnForTrigger;
        private int socialEmoteOutcomeIndexForTrigger;
        private bool useOutcomeReactionAnimationForTrigger;
        private bool useSocialEmoteOutcomeAnimationForTrigger;
        private string socialEmoteInitiatorWalletAddressForTrigger;

        private UpdateEmoteInputSystem(World world, IEmotesMessageBus messageBus, EmotesBus emotesBus)
            : base(world)
        {
            emotesActions = DCLInput.Instance.Emotes;
            this.messageBus = messageBus;
            this.emotesBus = emotesBus;
            this.emotesBus.SocialEmoteReactionPlayingRequested += OnEmoteBusSocialEmoteReactionPlayingRequested;

            GetReportData();

            ListenToSlotsInput(emotesActions.Get());
        }

        private void OnEmoteBusSocialEmoteReactionPlayingRequested(string initiatorWalletAddress, IEmote emote, int outcomeIndex)
        {
            socialEmoteUrnForTrigger = emote.Model!.Asset!.id!;
            socialEmoteOutcomeIndexForTrigger = outcomeIndex;
            useOutcomeReactionAnimationForTrigger = true;
            useSocialEmoteOutcomeAnimationForTrigger = true;
            socialEmoteInitiatorWalletAddressForTrigger = initiatorWalletAddress;
        }

        protected override void OnDispose()
        {
            UnregisterSlotsInput(emotesActions.Get());
        }

        private void OnSlotPerformed(InputAction.CallbackContext obj)
        {
            int emoteIndex = actionNameById[obj.action.name];
            triggeredEmote = emoteIndex; // Assigning this variable triggers the emote
            isWheelBlocked = true;
            emotesBus.OnQuickActionEmotePlayed();
        }

        protected override void Update(float t)
        {
            TriggerEmoteBySlotIntentQuery(World);

            if (triggeredEmote >= 0)
            {
                TriggerEmoteQuery(World, triggeredEmote);
                triggeredEmote = -1;
            }
        }

        [Query]
        [All(typeof(PlayerComponent))]
        private void TriggerEmoteBySlotIntent(in Entity entity, ref TriggerEmoteBySlotIntent intent)
        {
            triggeredEmote = intent.Slot;
            World.Remove<TriggerEmoteBySlotIntent>(entity);
        }

        [Query]
        [All(typeof(PlayerComponent))]
        [None(typeof(CharacterEmoteIntent))]
        private void TriggerEmote([Data] int emoteIndex, in Entity entity, in Profile profile, in InputModifierComponent inputModifier, in AvatarShapeComponent avatarShapeComponent)
        {
            if (!socialEmoteUrnForTrigger.IsNullOrEmpty())
            {
                // It's a social emote
                SendEmoteMessage(socialEmoteUrnForTrigger, profile.UserId, entity,  socialEmoteOutcomeIndexForTrigger, useOutcomeReactionAnimationForTrigger, useSocialEmoteOutcomeAnimationForTrigger, socialEmoteInitiatorWalletAddressForTrigger);
                socialEmoteUrnForTrigger = new URN();
            }
            else
            {
                if(inputModifier.DisableEmote || !avatarShapeComponent.IsVisible) return;

                IReadOnlyList<URN> emotes = profile.Avatar.Emotes;
                if (emoteIndex < 0 || emoteIndex >= emotes.Count) return;

                URN emoteId = emotes[emoteIndex];

                string walletAddress = profile.UserId;
                SendEmoteMessage(emoteId, walletAddress, entity, socialEmoteInitiatorWalletAddress: walletAddress);
            }
        }

        private void SendEmoteMessage(URN emoteId,
                                      string walletAddress,
                                      Entity entity,
                                      int socialEmoteOutcomeIndex = -1,
                                      bool useOutcomeReactionAnimation = false,
                                      bool useSocialEmoteOutcomeAnimation = false,
                                      string socialEmoteInitiatorWalletAddress = null)
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
                SocialEmoteInitiatorWalletAddress = socialEmoteInitiatorWalletAddress
            };
            ref var emoteIntent = ref World.AddOrGet(entity, newEmoteIntent);
            emoteIntent = newEmoteIntent;

            messageBus.Send(emoteId, false, emoteIntent.UseSocialEmoteOutcomeAnimation, emoteIntent.SocialEmoteOutcomeIndex, emoteIntent.UseOutcomeReactionAnimation);
        }

        private void ListenToSlotsInput(InputActionMap inputActionMap)
        {
            for (var i = 0; i < Avatar.MAX_EQUIPPED_EMOTES; i++)
            {
                string actionName = GetActionName(i);

                try
                {
                    InputAction inputAction = inputActionMap.FindAction(actionName);
                    inputAction.started += OnSlotPerformed;
                    actionNameById[actionName] = i;
                }
                catch (Exception e) { ReportHub.LogException(e, GetReportData()); }
            }
        }

        private void UnregisterSlotsInput(InputActionMap inputActionMap)
        {
            for (var i = 0; i < Avatar.MAX_EQUIPPED_EMOTES; i++)
            {
                string actionName = GetActionName(i);
                InputAction inputAction = inputActionMap.FindAction(actionName);
                inputAction.started -= OnSlotPerformed;
            }
        }

        private static string GetActionName(int i) =>
            $"Slot {i}";
    }
}
