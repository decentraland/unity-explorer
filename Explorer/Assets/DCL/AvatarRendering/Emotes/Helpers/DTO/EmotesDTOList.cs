using DCL.AvatarRendering.Loading.Components;

namespace DCL.AvatarRendering.Emotes
{
    public readonly struct EmotesDTOList : IAttachmentsDTOList<EmoteDTO>
    {
        private readonly RepoolableList<EmoteDTO> value;

        public EmotesDTOList(RepoolableList<EmoteDTO> value)
        {
            this.value = value;
        }

        public ConsumedList<EmoteDTO> ConsumeAttachments() =>
            IAttachmentsDTOList<EmoteDTO>.DefaultConsumeAttachments(value);
    }
}
