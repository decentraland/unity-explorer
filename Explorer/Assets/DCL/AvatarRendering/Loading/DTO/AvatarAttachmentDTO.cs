using DCL.Ipfs;
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
    public abstract class AvatarAttachmentDTO : EntityDefinitionBase
    {
        public string? ContentDownloadUrl { get; protected set; }

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
        public abstract class MetadataBase : TrimmedAvatarAttachmentDTO.TrimmedMetadataBase<DataBase>
        {
            public string name;

            public I18n[] i18n;
            public string thumbnail;

            public string description;
        }

        [Serializable]
        public abstract class DataBase : TrimmedAvatarAttachmentDTO.TrimmedDataBase
        {
            public string[] tags;
            public string[] replaces;
            public string[] hides;
            public string[] removesDefaultHiding;
            public bool outlineCompatible = true;
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
