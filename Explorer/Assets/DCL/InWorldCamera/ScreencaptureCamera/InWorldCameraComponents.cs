using UnityEngine;

namespace DCL.InWorldCamera.ScreencaptureCamera
{
    public struct InWorldCamera
    {
    }

    public struct CameraTarget
    {
        public CharacterController Value;
    }

    public struct CameraDampedFOV
    {
        public float Current;
        public float Velocity;
        public float Target;
    }

    public struct CameraDampedAim
    {
        public Vector2 Current;
        public Vector2 Velocity;
    }
}
