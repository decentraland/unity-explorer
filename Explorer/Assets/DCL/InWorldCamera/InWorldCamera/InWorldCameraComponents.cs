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
        public bool IsWalking;

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

    // ============================================
    // MCP Camera Control Components
    // ============================================

    /// <summary>
    ///     Маркер компонент - когда присутствует, MCP контролирует камеру.
    ///     User input отключается, Cinemachine Brain отключается.
    /// </summary>
    public struct MCPCameraControlComponent { }

    /// <summary>
    ///     Команда установки позиции камеры (абсолютная)
    /// </summary>
    public struct MCPCameraSetPositionCommand
    {
        public Vector3 TargetPosition;
    }

    /// <summary>
    ///     Команда установки rotation камеры через yaw/pitch
    /// </summary>
    public struct MCPCameraSetRotationCommand
    {
        public float Yaw; // Горизонтальный угол
        public float Pitch; // Вертикальный угол
    }

    /// <summary>
    ///     Команда направить камеру на точку (lookAt)
    /// </summary>
    public struct MCPCameraLookAtCommand
    {
        public Vector3 TargetPoint;
    }

    /// <summary>
    ///     Команда направить камеру на игрока
    /// </summary>
    public struct MCPCameraLookAtPlayerCommand { }

    /// <summary>
    ///     Команда установки FOV
    /// </summary>
    public struct MCPCameraSetFOVCommand
    {
        public float FOV;
    }
}
