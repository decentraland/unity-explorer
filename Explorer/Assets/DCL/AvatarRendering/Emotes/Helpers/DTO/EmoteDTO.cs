using DCL.AvatarRendering.Wearables;
using System;

namespace DCL.AvatarRendering.Emotes
{
    [Serializable]
    public class EmoteDTO : AvatarAttachmentDTO<EmoteDTO.Metadata>
    {
        [Serializable]
        public class Metadata : MetadataBase
        {
            public Data emoteDataADR74;

            public override DataBase AbstractData => emoteDataADR74;

            [Serializable]
            public class Data : DataBase
            {
                public bool loop;
            }
        }
    }
}
