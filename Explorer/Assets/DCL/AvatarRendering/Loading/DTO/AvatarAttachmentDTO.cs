﻿#nullable disable

using System;

namespace DCL.AvatarRendering.Loading.DTO
{
    public abstract class AvatarAttachmentDTO<TMetadata> : AvatarAttachmentDTO where TMetadata: AvatarAttachmentDTO.MetadataBase
    {
        public TMetadata metadata;

        public override MetadataBase Metadata => metadata;
    }

    /// <summary>
    /// Contains common serialization data for Wearables and Emotes
    /// </summary>
    public abstract class AvatarAttachmentDTO
    {
        public string id;
        public string type;
        public string[] pointers;
        public long timestamp;
        public string version;
        public Content[] content;

        public abstract MetadataBase Metadata { get; }

        [Serializable]
        public struct Representation
        {
            public string[] bodyShapes;
            public string mainFile;
            public string[] contents;
            public string[] overrideHides;
            public string[] overrideReplaces;

            public static Representation NewFakeRepresentation() =>
                new()
                {
                    bodyShapes = Array.Empty<string>(),
                    mainFile = string.Empty,
                    contents = Array.Empty<string>(),
                    overrideHides = Array.Empty<string>(),
                    overrideReplaces = Array.Empty<string>(),
                };
        }

        [Serializable]
        public abstract class MetadataBase
        {
            public abstract DataBase AbstractData { get; }

            //urn
            public string id;
            public string name;

            public I18n[] i18n;
            public string thumbnail;

            public string rarity;
            public string description;
        }

        [Serializable]
        public class DataBase
        {
            public Representation[] representations;
            public string category;
            public string[] tags;
            public string[] replaces;
            public string[] hides;
            public string[] removesDefaultHiding;
        }

        [Serializable]
        public struct Content
        {
            public string file;
            public string hash;
        }

        [Serializable]
        public struct I18n
        {
            public string code;
            public string text;
        }
    }

    public static class AvatarAttachmentDTOExtensions
    {
        public static string GetHash(this AvatarAttachmentDTO DTO) =>
            DTO.id;
    }
}
