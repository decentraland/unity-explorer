using System;
using System.Collections.Generic;

namespace DCL.AvatarRendering.Wearables.Helpers
{
    [Serializable]
    public struct WearableDTO
    {
        public string version;

        //hash
        public string id;
        public string type;
        public string[] pointers;
        public long timestamp;

        public WearableMetadataDto metadata;
        public WearableContentDto[] content;

        [Serializable]
        public struct WearableContentDto
        {
            public string file;
            public string hash;
        }

        [Serializable]
        public struct WearableMetadataDto
        {
            public DataDto data;

            //urn
            public string id;
            public string name;

            public I18n[] i18n;
            public string thumbnail;

            public string rarity;
            public string description;

            [Serializable]
            public class I18n
            {
                public string code;
                public string text;
            }

            [Serializable]
            public class Representation
            {
                public string[] bodyShapes;
                public string mainFile;
                public string[] contents;
                public string[] overrideHides;
                public string[] overrideReplaces;
            }

            [Serializable]
            public struct RepresentationContentsDto
            {
                public string key;
                public string url;
            }

            [Serializable]
            public class DataDto
            {
                public Representation[] representations;
                public string category;
                public string[] tags;
                public string[] replaces;
                public string[] hides;
                public string[] removesDefaultHiding;
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

        public void Sanitize(bool hasEmptyDefaultWearableAB)
        {
            // If the default wearable is empty, we need to remove all the hiding/replacing/removing data
            if (hasEmptyDefaultWearableAB)
            {
                metadata.data.hides = Array.Empty<string>();
                metadata.data.replaces = Array.Empty<string>();
                metadata.data.removesDefaultHiding = Array.Empty<string>();

                for (var i = 0; i < metadata.data.representations.Length; i++)
                {
                    metadata.data.representations[i].overrideHides = Array.Empty<string>();
                    metadata.data.representations[i].overrideReplaces = Array.Empty<string>();
                }
            }
        }
    }
}
