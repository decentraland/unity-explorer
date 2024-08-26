using DCL.Input.Component;
using System;

namespace DCL.Input.UnityInputSystem.Blocks
{
    public interface IInputBlock
    {
        public void Initialize();
        public void BlockInputs(InputMapComponent.Kind kinds, bool singleValue = false);
        public void UnblockInputs(InputMapComponent.Kind kinds, bool singleValue = false);

        class Fake : IInputBlock
        {
            public void Initialize(){}
            public void BlockInputs(InputMapComponent.Kind kinds, bool singleValue)
            {
                //ignore
            }

            public void UnblockInputs(InputMapComponent.Kind kinds, bool singleValue)
            {
                //ignore
            }
        }
    }
}
