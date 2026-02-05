using DCL.AvatarRendering.Loading;
using DCL.AvatarRendering.Loading.DTO;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;

namespace DCL.AvatarRendering.Emotes
{
    [Serializable]
    public class TrimmedEmoteDTO : TrimmedAvatarAttachmentDTO<TrimmedEmoteDTO.EmoteMetadataDto>
    {
        public ElementIndividualDataDto[] individualData;

        [Serializable]
        public class EmoteMetadataDto : TrimmedAvatarAttachmentDTO.TrimmedMetadataBase<TrimmedAvatarAttachmentDTO.TrimmedDataBase>
        {
            public DataDto emoteDataADR74 = new ();
            public override TrimmedAvatarAttachmentDTO.TrimmedDataBase AbstractData => emoteDataADR74;

            [Serializable]
            public class DataDto : TrimmedAvatarAttachmentDTO.TrimmedDataBase
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
        public class LambdaResponseElementDto : ILambdaResponseElement<TrimmedEmoteDTO>
        {
            public TrimmedEmoteDTO entity;

            [JsonIgnore]
            public TrimmedEmoteDTO Entity => entity;

            [JsonIgnore]
            public IReadOnlyList<ElementIndividualDataDto> IndividualData => entity.individualData;
        }
    }

    public static class TrimmedEmoteDTOExtensions
    {
        public static TrimmedEmoteDTO Convert(this EmoteDTO emoteDTO, string thumbnailHash)
        {
            return new TrimmedEmoteDTO
            {
                id = emoteDTO.id, thumbnail = thumbnailHash, metadata = new TrimmedEmoteDTO.EmoteMetadataDto
                {
                    id = emoteDTO.metadata.id, rarity = emoteDTO.metadata.rarity, name = emoteDTO.metadata.name, emoteDataADR74 = new TrimmedEmoteDTO.EmoteMetadataDto.DataDto
                    {
                        category = emoteDTO.metadata.data.category, representations = emoteDTO.metadata.data.representations
                    }
                }
            };
        }
    }
}
