using DCL.AvatarRendering.Loading.Components;

namespace DCL.AvatarRendering.Wearables.Helpers
{
    // ReSharper disable once InconsistentNaming
    public readonly struct WearablesDTOList : IAttachmentsDTOList<WearableDTO>
    {
        private readonly RepoolableList<WearableDTO> value;

        public WearablesDTOList(RepoolableList<WearableDTO> value)
        {
            this.value = value;
        }

        public ConsumedAttachmentsDTOList<WearableDTO> ConsumeAttachments() =>
            IAttachmentsDTOList<WearableDTO>.DefaultConsumeAttachments(value);
    }
}
