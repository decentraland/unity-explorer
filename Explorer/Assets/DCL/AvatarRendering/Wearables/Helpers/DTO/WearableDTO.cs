using DCL.AvatarRendering.Loading;
using DCL.AvatarRendering.Loading.DTO;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;

namespace DCL.AvatarRendering.Wearables.Helpers
{
    [Serializable]
    public class WearableDTO : AvatarAttachmentDTO<WearableDTO.WearableMetadataDto>
    {
        [Serializable]
        public class WearableMetadataDto : MetadataBase
        {
            public DataDto data = new ();
            public override DataBase AbstractData => data;

            [Serializable]
            public class DataDto : DataBase
            {
            }
        }

        [Serializable]
        public struct LambdaResponse : IAttachmentLambdaResponse<LambdaResponseElementDto>
        {
            public List<LambdaResponseElementDto> elements;
            public int totalAmount;

            [JsonIgnore]
            public IReadOnlyList<LambdaResponseElementDto> Page => elements;

            [JsonIgnore]
            public int TotalAmount => totalAmount;
        }

        [Serializable]
        public class LambdaResponseElementDto : ILambdaResponseElement<WearableDTO>
        {
            public string type;
            public string urn;
            public string name;
            public string category;
            public WearableDTO entity;
            public ElementIndividualDataDto[] individualData;

            [JsonIgnore]
            public WearableDTO Entity => entity;

            [JsonIgnore]
            public IReadOnlyList<ElementIndividualDataDto> IndividualData => individualData;
        }

        [Serializable]
        public struct BuilderLambdaResponse : IBuilderLambdaResponse
        {
            [Serializable]
            public class BuilderWearableMetadataDto : WearableMetadataDto
            {
                public Dictionary<string, string> contents;
            }

            public bool ok;
            public List<BuilderWearableMetadataDto> data;
        }
    }
}
