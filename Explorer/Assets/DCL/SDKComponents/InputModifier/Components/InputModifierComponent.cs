using System;

namespace DCL.SDKComponents.PlayerInputMovement.Components
{
    /// <summary>
    /// Allows systems to modify different aspects of the player input.
    /// </summary>
    public struct InputModifierComponent
    {
        private bool disableAll;
        public bool DisableAll
        {
            get => disableAll;

            set
            {
                disableAll = value;
                DisableWalk = value;
                DisableJog = value;
                DisableRun = value;
                DisableJump = value;
                DisableEmote = value;
                DisableCamera = value;
            }
        }

        public bool DisableWalk;
        public bool DisableJog;
        public bool DisableRun;
        public bool DisableJump;
        public bool DisableEmote;
        public bool DisableCamera;
    }
}
