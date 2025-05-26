using DCL.AvatarRendering.Loading;
using DCL.AvatarRendering.Loading.DTO;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using UnityEngine.Serialization;

namespace DCL.AvatarRendering.Emotes
{
    [Serializable]
    public class EmoteDTO : AvatarAttachmentDTO<EmoteDTO.EmoteMetadataDto>
    {
        [Serializable]
        public class EmoteMetadataDto : MetadataBase
        {
            [FormerlySerializedAs("emoteDataADR74")] public Data data = new();

            public override DataBase AbstractData => data;

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
                Content[] parsedContent = new Content[contents.Count];

                using (var enumerator = contents.GetEnumerator())
                {
                    for (int i = 0; i < parsedContent.Length; i++)
                    {
                        enumerator.MoveNext();
                        parsedContent[i] = new Content()
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
