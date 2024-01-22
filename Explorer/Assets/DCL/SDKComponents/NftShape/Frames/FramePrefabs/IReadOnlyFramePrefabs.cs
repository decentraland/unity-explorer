using DCL.ECSComponents;

namespace DCL.SDKComponents.NftShape.Frames.FramePrefabs
{
    public interface IReadOnlyFramePrefabs
    {
        AbstractFrame FrameOrDefault(NftFrameType frameType);
    }
}
