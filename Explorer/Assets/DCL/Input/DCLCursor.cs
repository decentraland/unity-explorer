using UnityEngine;

namespace DCL.Input
{
    public class DCLCursor : ICursor
    {
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
    }

    public interface ICursor
    {
        bool IsLocked();

        void Lock();

        void Unlock();
    }
}
