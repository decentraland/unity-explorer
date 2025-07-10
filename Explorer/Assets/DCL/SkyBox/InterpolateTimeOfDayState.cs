using UnityEngine;

namespace DCL.SkyBox
{
    public class InterpolateTimeOfDayState : ISkyboxState
    {
        private readonly SkyboxSettingsAsset settings;

        public InterpolateTimeOfDayState(SkyboxSettingsAsset settings)
        {
            this.settings = settings;
        }

        public bool Applies()
        {
            float current = settings.TimeOfDayNormalized;
            float target = settings.TargetTimeOfDayNormalized;

            return !Mathf.Approximately(current, target);
        }

        public void Enter() { }

        public void Exit() { }

        public void Update(float dt)
        {
            float current = settings.TimeOfDayNormalized;
            float target = settings.TargetTimeOfDayNormalized;
            float speed = settings.TransitionSpeed;

            if (Mathf.Approximately(current, target)) return;

            float step = dt * speed;

            switch (settings.TransitionMode)
            {
                case TransitionMode.FORWARD:
                {
                    float distance;

                    if (target >= current)
                        distance = target - current;
                    else
                        distance = target + 1f - current;

                    if (step >= distance)
                        settings.TimeOfDayNormalized = target;
                    else
                        settings.TimeOfDayNormalized = (current + step) % 1f;

                    break;
                }
                case TransitionMode.BACKWARD:
                {
                    float distance = (current - target + 1f) % 1f;

                    if (step >= distance)
                        settings.TimeOfDayNormalized = target;
                    else
                        settings.TimeOfDayNormalized = (current - step + 1f) % 1f;

                    break;
                }
            }

            settings.ShouldUpdateSkybox = true;
        }
    }
}
