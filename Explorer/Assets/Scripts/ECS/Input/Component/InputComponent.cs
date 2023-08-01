using UnityEngine.InputSystem;

namespace ECS.Input.Component
{
    public interface InputComponent
    {

    }

    public interface KeyComponent : InputComponent
    {
        ButtonArguments ButtonArguments { get; set; }
    }

    public interface PhysicalKeyComponent : InputComponent
    {
        PhysicalButtonArguments PhysicalButtonArguments { get; set; }
    }

    public struct ButtonArguments
    {
        public bool IsKeyDown;
        public bool IsKeyUp;
        public bool IsKeyPressed;
    }

    public struct PhysicalButtonArguments
    {
        public int tickDown;
        public int tickUp;
        public bool IsKeyPressed;

    }

    public struct PrimaryKey : KeyComponent
    {
        public ButtonArguments ButtonArguments { get; set; }
    }

    public static class InputComponentExtensions
    {

        public static bool IsKeyDown<T>(this ref T keyComponent) where T: struct, KeyComponent =>
            keyComponent.ButtonArguments.IsKeyDown;

        public static bool IsKeyUp<T>(this ref T keyComponent) where T: struct, KeyComponent =>
            keyComponent.ButtonArguments.IsKeyUp;

        public static bool IsKeyPressed<T>(this ref T keyComponent) where T: struct, KeyComponent =>
            keyComponent.ButtonArguments.IsKeyPressed;

        public static void SetKeyDown<T>(this ref T keyComponent, bool isKeyDown) where T: struct, KeyComponent
        {
            ButtonArguments ba = keyComponent.ButtonArguments;
            ba.IsKeyDown = isKeyDown;
            keyComponent.ButtonArguments = ba;
        }

        public static void SetKeyUp<T>(this ref T keyComponent, bool isKeyUp) where T: struct, KeyComponent
        {
            ButtonArguments ba = keyComponent.ButtonArguments;
            ba.IsKeyUp = isKeyUp;
            keyComponent.ButtonArguments = ba;
        }

        public static void SetKeyPressed<T>(this ref T keyComponent, bool isKeyPressed) where T: struct, KeyComponent
        {
            ButtonArguments ba = keyComponent.ButtonArguments;
            ba.IsKeyPressed = isKeyPressed;
            keyComponent.ButtonArguments = ba;
        }

        public static bool IsKeyDown<T>(this ref T keyComponent, int physicsTick) where T: struct, PhysicalKeyComponent =>
            keyComponent.PhysicalButtonArguments.tickDown == physicsTick;

        public static bool IsKeyUp<T>(this ref T keyComponent, int physicsTick) where T: struct, PhysicalKeyComponent =>
            keyComponent.PhysicalButtonArguments.tickUp == physicsTick;

        public static bool IsPhysicalKeyPressed<T>(this ref T keyComponent) where T: struct, PhysicalKeyComponent =>
            keyComponent.PhysicalButtonArguments.IsKeyPressed;

        public static void SetPhysicsTickKeyDown<T>(this ref T keyComponent, int tickWhenPressed) where T: struct, PhysicalKeyComponent
        {
            PhysicalButtonArguments pba = keyComponent.PhysicalButtonArguments;
            pba.tickDown = tickWhenPressed;
            keyComponent.PhysicalButtonArguments = pba;
        }

        public static void SetPhysicsTickKeyUp<T>(this ref T keyComponent, int tickWhenPressed) where T: struct, PhysicalKeyComponent
        {
            PhysicalButtonArguments pba = keyComponent.PhysicalButtonArguments;
            pba.tickUp = tickWhenPressed;
            keyComponent.PhysicalButtonArguments = pba;
        }

        public static void SetPhysicsKeyPressed<T>(this ref T keyComponent, bool value) where T: struct, PhysicalKeyComponent
        {
            PhysicalButtonArguments pba = keyComponent.PhysicalButtonArguments;
            pba.IsKeyPressed = value;
            keyComponent.PhysicalButtonArguments = pba;
        }

    }

}
