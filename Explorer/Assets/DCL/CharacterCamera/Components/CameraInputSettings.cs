namespace DCL.CharacterCamera.Components
{
    public readonly struct CameraInputSettings
    {
        public readonly float CameraModeMouseWheelThreshold;

        public CameraInputSettings(float cameraModeMouseWheelThreshold)
        {
            CameraModeMouseWheelThreshold = cameraModeMouseWheelThreshold;
        }
    }
}
