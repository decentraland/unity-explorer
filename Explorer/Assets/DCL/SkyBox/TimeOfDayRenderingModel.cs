using UnityEngine;

namespace DCL.SkyBox
{
    /// <summary>
    ///     This model is shared between Rendering Features and Regular C# Code
    /// </summary>
    public class TimeOfDayRenderingModel
    {
        private Vector3 vSunPos;
        private Vector3 vMoonPos;

        public void SetStellarPositions(Vector3 sun, Vector3 moon)
        {
            vSunPos = sun;
            vMoonPos = moon;
        }

        public float GetLightIntensity()
        {
            float sunPosOffset = vSunPos.x + 20.0f;
            return Mathf.Min(1.0f, (1.0f - Mathf.Abs((sunPosOffset / 110.0f) - 1.0f)) * 2.0f);
        }

        public Vector3 GetSunPosLocal() =>
            vSunPos;

        public Vector3 GetSunPosition()
        {
            var dir = new Vector3(0.0f, 0.0f, -1.0f);
            return Quaternion.Euler(vSunPos) * dir;
        }

        public Vector3 GetMoonPosition()
        {
            var dir = new Vector3(0.0f, 0.0f, -1.0f);
            return Quaternion.Euler(vMoonPos) * dir;
        }

        public Vector4 GetLightDirection()
        {
            var dir = new Vector3(0.0f, 0.0f, -1.0f);
            return Quaternion.Euler(vSunPos) * dir;
        }
    }
}
