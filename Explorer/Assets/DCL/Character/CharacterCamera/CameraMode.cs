namespace DCL.CharacterCamera
{
    public enum CameraMode : byte
    {
        Unknown = 0,
        FirstPerson = 1,
        ThirdPerson = 2,
        DroneView = 3,
        SDKCamera = 4,

        /// <summary>
        ///     Free-fly, does not follow character, intercepts controls designated for character movement
        /// </summary>
        Free = 5,
    }
}
