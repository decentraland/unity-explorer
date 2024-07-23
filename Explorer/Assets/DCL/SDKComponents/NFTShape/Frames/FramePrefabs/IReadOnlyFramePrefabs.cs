using DCL.ECSComponents;

namespace DCL.SDKComponents.NFTShape.Frames.FramePrefabs
{
    public interface IReadOnlyFramePrefabs
    {
        bool IsInitialized { get; }

        AbstractFrame FrameOrDefault(NftFrameType frameType);
    }
}
