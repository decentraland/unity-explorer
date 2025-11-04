using System;

namespace DCL.AvatarRendering.AvatarShape.Components
{
    public struct HiddenPlayerComponent
    {
        [Flags]
        public enum HiddenReason : byte
        {
            BLOCKED = 1 << 0,
            BANNED  = 1 << 1,
        }

        public HiddenReason Reason;
    }
}
