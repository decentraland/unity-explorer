using DCL.AvatarRendering.Loading;
using DCL.AvatarRendering.Loading.DTO;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;

namespace DCL.AvatarRendering.Wearables.Helpers
{
    [Serializable]
    public class TrimmedWearableDTO : TrimmedAvatarAttachmentDTO<TrimmedWearableDTO.WearableMetadataDto>
    {
        public ElementIndividualDataDto[] individualData;

        [Serializable]
        public class WearableMetadataDto : TrimmedMetadataBase<TrimmedDataBase>
        {
            public DataDto data = new ();
            public override TrimmedDataBase AbstractData => data;

            [Serializable]
            public class DataDto : TrimmedDataBase
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
        public class LambdaResponseElementDto : ILambdaResponseElement<TrimmedWearableDTO>
        {
            public TrimmedWearableDTO entity;

            [JsonIgnore]
            public TrimmedWearableDTO Entity => entity;

            [JsonIgnore]
            public IReadOnlyList<ElementIndividualDataDto> IndividualData => entity.individualData;
        }
    }

    public static class TrimmedWearableDTOExtensions
    {
        public static TrimmedWearableDTO Convert(this WearableDTO wearableDTO, string thumbnailHash) =>
            new ()
            {
                id = wearableDTO.id,
                thumbnail = thumbnailHash,
                metadata = new TrimmedWearableDTO.WearableMetadataDto
                {
                    id = wearableDTO.metadata.id,
                    rarity = wearableDTO.metadata.rarity,
                    data = new TrimmedWearableDTO.WearableMetadataDto.DataDto
                    {
                        category = wearableDTO.metadata.data.category,
                        representations = wearableDTO.metadata.data.representations
                    }
                }
            };
    }
}
