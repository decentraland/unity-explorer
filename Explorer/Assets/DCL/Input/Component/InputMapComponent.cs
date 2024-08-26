using System.Collections.Generic;
using Utility;

namespace DCL.Input.Component
{
    public struct InputMapComponent
    {
        public static readonly IReadOnlyList<InputMapKind> VALUES = EnumUtils.Values<InputMapKind>();

        private InputMapKind active;

        /// <summary>
        ///     Active maps flags
        /// </summary>
        public InputMapKind Active
        {
            get => active;

            set
            {
                active = value;
                IsDirty = true;
            }
        }

        public bool IsDirty;

        public InputMapComponent(InputMapKind flags)
        {
            active = flags;
            IsDirty = true;
        }
    }
}
