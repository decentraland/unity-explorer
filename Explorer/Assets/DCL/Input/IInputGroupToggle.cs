using DCL.Input.Component;

namespace DCL.Input
{
    public interface IInputGroupToggle
    {
        void Set(InputMapKind kind);

        void Enable(InputMapKind kind);

        void Disable(InputMapKind kind);
    }
}
