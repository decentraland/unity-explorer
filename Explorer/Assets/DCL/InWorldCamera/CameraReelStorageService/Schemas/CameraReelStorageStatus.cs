namespace DCL.InWorldCamera.CameraReelStorageService.Schemas
{
    public readonly struct CameraReelStorageStatus
    {
        public readonly int ScreenshotsAmount;
        public readonly int MaxScreenshots;

        public readonly bool HasFreeSpace;

        public CameraReelStorageStatus(int screenshotsAmount, int maxScreenshots)
        {
            this.ScreenshotsAmount = screenshotsAmount;
            MaxScreenshots = maxScreenshots;

            HasFreeSpace = ScreenshotsAmount < MaxScreenshots;
        }
    }
}
