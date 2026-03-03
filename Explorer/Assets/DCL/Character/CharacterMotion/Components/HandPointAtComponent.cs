using UnityEngine;

namespace DCL.Character.CharacterMotion.Components
{
    public struct HandPointAtComponent
    {
        public bool IsPointing;
        public Vector3 Point;
        public float AnimationWeight;

        private float duration;

        public void RefreshDuration(float newDuration)
        {
            duration = newDuration;
        }

        public void TickDuration(float deltaTime)
        {
            duration = Mathf.Clamp(duration - deltaTime, 0f, float.MaxValue);
            if (duration <= 0)
                IsPointing = false;
        }
    }
}
