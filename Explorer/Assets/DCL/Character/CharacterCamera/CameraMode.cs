namespace DCL.CharacterCamera
{
    public enum CameraMode : byte
    {
        FirstPerson = 0,
        ThirdPerson = 1,
        DroneView = 2,
        SDKCamera = 3,

        /// <summary>
        ///     Free-fly, does not follow character, intercepts controls designated for character movement
        /// </summary>
        Free = 4,
    }
}
