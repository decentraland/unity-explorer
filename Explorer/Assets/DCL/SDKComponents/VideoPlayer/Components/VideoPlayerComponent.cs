using DCL.ECSComponents;

namespace DCL.SDKComponents.VideoPlayer
{
    public struct VideoPlayerComponent
    {
        private readonly PBVideoPlayer sdkVideo;

        public VideoPlayerComponent(PBVideoPlayer sdkVideo)
        {
            this.sdkVideo = sdkVideo;
        }
    }
}
