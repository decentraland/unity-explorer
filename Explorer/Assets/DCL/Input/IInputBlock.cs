using DCL.Input.Component;

namespace DCL.Input
{
    public interface IInputBlock
    {
        public void Disable(params InputMapComponent.Kind[] kinds);
        public void Disable(InputMapComponent.Kind kind);
        public void Disable(InputMapComponent.Kind kind, InputMapComponent.Kind kind2);
        public void Disable(InputMapComponent.Kind kind, InputMapComponent.Kind kind2, InputMapComponent.Kind kind3);

        public void Enable(params InputMapComponent.Kind[] kinds);
        public void Enable(InputMapComponent.Kind kind);
        public void Enable(InputMapComponent.Kind kind, InputMapComponent.Kind kind2);
        public void Enable(InputMapComponent.Kind kind, InputMapComponent.Kind kind2, InputMapComponent.Kind kind3);

    }
}
