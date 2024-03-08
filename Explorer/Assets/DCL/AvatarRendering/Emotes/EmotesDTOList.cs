using System.Collections.Generic;

namespace DCL.AvatarRendering.Emotes
{
    public struct EmotesDTOList
    {
        public readonly IReadOnlyList<EmoteJsonDTO> Value;

        public EmotesDTOList(IReadOnlyList<EmoteJsonDTO> value)
        {
            Value = value;
        }
    }
}
