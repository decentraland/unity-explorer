using UnityEngine;

namespace DCL.CharacterMotion.Components
{
    public struct HeadIKComponent
    {
        public bool IsEnabled;

        public Vector3 LookAt;

        public readonly Vector2 GetHeadYawAndPitch()
        {
            if (LookAt.sqrMagnitude < 0.0001f) return Vector2.zero;
            Vector3 angles = Quaternion.LookRotation(LookAt).eulerAngles;
            return new Vector2(angles.y, angles.x);
        }
    }
}
