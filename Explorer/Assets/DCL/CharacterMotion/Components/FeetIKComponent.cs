using UnityEngine;

namespace DCL.CharacterMotion.Components
{
    public struct FeetIKComponent
    {
        public FeetComponent Left;
        public FeetComponent Right;
        public bool Initialized;

        public struct FeetComponent
        {
            public Vector3 TargetInitialPosition;
            public Quaternion TargetInitialRotation;
            public bool isGrounded;
            public float distance;
        }
    }
}
