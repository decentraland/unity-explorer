namespace DCL.Input
{
    public interface ICursor
    {
        bool IsLocked();

        void Lock();

        void Unlock();

        void SetVisibility(bool visible);

        void SetStyle(CursorStyle style);
    }
}
