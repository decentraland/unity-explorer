using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using CommunicationData.URLHelpers;
using DCL.AvatarRendering.AvatarShape.Components;
using DCL.Character.Components;
using DCL.Diagnostics;
using DCL.EmotesWheel;
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
        private const int AFTER_WHEEL_WAS_CLOSED_FRAMES_DELAY = 30;
        private readonly Dictionary<string, int> actionNameById = new ();
        private readonly IEmotesMessageBus messageBus;
        private readonly IMVCManager mvcManager;
        private readonly DCLInput.EmotesActions emotesActions;

        private int triggeredEmote = -1;
        private bool isWheelBlocked;
        private int framesAfterWheelWasClosed;

        private UpdateEmoteInputSystem(World world, DCLInput dclInput, IEmotesMessageBus messageBus,
            IMVCManager mvcManager) : base(world)
        {
            emotesActions = dclInput.Emotes;
            this.messageBus = messageBus;
            this.mvcManager = mvcManager;

            this.mvcManager.OnViewClosed += OnEmoteWheelClosed;

            GetReportData();

            ListenToSlotsInput(emotesActions.Get());
        }

        protected override void OnDispose()
        {
            UnregisterSlotsInput(emotesActions.Get());

            this.mvcManager.OnViewClosed -= OnEmoteWheelClosed;
        }

        private void OnSlotPerformed(InputAction.CallbackContext obj)
        {
            int emoteIndex = actionNameById[obj.action.name];
            triggeredEmote = emoteIndex;
            isWheelBlocked = true;
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
            if(inputModifier.DisableEmote || !avatarShapeComponent.IsVisible) return;

            IReadOnlyList<URN> emotes = profile.Avatar.Emotes;
            if (emoteIndex < 0 || emoteIndex >= emotes.Count) return;

            URN emoteId = emotes[emoteIndex];

            if (emoteId.IsNullOrEmpty()) return;

            var newEmoteIntent = new CharacterEmoteIntent { EmoteId = emoteId, Spatial = true, TriggerSource = TriggerSource.SELF};
            ref var emoteIntent = ref World.AddOrGet(entity, newEmoteIntent);
            emoteIntent = newEmoteIntent;

            messageBus.Send(emoteId, false);
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

        private void OnEmoteWheelClosed(IController obj)
        {
            if (obj is not EmotesWheelController) return;
            framesAfterWheelWasClosed = AFTER_WHEEL_WAS_CLOSED_FRAMES_DELAY;
        }
    }
}
