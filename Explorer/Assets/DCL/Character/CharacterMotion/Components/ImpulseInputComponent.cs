using DCL.Input.Component;

namespace DCL.Character.CharacterMotion.Components
{
    public struct ImpulseInputComponent : IInputComponent
    {
        public bool WasTriggered;
        public bool IsPressed { get; set; }
    }
}
