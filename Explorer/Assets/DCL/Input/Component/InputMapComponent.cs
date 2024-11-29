using DCL.Diagnostics;
using System;
using System.Collections.Generic;
using Utility;

namespace DCL.Input.Component
{
    public struct InputMapComponent
    {
        public static readonly IReadOnlyList<Kind> VALUES = EnumUtils.Values<Kind>();

        [Flags]
        public enum Kind
        {
            NONE = 0,
            PLAYER = 1,
            CAMERA = 1 << 1,
            FREE_CAMERA = 1 << 2,
            EMOTE_WHEEL = 1 << 3,
            EMOTES = 1 << 4,
            SHORTCUTS = 1 << 5,
            IN_WORLD_CAMERA = 1 << 6,
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
                inputBlockCounters.Add(kind, EnumUtils.HasFlag(active, kind) ? 0 : 1);
            }
        }

        public void BlockInput(Kind kind)
        {
            inputBlockCounters[kind] += 1;
            ReportHub.Log(ReportCategory.INPUT, $"Block {kind} - {inputBlockCounters[kind]}");

            if (inputBlockCounters[kind] == 1) { Active &= ~kind; }
        }

        public void UnblockInput(Kind kind)
        {
            inputBlockCounters[kind] -= 1;
            ReportHub.Log(ReportCategory.INPUT, $"Unblock {kind} - {inputBlockCounters[kind]}");
            switch (inputBlockCounters[kind])
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
