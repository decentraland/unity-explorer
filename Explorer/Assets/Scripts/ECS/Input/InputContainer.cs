using ECS.CharacterMotion.InputHandlers;
using ECS.Input.Handler;
using System.Collections.Generic;

namespace ECS.Input
{
    public class InputContainer
    {

        public List<InputComponentHandler> inputComponentHandlers;
        public readonly DCLInput dclInput;

        public InputContainer()
        {
            dclInput = new DCLInput();

            inputComponentHandlers = new List<InputComponentHandler>()
            {
                new JumpInputComponentHandler(dclInput),
                new MovementInputComponentHandler(dclInput)
            };
        }
    }
}
