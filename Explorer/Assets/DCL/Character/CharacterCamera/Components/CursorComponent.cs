using UnityEngine;

namespace DCL.CharacterCamera.Components
{
    public struct CursorComponent
    {
        public bool IsOverUI;
        public bool CursorIsLocked;
        public bool AllowCameraMovement;
        public bool CancelCursorLock;
        public Vector2 Position;
    }
}
