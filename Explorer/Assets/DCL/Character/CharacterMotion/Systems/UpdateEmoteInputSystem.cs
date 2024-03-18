using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using DCL.AvatarRendering.Emotes;
using DCL.Character.Components;
using DCL.CharacterMotion.Components;
using DCL.Diagnostics;
using DCL.Input;
using DCL.Input.Systems;
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
        private readonly string reportCategory;
        private int triggeredEmote = -1;

        private static readonly List<string> embedEmotes = new ()
        {
            "confetti",
            "dance",
            "dab",
            "clap",
        };

        public UpdateEmoteInputSystem(World world, DCLInput.EmotesActions emotesActions, IEmoteCache emoteCache) : base(world)
        {
            this.emotesActions = emotesActions;
            reportCategory = GetReportCategory();
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
            var emoteIndex = actionNameById[obj.action.name];
            triggeredEmote = emoteIndex;
        }

        protected override void Update(float t)
        {
            if (triggeredEmote >= 0)
            {
                // TODO: here we have to convert the emote wheel id to an emote id, for now we are going to load one from the embedded list
                if (triggeredEmote < embedEmotes.Count)
                {
                    string hotkeyEmote = embedEmotes[triggeredEmote];

                    TriggerEmoteQuery(World, hotkeyEmote);

                    triggeredEmote = -1;
                }
            }
        }

        [Query]
        [All(typeof(PlayerComponent))]
        [None(typeof(CharacterEmoteIntent))]
        private void TriggerEmote([Data] string emoteId, in Entity entity)
        {
            World.Add(entity, new CharacterEmoteIntent { EmoteId = emoteId});
        }
    }
}
