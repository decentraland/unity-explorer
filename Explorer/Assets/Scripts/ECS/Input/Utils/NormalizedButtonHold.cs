using UnityEngine;
using UnityEngine.InputSystem;

namespace ECS.Input.Utils
{
    public class NormalizedButtonPressListener
    {
        private InputAction actionToListen;
        private float currentValue;
        private float timeToMax;
        private bool isPressed;

        public NormalizedButtonPressListener(InputAction actionToListen, float timeToMax)
        {
            this.actionToListen = actionToListen;
            this.timeToMax = timeToMax;
        }

        public void Update(float dt)
        {
            if (actionToListen.WasPerformedThisFrame())
            {
                isPressed = true;
            }

            if (actionToListen.WasReleasedThisFrame())
            {
                isPressed = false;
                currentValue = 0;
            }

            if (isPressed)
                currentValue += dt;
        }

        public float GetValue() =>
            Mathf.Clamp(currentValue, 0, timeToMax) / timeToMax;
    }
}
