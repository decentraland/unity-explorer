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

        private DCLInput.EmotesActions emotesActions;
        private int triggeredEmote = -1;

        public UpdateEmoteInputSystem(World world, DCLInput dclInput, IEmotesMessageBus messageBus,
            IMVCManager mvcManager) : base(world)
        {
            shortcuts = dclInput.Shortcuts;
            emotesActions = dclInput.Emotes;
            this.messageBus = messageBus;
            this.mvcManager = mvcManager;
            GetReportCategory();
            InputActionMap inputActionMap = emotesActions.Get();

            for (var i = 0; i < 10; i++)
            {
                string actionName = GetActionName(i);

                try
                {
                    InputAction inputAction = inputActionMap.FindAction(actionName);
                    inputAction.started += OnSlotPerformed;
                    actionNameById.Add(actionName, i);
                }
                catch (Exception) { ReportHub.LogError(GetReportCategory(), "Input action " + actionName + " does not exist"); }
            }
        }

        private static string GetActionName(int i) =>
            $"Slot {i}";

        public override void Dispose()
        {
            base.Dispose();
            InputActionMap inputActionMap = emotesActions.Get();
            for (int i = 0; i < 8; i++)
            {
                var actionName = GetActionName(i+1);
                InputAction inputAction = inputActionMap.FindAction(actionName);
                inputAction.started -= OnSlotPerformed;
            }
        }

        private void OnSlotPerformed(InputAction.CallbackContext obj)
        {
            int emoteIndex = actionNameById[obj.action.name];
            triggeredEmote = emoteIndex;
        }

        protected override void Update(float t)
        {
            if (shortcuts.EmoteWheel.WasReleasedThisFrame())
                mvcManager.ShowAsync(EmotesWheelController.IssueCommand()).Forget();

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
        private void TriggerEmote([Data] int emoteIndex, in Entity entity, in Profile profile)
        {
            IReadOnlyList<URN> emotes = profile.Avatar.Emotes;
            if (emoteIndex < 0 || emoteIndex >= emotes.Count) return;

            URN emoteId = emotes[emoteIndex];

            if (emoteId.IsNullOrEmpty()) return;

            var newEmoteIntent = new CharacterEmoteIntent { EmoteId = emoteId };
            ref var emoteIntent = ref World.AddOrGet(entity, newEmoteIntent);
            emoteIntent = newEmoteIntent;

            messageBus.Send(emoteId, false, true);
        }
    }
}
