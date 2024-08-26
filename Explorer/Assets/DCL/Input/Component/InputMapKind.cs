using System;

namespace DCL.Input.Component
{
    [Flags]
    public enum InputMapKind
    {
        None = 0,
        Player = 1,
        Camera = 1 << 1,
        FreeCamera = 1 << 2,
        EmoteWheel = 1 << 3,
        Emotes = 1 << 4,
        Shortcuts = 1 << 5,
    }
}
