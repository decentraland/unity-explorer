using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using CommunicationData.URLHelpers;
using DCL.Character.Components;
using DCL.CharacterMotion.Components;
using DCL.Diagnostics;
using DCL.Input;
using DCL.Input.Systems;
using DCL.Profiles;
using System.Collections.Generic;
using UnityEngine.InputSystem;

namespace DCL.CharacterMotion.Systems
{
    [LogCategory(ReportCategory.EMOTE)]
    [UpdateInGroup(typeof(InputGroup))]
    public partial class UpdateEmoteInputSystem : UpdateInputSystem<EmoteInputComponent, PlayerComponent>
    {
        private readonly Dictionary<string, int> actionNameById = new ();
        private DCLInput.EmotesActions emotesActions;
        private int triggeredEmote = -1;

        public UpdateEmoteInputSystem(World world, DCLInput.EmotesActions emotesActions) : base(world)
        {
            this.emotesActions = emotesActions;
            GetReportCategory();
            InputActionMap inputActionMap = emotesActions.Get();

            for (int i = 0; i < 8; i++)
            {
                var actionName = GetActionName(i+1);
                InputAction inputAction = inputActionMap.FindAction(actionName);
                inputAction.started += OnSlotPerformed;
                actionNameById.Add(actionName, i);
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
            if (triggeredEmote < 0) return;
            TriggerEmoteQuery(World, triggeredEmote);
            triggeredEmote = -1;
        }

        [Query]
        [All(typeof(PlayerComponent))]
        [None(typeof(CharacterEmoteIntent))]
        private void TriggerEmote([Data] int emoteIndex, in Entity entity, in Profile profile)
        {
            IReadOnlyList<URN> emotes = profile.Avatar.Emotes;
            if (emoteIndex < 0 || emoteIndex >= emotes.Count) return;

            URN emoteId = emotes[emoteIndex].Shorten();

            if (!string.IsNullOrEmpty(emoteId))
                World.Add(entity, new CharacterEmoteIntent { EmoteId = emoteId });
        }
    }
}
