using DCL.InWorldCamera.CameraReelStorageService.Schemas;
using UnityEngine;

namespace DCL.InWorldCamera
{
    public struct InWorldCameraComponent { }

    public struct ToggleInWorldCameraRequest
    {
        public bool IsEnable;
        public string Source;
    }
    public struct TakeScreenshotRequest { public string Source; }
    public struct CameraTarget { public CharacterController Value; }

    public struct InWorldCameraInput
    {
        public Vector2 Translation;
        public float Panning;
        public bool IsRunning;

        public Vector2 Aim;
        public bool MouseIsDragging;
        public float Zoom;
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
