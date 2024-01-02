namespace DCL.AvatarRendering.Wearables.Components
{
    public struct WearableThumbnailComponent
    {
        public readonly IWearable Wearable;

        public WearableThumbnailComponent(IWearable wearable)
        {
            this.Wearable = wearable;
        }
    }
}
