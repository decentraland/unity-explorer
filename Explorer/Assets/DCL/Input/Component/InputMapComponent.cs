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
        }

        private Kind active;

        /// <summary>
        ///     Active maps flags
        /// </summary>
        public Kind Active
        {
            get => active;

            set
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
        }
    }
}
