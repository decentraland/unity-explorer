using ECS.Input.Component;

public struct CameraZoomInputComponent : InputComponent
{
    /// <summary>
    ///     Describes if a zoom in action should be fired
    /// </summary>
    public bool DoZoomIn;

    /// <summary>
    ///     Describes if a zoom out action should be fired
    /// </summary>
    public bool DoZoomOut;
}
