using UnityEngine;

namespace DCL.CharacterMotion.Components
{
    public struct HeadIKComponent
    {
        public bool YawEnabled;

        public bool PitchEnabled;

        public Vector3 LookAt;

        public bool IsEnabled => YawEnabled || PitchEnabled;

        public void SetEnabled(bool yawEnabled, bool pitchEnabled)
        {
            YawEnabled = yawEnabled;
            PitchEnabled = pitchEnabled;
        }

        public readonly Vector2 GetHeadYawAndPitch()
        {
            if (LookAt.sqrMagnitude < 0.0001f) return Vector2.zero;
            Vector3 angles = Quaternion.LookRotation(LookAt).eulerAngles;
            return new Vector2(angles.y, angles.x);
        }
    }
}
