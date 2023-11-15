using DCL.Input.Component;

namespace DCL.CharacterMotion.Components
{
    public struct JumpInputComponent : IInputComponent
    {
        public JumpTrigger Trigger;
        public bool IsPressed { get; set; }
    }
}
