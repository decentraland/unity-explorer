using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.Character.Components;
using DCL.Diagnostics;
using DCL.EmotesWheel;
using DCL.Input;
using DCL.Multiplayer.Emotes.Interfaces;
using DCL.Profiles;
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
        private readonly Dictionary<string, int> actionNameById = new ();
        private readonly IEmotesMessageBus messageBus;
        private readonly IMVCManager mvcManager;
        private readonly DCLInput.ShortcutsActions shortcuts;
        private readonly DCLInput.EmotesActions emotesActions;

        private int triggeredEmote = -1;
        private bool isWheelBlocked;

        public UpdateEmoteInputSystem(World world, DCLInput dclInput, IEmotesMessageBus messageBus,
            IMVCManager mvcManager) : base(world)
        {
            shortcuts = dclInput.Shortcuts;
            emotesActions = dclInput.Emotes;
            this.messageBus = messageBus;
            this.mvcManager = mvcManager;

            GetReportCategory();

            ListenToSlotsInput(emotesActions.Get());
        }

        public override void Dispose()
        {
            base.Dispose();

            UnregisterSlotsInput(emotesActions.Get());
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

            if (shortcuts.EmoteWheel.WasReleasedThisFrame())
            {
                if (!isWheelBlocked)
                    OpenEmoteWheel();

                isWheelBlocked = false;
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
        private void TriggerEmote([Data] int emoteIndex, in Entity entity, in Profile profile)
        {
            IReadOnlyList<URN> emotes = profile.Avatar.Emotes;
            if (emoteIndex < 0 || emoteIndex >= emotes.Count) return;

            URN emoteId = emotes[emoteIndex];

            if (emoteId.IsNullOrEmpty()) return;

            var newEmoteIntent = new CharacterEmoteIntent { EmoteId = emoteId };
            ref var emoteIntent = ref World.AddOrGet(entity, newEmoteIntent);
            emoteIntent = newEmoteIntent;

            messageBus.Send(emoteId, false, false);
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
                catch (Exception e) { ReportHub.LogException(e, new ReportData(GetReportCategory())); }
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

        private void OpenEmoteWheel() =>
            mvcManager.ShowAsync(EmotesWheelController.IssueCommand()).Forget();
    }
}
