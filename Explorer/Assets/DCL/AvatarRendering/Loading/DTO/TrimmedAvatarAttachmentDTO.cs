using DCL.Ipfs;
using System;

namespace DCL.AvatarRendering.Loading.DTO
{
    public abstract class TrimmedAvatarAttachmentDTO<TMetadata> : TrimmedAvatarAttachmentDTO where TMetadata : TrimmedAvatarAttachmentDTO.TrimmedMetadataBase
    {
        public TMetadata metadata;

        public override TrimmedMetadataBase Metadata => metadata;
    }

    public abstract class TrimmedAvatarAttachmentDTO : TrimmedEntityDefinitionBase
    {
        public string? ContentDownloadUrl { get; protected set; }

        public abstract TrimmedMetadataBase Metadata { get; }

        [Serializable]
        public struct Representation
        {
            public string[] bodyShapes;

            public static Representation NewFakeRepresentation() =>
                new()
                {
                    bodyShapes = Array.Empty<string>(),
                };
        }

        [Serializable]
        public abstract class TrimmedMetadataBase
        {
            public abstract TrimmedDataBase AbstractData { get; }

            public string id;
            public string rarity;
        }

        [Serializable]
        public abstract class TrimmedDataBase
        {
            public Representation[] representations;
            public string category;
        }
    }

    public static class TrimmedAvatarAttachmentDTOExtensions
    {
        public static string GetHash(this TrimmedAvatarAttachmentDTO DTO) =>
            DTO.id;
    }
}
