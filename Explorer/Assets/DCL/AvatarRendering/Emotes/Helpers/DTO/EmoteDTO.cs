using DCL.AvatarRendering.Loading;
using DCL.AvatarRendering.Loading.DTO;
using DCL.Ipfs;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;

namespace DCL.AvatarRendering.Emotes
{
    [Serializable]
    public class EmoteDTO : AvatarAttachmentDTO<EmoteDTO.EmoteMetadataDto>
    {
        [Serializable]
        public class EmoteMetadataDto : MetadataBase
        {
            // emotes DTO fetched from builder-API use the normal 'data' property
            // emotes DTO fetched from realm use 'emoteDataADR74' property...
            public Data emoteDataADR74;
            public Data data
            {
                get => emoteDataADR74;
                set
                {
                    emoteDataADR74 = value;
                }
            }

            public override DataBase AbstractData => emoteDataADR74;

            [Serializable]
            public class Data : DataBase
            {
                public bool loop;
            }
        }
    }

    [Serializable]
    public class BuilderEmoteDTO : EmoteDTO
    {
        [Serializable]
        public struct BuilderLambdaResponse : IBuilderLambdaResponse<BuilderEmoteMetadataDto>
        {
            public bool ok;
            public List<BuilderEmoteMetadataDto> data;

            [JsonIgnore]
            public IReadOnlyList<BuilderEmoteMetadataDto> CollectionElements => data;
        }

        [Serializable]
        public class BuilderEmoteMetadataDto : EmoteMetadataDto, IBuilderLambdaResponseElement<BuilderEmoteDTO>
        {
            public Dictionary<string, string> contents;
            public string type;

            [JsonIgnore]
            public IReadOnlyDictionary<string, string> Contents => contents;

            public BuilderEmoteDTO BuildElementDTO(string contentDownloadUrl)
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

                return new BuilderEmoteDTO()
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
