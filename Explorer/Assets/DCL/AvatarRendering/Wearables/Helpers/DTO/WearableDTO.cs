using System;
using System.Collections.Generic;

namespace DCL.AvatarRendering.Wearables.Helpers
{
    [Serializable]
    public class WearableDTO : AvatarAttachmentDTO<WearableDTO.WearableMetadataDto>
    {
        [Serializable]
        public class WearableMetadataDto : AvatarAttachmentDTO.MetadataBase
        {
            public DataDto data;
            public override AvatarAttachmentDTO.DataBase AbstractData => data;

            [Serializable]
            public class DataDto : AvatarAttachmentDTO.DataBase
            {
            }
        }

        [Serializable]
        public struct LambdaResponse
        {
            public List<LambdaResponseElementDto> elements;
            public int totalAmount;
        }

        [Serializable]
        public struct LambdaResponseElementDto
        {
            public string type;
            public string urn;
            public string name;
            public string category;
            public WearableDTO entity;
            public LambdaResponseIndividualDataDto[] individualData;
        }

        [Serializable]
        public struct LambdaResponseIndividualDataDto
        {
            public string id;
            public string tokenId;
            public string transferredAt;
            public string price;
        }
    }
}
