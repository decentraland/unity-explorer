using UnityEngine;

namespace DCL.Input.Crosshair
{
    public interface ICrosshairView
    {
        void SetCursorStyle(CursorStyle style);

        void SetDisplayed(bool displayed);

        void SetPosition(Vector2 pos);

        void ResetPosition();
    }
}
