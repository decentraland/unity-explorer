using DCL.AvatarRendering.Wearables.Components;
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

        public WearableComponent ToWearableItem(string contentBaseUrl)
        {
            var wearableComponent = new WearableComponent
            {
                urn = metadata.id,
                wearableContent = new WearableContent
                {
                    representations = new WearableRepresentation[metadata.data.representations.Length],
                    category = metadata.data.category,
                    hides = metadata.data.hides,
                    replaces = metadata.data.replaces,
                    tags = metadata.data.tags,
                    removesDefaultHiding = metadata.data.removesDefaultHiding,
                },
                baseUrl = contentBaseUrl,
                description = metadata.description,
                i18n = metadata.i18n,
                hash = id,
                rarity = metadata.rarity,
                thumbnailHash = GetContentHashByFileName(metadata.thumbnail),
            };

            for (var i = 0; i < metadata.data.representations.Length; i++)
            {
                WearableMetadataDto.Representation representation = metadata.data.representations[i];

                wearableComponent.wearableContent.representations[i] = new WearableRepresentation
                {
                    bodyShapes = representation.bodyShapes,
                    mainFile = representation.mainFile,
                    overrideHides = representation.overrideHides,
                    overrideReplaces = representation.overrideReplaces,
                    contents = new WearableMappingPair[representation.contents.Length],
                };

                for (var z = 0; z < representation.contents.Length; z++)
                {
                    string fileName = representation.contents[z];
                    string hash = GetContentHashByFileName(fileName);

                    wearableComponent.wearableContent.representations[i].contents[z] = new WearableMappingPair
                    {
                        url = $"{contentBaseUrl}/{hash}",
                        hash = hash,
                        key = fileName,
                    };
                }
            }

            return wearableComponent;
        }

        public string GetContentHashByFileName(string fileName)
        {
            foreach (WearableContentDto dto in content)
                if (dto.file == fileName)
                    return dto.hash;

            return null;
        }
    }

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

        public i18n[] i18n;
        public string thumbnail;

        public string rarity;
        public string description;

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
