using UnityEngine;

namespace DCL.Input
{
    public enum CursorStyle
    {
        None,
        Normal,
        Interaction,
        CameraPan,
    }
    public class DCLCursor : ICursor
    {
        private readonly IEventSystem eventSystem;
        private readonly Texture2D normalCursor;
        private readonly Texture2D interactionCursor;
        private CursorStyle cursorStyle = CursorStyle.None;

        public DCLCursor(Texture2D normalCursor, Texture2D interactionCursor)
        {
            this.normalCursor = normalCursor;
            this.interactionCursor = interactionCursor;
            SetStyle(CursorStyle.Normal);
        }

        public bool IsLocked() =>
            Cursor.lockState != CursorLockMode.None;

        public void Lock()
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        public void Unlock()
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        public void SetVisibility(bool visible)
        {
            Cursor.visible = visible;
        }

        public void SetStyle(CursorStyle style)
        {
            if (cursorStyle == style) return;
            cursorStyle = style;

            switch (cursorStyle)
            {
                case CursorStyle.Normal:
                    Cursor.SetCursor(normalCursor, Vector2.zero, CursorMode.Auto);
                    return;
                case CursorStyle.Interaction:
                    Cursor.SetCursor(interactionCursor, Vector2.zero, CursorMode.Auto);
                    return;
                default:
                    Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
                    break;
            }
        }
    }
}
