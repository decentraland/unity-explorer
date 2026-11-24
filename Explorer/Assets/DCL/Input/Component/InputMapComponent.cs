using DCL.Diagnostics;
using System;
using System.Collections.Generic;
using Utility;

namespace DCL.Input.Component
{
    public struct InputMapComponent
    {
        public static readonly IReadOnlyList<Kind> VALUES = EnumUtils.Values<Kind>();

        public static readonly Kind[] BLOCK_USER_INPUT =
        {
            Kind.InWorldCamera,
            Kind.Camera,
            Kind.Shortcuts,
            Kind.Player,
            Kind.VoiceChat
        };


        [Flags]
        public enum Kind
        {
            None = 0,
            Player = 1,
            Camera = 1 << 1,
            FreeCamera = 1 << 2,
            EmoteWheel = 1 << 3,
            Emotes = 1 << 4,
            Shortcuts = 1 << 5,
            InWorldCamera = 1 << 6,
            VoiceChat = 1 << 7,

            /// <summary>
            ///     The single UI/Submit action (Enter), not a whole action map. Blocked while a text field
            ///     owns the keyboard so Enter reaches that field instead of focusing the chat.
            /// </summary>
            Submit = 1 << 8,
        }

        private Kind active;
        private readonly Dictionary<Kind, int> inputBlockCounters;

        /// <summary>
        ///     Active maps flags
        /// </summary>
        public Kind Active
        {
            get => active;

            private set
            {
                active = value;
                IsDirty = true;
            }
        }

        public bool IsDirty;

        public InputMapComponent(Kind flags)
        {
            active = flags;
            IsDirty = true;
            inputBlockCounters = new Dictionary<Kind, int>(VALUES.Count);

            for (var i = 0; i < VALUES.Count; i++)
            {
                Kind kind = VALUES[i];
                inputBlockCounters[kind] = EnumUtils.HasFlag(active, kind) ? 0 : 1;
            }
        }

        public void BlockInput(Kind kind)
        {
            if (!inputBlockCounters.TryGetValue(kind, out int counter))
            {
                ReportHub.LogError(ReportCategory.INPUT, $"Cannot block input {kind}: it is not a single input map kind");
                return;
            }

            counter += 1;
            inputBlockCounters[kind] = counter;
            ReportHub.Log(ReportCategory.INPUT, $"Block {kind} - {counter}");

            if (counter == 1) { Active &= ~kind; }
        }

        public void UnblockInput(Kind kind)
        {
            if (!inputBlockCounters.TryGetValue(kind, out int counter))
            {
                ReportHub.LogError(ReportCategory.INPUT, $"Cannot unblock input {kind}: it is not a single input map kind");
                return;
            }

            counter -= 1;
            inputBlockCounters[kind] = counter;
            ReportHub.Log(ReportCategory.INPUT, $"Unblock {kind} - {counter}");
            switch (counter)
            {
                case 0:
                    Active |= kind;
                    break;
                case < 0:
                    ReportHub.LogWarning(ReportCategory.INPUT, $"Block Counter is less than zero for {kind.ToString()} but it should not be. Something is trying to unlock an input without blocking it first or double unlocking is happening");
                    inputBlockCounters[kind] = 0;
                    break;
            }
        }
    }
}
