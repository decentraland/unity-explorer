using UnityEngine;

namespace DCL.Character.CharacterCamera.Components
{
    public enum CursorState
    {
        Free,
        Locked,
        Panning,

        /// <summary>
        ///     Cursor is Locked and opened a menu with which the user has to interact without unlocking.
        ///     The mouse cursor will be visible and camera will not move, so the user can click on the UI. The camera will keep
        ///     the Locked position.
        /// </summary>
        LockedWithUI
    }

    public struct CursorComponent
    {
        public bool IsOverUI;
        public CursorState CursorState;
        public bool PositionIsDirty;
        public Vector2 Position;
    }
}
