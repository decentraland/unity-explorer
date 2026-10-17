using DCL.CharacterMotion.Components;
using DCL.Input.Component;

namespace DCL.Character.CharacterMotion.Components
{
    public struct JumpInputComponent : IInputComponent
    {
        public JumpTrigger Trigger;
        public bool IsPressed { get; set; }
    }
}
