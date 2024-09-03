using DCL.AvatarRendering.Loading;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;

namespace DCL.AvatarRendering.Emotes
{
    [Serializable]
    public class LambdaOwnedEmoteElementDTO : ILambdaResponseElement<EmoteDTO>
    {
        public string type;
        public string urn;
        public string name;
        public string category;
        public EmoteDTO entity;
        public ElementIndividualDataDto[] individualData;

        [JsonIgnore]
        public EmoteDTO Entity => entity;

        [JsonIgnore]
        public IReadOnlyList<ElementIndividualDataDto> IndividualData => individualData;

    }

    [Serializable]
    public struct LambdaOwnedEmoteElementList : IAttachmentLambdaResponse<LambdaOwnedEmoteElementDTO>
    {
        public List<LambdaOwnedEmoteElementDTO> elements;
        public int totalAmount;

        [JsonIgnore]
        public IReadOnlyList<LambdaOwnedEmoteElementDTO> Elements => elements;

        [JsonIgnore]
        public int TotalAmount => totalAmount;
    }
}
