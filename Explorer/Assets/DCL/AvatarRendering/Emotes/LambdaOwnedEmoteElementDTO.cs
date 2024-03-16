using System;
using System.Collections.Generic;

namespace DCL.AvatarRendering.Emotes
{
    [Serializable]
    public struct LambdaOwnedEmoteElementDTO
    {
        public string type;
        public string urn;
        public string name;
        public string category;
        public EmoteDTO entity;
        public IndividualDataDTO[] individualData;

        [Serializable]
        public struct IndividualDataDTO
        {
            public string id;
            public string tokenId;
            public string transferredAt;
            public string price;
        }
    }

    [Serializable]
    public struct LambdaOwnedEmoteElementList
    {
        public List<LambdaOwnedEmoteElementDTO> elements;
        public int totalAmount;
    }
}
