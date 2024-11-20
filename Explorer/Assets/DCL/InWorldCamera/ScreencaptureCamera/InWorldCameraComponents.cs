using UnityEngine;

namespace DCL.InWorldCamera.ScreencaptureCamera
{
    public struct InWorldCamera
    {
    }

    public struct InWorldCameraInput
    {
        public Vector2 Translation;
        public float Panning;
        public bool IsRunning;

        public Vector2 Aim;
        public bool MouseIsDragging;
        public float Zoom;

        public bool TakeScreenshot;
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
