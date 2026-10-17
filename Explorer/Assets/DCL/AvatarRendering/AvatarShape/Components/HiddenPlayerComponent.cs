using System;

namespace DCL.AvatarRendering.AvatarShape.Components
{
    public struct HiddenPlayerComponent
    {
        [Flags]
        public enum HiddenReason : byte
        {
            Blocked = 1 << 0,
            Banned  = 1 << 1,
        }

        public HiddenReason Reason;
    }
}
