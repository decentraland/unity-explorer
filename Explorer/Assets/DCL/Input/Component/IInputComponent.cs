namespace DCL.Input.Component
{
    public interface IInputComponent { }

    public interface IKeyComponent : IInputComponent
    {
        ButtonArguments ButtonArguments { get; set; }
    }

    public struct ButtonArguments
    {
        public bool IsKeyDown;
        public bool IsKeyUp;
        public bool IsKeyPressed;
    }

    public struct PrimaryKey : IKeyComponent
    {
        public ButtonArguments ButtonArguments { get; set; }
    }

    public static class InputComponentExtensions
    {
        public static bool IsKeyDown<T>(this ref T keyComponent) where T: struct, IKeyComponent =>
            keyComponent.ButtonArguments.IsKeyDown;

        public static bool IsKeyUp<T>(this ref T keyComponent) where T: struct, IKeyComponent =>
            keyComponent.ButtonArguments.IsKeyUp;

        public static bool IsKeyPressed<T>(this ref T keyComponent) where T: struct, IKeyComponent =>
            keyComponent.ButtonArguments.IsKeyPressed;

        public static void SetKeyDown<T>(this ref T keyComponent, bool isKeyDown) where T: struct, IKeyComponent
        {
            ButtonArguments ba = keyComponent.ButtonArguments;
            ba.IsKeyDown = isKeyDown;
            keyComponent.ButtonArguments = ba;
        }

        public static void SetKeyUp<T>(this ref T keyComponent, bool isKeyUp) where T: struct, IKeyComponent
        {
            ButtonArguments ba = keyComponent.ButtonArguments;
            ba.IsKeyUp = isKeyUp;
            keyComponent.ButtonArguments = ba;
        }

        public static void SetKeyPressed<T>(this ref T keyComponent, bool isKeyPressed) where T: struct, IKeyComponent
        {
            ButtonArguments ba = keyComponent.ButtonArguments;
            ba.IsKeyPressed = isKeyPressed;
            keyComponent.ButtonArguments = ba;
        }
    }
}
