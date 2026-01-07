using UnityEngine;

namespace DCL.Character.CharacterCamera.Components
{
    public enum CursorState
    {
        Free,
        Locked,
        Panning,
    }

    public struct CursorComponent
    {
        public bool IsOverUI;
        public CursorState CursorState;
        public bool PositionIsDirty;
        public Vector2 Position;
    }
}
