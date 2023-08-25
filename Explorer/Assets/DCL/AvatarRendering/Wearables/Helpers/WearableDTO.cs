using DCL.AvatarRendering.Wearables.Components;
using SceneRunner.Scene;
using System;

namespace DCL.AvatarRendering.Wearables.Helpers
{
    [Serializable]
    public class WearableDTO
    {
        public string version;
        public string id;
        public string type;
        public string[] pointers;
        public long timestamp;

        public EntityDto.MetadataDto metadata;
        public EntityDto.ContentDto[] content;

        public SceneAssetBundleManifest AssetBundleManifest;

        public void ToWearableItem(ref WearableComponent wearableComponent, string contentBaseUrl)
        {
            //id = metadata.id,

            wearableComponent.wearableContent = new WearableContent
            {
                representations = new WearableRepresentation[metadata.data.representations.Length],
                category = metadata.data.category,
                hides = metadata.data.hides,
                replaces = metadata.data.replaces,
                tags = metadata.data.tags,
                removesDefaultHiding = metadata.data.removesDefaultHiding,
            };

            wearableComponent.baseUrl = contentBaseUrl;
            wearableComponent.description = metadata.description;
            wearableComponent.i18n = metadata.i18n;
            wearableComponent.hash = id;
            wearableComponent.rarity = metadata.rarity;
            wearableComponent.thumbnailHash = GetContentHashByFileName(metadata.thumbnail);
            wearableComponent.AssetBundleManifest = AssetBundleManifest;

            for (var i = 0; i < metadata.data.representations.Length; i++)
            {
                EntityDto.MetadataDto.Representation representation = metadata.data.representations[i];

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
        }

        public string GetContentHashByFileName(string fileName)
        {
            foreach (EntityDto.ContentDto dto in content)
                if (dto.file == fileName)
                    return dto.hash;

            return null;
        }
    }

    [Serializable]
    public class EntityDto
    {
        [Serializable]
        public class ContentDto
        {
            public string file;
            public string hash;
        }

        [Serializable]
        public class MetadataDto
        {
            public DataDto data;
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
    }
}
