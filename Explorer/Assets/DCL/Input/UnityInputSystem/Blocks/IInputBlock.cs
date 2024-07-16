using System;

namespace DCL.Input.UnityInputSystem.Blocks
{
    public interface IInputBlock
    {
        void BlockMovement();

        void UnblockMovement();

        class Fake : IInputBlock
        {
            public void BlockMovement()
            {
                //ignore
            }

            public void UnblockMovement()
            {
                //ignore
            }
        }
    }
}
