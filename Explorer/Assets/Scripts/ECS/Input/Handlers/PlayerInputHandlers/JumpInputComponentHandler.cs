using ECS.CharacterMotion.Components;
using ECS.Input.Handler;
using ECS.Input.Utils;

namespace ECS.CharacterMotion.InputHandlers
{
    public class JumpInputComponentHandler : InputComponentHandler<JumpInputComponent>
    {
        private readonly NormalizedButtonPressListener jumpNormalizedButtonPressListener;

        public JumpInputComponentHandler(DCLInput dclInput)
        {
            jumpNormalizedButtonPressListener = new NormalizedButtonPressListener(dclInput.Player.Jump, 1.5f);
        }

        public void HandleInput(float t, ref JumpInputComponent component)
        {
            jumpNormalizedButtonPressListener.Update(t);
            component.Power = jumpNormalizedButtonPressListener.GetValue();
        }

    }
}
