using DCL.AvatarRendering.Loading;
using DCL.AvatarRendering.Loading.DTO;
using DCL.Ipfs;
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
    }

    [Serializable]
    public class BuilderWearableDTO : WearableDTO
    {
        [Serializable]
        public struct BuilderLambdaResponse : IBuilderLambdaResponse<BuilderWearableMetadataDto>
        {
            public bool ok;
            public List<BuilderWearableMetadataDto> data;

            [JsonIgnore]
            public IReadOnlyList<BuilderWearableMetadataDto> CollectionElements => data;
        }

        [Serializable]
        public class BuilderWearableMetadataDto : WearableMetadataDto, IBuilderLambdaResponseElement<BuilderWearableDTO>
        {
            public Dictionary<string, string> contents;
            public string type;

            [JsonIgnore]
            public IReadOnlyDictionary<string, string> Contents => contents;

            public BuilderWearableDTO BuildElementDTO(string contentDownloadUrl)
            {
                ContentDefinition[] parsedContent = new ContentDefinition[contents.Count];

                using (var enumerator = contents.GetEnumerator())
                {
                    for (int i = 0; i < parsedContent.Length; i++)
                    {
                        enumerator.MoveNext();
                        parsedContent[i] = new ContentDefinition()
                        {
                            file = enumerator.Current.Key,
                            hash = enumerator.Current.Value
                        };
                    }
                }

                return new BuilderWearableDTO()
                {
                    ContentDownloadUrl = contentDownloadUrl,
                    metadata = this,
                    id = this.id,
                    type = this.type,
                    content = parsedContent
                };
            }
        }
    }
}
