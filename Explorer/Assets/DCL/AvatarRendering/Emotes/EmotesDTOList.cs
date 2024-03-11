using System.Collections.Generic;

namespace DCL.AvatarRendering.Emotes
{
    public struct EmotesDTOList
    {
        public readonly IReadOnlyList<EmoteDTO> Value;

        public EmotesDTOList(IReadOnlyList<EmoteDTO> value)
        {
            Value = value;
        }
    }
}
