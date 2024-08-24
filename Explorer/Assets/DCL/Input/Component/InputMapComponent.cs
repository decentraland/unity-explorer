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
            None = 0,
            Player = 1,
            Camera = 1 << 1,
            FreeCamera = 1 << 2,
            EmoteWheel = 1 << 3,
            Emotes = 1 << 4,
            Shortcuts = 1 << 5,
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

        public void BlockInput(Kind kind)
        {
            inputBlockCounters[kind] += 1;
            ReportHub.LogError(ReportData.UNSPECIFIED, $"blockCounters INCREASED for {kind.ToString()} value {inputBlockCounters![kind]}");

            if (inputBlockCounters[kind] == 1)
            {
                Active &= ~kind;
            }
        }

        public void UnblockInput(Kind kind)
        {
            inputBlockCounters[kind] -= 1;
            ReportHub.LogError(ReportData.UNSPECIFIED, $"blockCounters DECREASED for {kind.ToString()} value {inputBlockCounters![kind]}");

            if (inputBlockCounters[kind] == 0) { Active |= kind; }

            if (inputBlockCounters[kind] < 0)
            {
                ReportHub.LogError(ReportData.UNSPECIFIED, $"blockCounters LESS THAN ZERO!!!!! for {kind.ToString()} value {inputBlockCounters![kind]}");
                inputBlockCounters[kind] = 0;
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
                ReportHub.LogError(ReportData.UNSPECIFIED, $"Added BlockCounter for {kind.ToString()} value {inputBlockCounters![kind]}");

            }
        }
    }
}
