using DCL.AvatarRendering.Loading.DTO;

namespace DCL.AvatarRendering.Loading.Components
{
    public interface ITrimmedAvatarAttachment
    {
        TrimmedAvatarAttachmentDTO TrimmedDTO { get; }
        void SetAmount(int amount);
    }
}
