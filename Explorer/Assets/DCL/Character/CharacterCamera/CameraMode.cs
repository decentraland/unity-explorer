namespace DCL.CharacterCamera
{
    public enum CameraMode : byte
    {
        FirstPerson,
        ThirdPerson,
        DroneView,

        /// <summary>
        ///     Free-fly, does not follow character, intercepts controls designated for character movement
        /// </summary>
        Free,
    }
}
