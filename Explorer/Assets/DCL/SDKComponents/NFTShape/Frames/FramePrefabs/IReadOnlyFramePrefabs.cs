using DCL.ECSComponents;

namespace DCL.SDKComponents.NFTShape.Frames.FramePrefabs
{
    public interface IReadOnlyFramePrefabs
    {
        AbstractFrame FrameOrDefault(NftFrameType frameType);
    }
}
