using UnityEngine;

namespace DCL.Input
{
    public enum CursorStyle
    {
        None,
        Normal,
        Interaction,
        CameraPan,
        Grab,
        Dragging,
        NotAllowed,
    }

    /// <summary>A hardware cursor image and the pixel of it that sits on the pointer position.</summary>
    public readonly struct CursorTexture
    {
        public readonly Texture2D Texture;
        public readonly Vector2 Hotspot;

        public CursorTexture(Texture2D texture, Vector2 hotspot)
        {
            Texture = texture;
            Hotspot = hotspot;
        }
    }

    public class DCLCursor : ICursor
    {
        private readonly CursorTexture normalCursor;
        private readonly CursorTexture interactionCursor;
        private readonly CursorTexture grabCursor;
        private readonly CursorTexture draggingCursor;
        private readonly CursorTexture notAllowedCursor;
        private CursorStyle cursorStyle = CursorStyle.None;

        public bool IsStyleForced { get; private set; }

        public DCLCursor(CursorTexture normalCursor, CursorTexture interactionCursor, CursorTexture grabCursor, CursorTexture draggingCursor, CursorTexture notAllowedCursor)
        {
            this.normalCursor = normalCursor;
            this.interactionCursor = interactionCursor;
            this.grabCursor = grabCursor;
            this.draggingCursor = draggingCursor;
            this.notAllowedCursor = notAllowedCursor;
            SetStyle(CursorStyle.Normal);
        }

        public bool IsLocked() =>
            Cursor.lockState != CursorLockMode.None;

        public void Lock() // IGNORE_LINE_WEBGL_THREAD_SAFETY_FLAG 
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

        public void SetStyle(CursorStyle style, bool force = false)
        {
            if (cursorStyle == style) return;
            IsStyleForced = force;

            cursorStyle = style;

            switch (cursorStyle)
            {
                case CursorStyle.Normal:
                    Apply(normalCursor);
                    return;
                case CursorStyle.Interaction:
                    Apply(interactionCursor);
                    return;
                case CursorStyle.Grab:
                    Apply(grabCursor);
                    return;
                case CursorStyle.Dragging:
                    Apply(draggingCursor);
                    return;
                case CursorStyle.NotAllowed:
                    Apply(notAllowedCursor);
                    return;
                default:
                    Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
                    break;
            }
        }

        private static void Apply(in CursorTexture cursor) =>
            Cursor.SetCursor(cursor.Texture, cursor.Hotspot, CursorMode.Auto);
    }
}
