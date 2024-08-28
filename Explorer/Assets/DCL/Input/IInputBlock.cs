using DCL.Input.Component;

namespace DCL.Input
{
    public interface IInputBlock
    {
        public void Disable(params InputMapComponent.Kind[] kinds);
        public void Enable(params InputMapComponent.Kind[] kinds);
    }
}
