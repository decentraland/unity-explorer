using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using CommunicationData.URLHelpers;
using DCL.AvatarRendering.Emotes.Components;
using DCL.Character.Components;
using DCL.Diagnostics;
using DCL.Input;
using DCL.Multiplayer.Emotes.Interfaces;
using DCL.Profiles;
using ECS.Abstract;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using Emote = Decentraland.Kernel.Comms.Rfc4.Emote;
using InputAction = UnityEngine.InputSystem.InputAction;

namespace DCL.CharacterMotion.Systems
{
    [LogCategory(ReportCategory.EMOTE)]
    [UpdateInGroup(typeof(InputGroup))]
    public partial class UpdateEmoteInputSystem : BaseUnityLoopSystem
    {
        private readonly Dictionary<string, int> actionNameById = new ();
        private DCLInput.EmotesActions emotesActions;
        private readonly IEmotesMessageBus messageBus;
        private int triggeredEmote = -1;

        public UpdateEmoteInputSystem(World world, DCLInput.EmotesActions emotesActions, IEmotesMessageBus messageBus) : base(world)
        {
            this.emotesActions = emotesActions;
            this.messageBus = messageBus;
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
            if (triggeredEmote < 0) return;

            messageBus.InjectWorld(World!);
            TriggerEmoteQuery(World, triggeredEmote);
            triggeredEmote = -1;
        }

        [Query]
        [All(typeof(PlayerComponent))]
        [None(typeof(CharacterEmoteIntent))]
        private void TriggerEmote([Data] int emoteIndex, in Entity entity, in Profile profile)
        {
            messageBus.Send((uint)emoteIndex);
            messageBus.SelfSendWithDelayAsync(new Emote
            {
                EmoteId = (uint)emoteIndex,
                Timestamp = Time.unscaledTime,
            }, 0.1f).Forget();

            IReadOnlyList<URN> emotes = profile.Avatar.Emotes;
            if (emoteIndex < 0 || emoteIndex >= emotes.Count) return;

            URN emoteId = emotes[emoteIndex];

            if (!string.IsNullOrEmpty(emoteId))
                World.Add(entity, new CharacterEmoteIntent { EmoteId = emoteId });
        }
    }
}
