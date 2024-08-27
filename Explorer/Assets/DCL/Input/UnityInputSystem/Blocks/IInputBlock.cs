using DCL.Input.Component;
using System;

namespace DCL.Input.UnityInputSystem.Blocks
{
    public interface IInputBlock
    {
        public void Initialize();
        public void BlockInputs(params InputMapComponent.Kind[] kinds);
        public void UnblockInputs(params InputMapComponent.Kind[] kinds);

        class Fake : IInputBlock
        {
            public void Initialize(){}
            public void BlockInputs(params InputMapComponent.Kind[] kinds)
            {
                //ignore
            }
            public void UnblockInputs(params InputMapComponent.Kind[] kinds)
            {
                //ignore
            }
        }
    }
}
