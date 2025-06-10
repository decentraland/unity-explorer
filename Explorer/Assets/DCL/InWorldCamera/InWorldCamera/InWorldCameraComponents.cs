using DCL.CharacterCamera;
using MVC;
using UnityEngine;

namespace DCL.InWorldCamera
{
    public struct InWorldCameraComponent { }

    public struct ToggleInWorldCameraRequest
    {
        public bool IsEnable;
        public string Source;
        public CameraMode? TargetCameraMode;
    }

    public struct ToggleUIRequest
    {
        public bool Enable;
        public IController Except;
    }

    public struct TakeScreenshotRequest { public string Source; }
    public struct CameraTarget { public CharacterController Value; }

    public struct InWorldCameraInput
    {
        public Vector2 Translation;
        public float Panning;
        public float Tilting;
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

    public struct CameraDampedTilt
    {
        public float Current;
        public float Target;
        public float Velocity;
    }

    public struct CameraDampedAim
    {
        public Vector2 Current;
        public Vector2 Velocity;
    }
}
